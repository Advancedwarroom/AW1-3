// MapEditorHelper.cs - 编辑器大更新：图形填充、区域填充、地形填充、覆盖全图、选区复制粘贴（矩形+不规则）、对称放置
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public enum FillShapeType
{
    Circle, Square, Rectangle, Diamond, Triangle
}

public enum SymmetryAxis
{
    None, Vertical, Horizontal
}

public enum SelectModeType
{
    None, Rectangle, Irregular
}

// ========== 选区数据 ==========
public class MapSelection
{
    public string id;
    public bool isFavorite;
    public Rect2I bounds;
    public List<SelectionCellData> cells = new();
    public long savedTimestamp;
    public bool isIrregular;
}

public class SelectionCellData
{
    public Vector2I gridPos;
    public GridType gridType;
    public Dictionary<string, string> gridProperties = new();
    public SavedUnitData unitData;
    public SavedWeaponData weaponData;
    public SavedFacilityData facilityData;
}

// ========== 撤销操作记录 ==========
public enum EditorActionType
{
    PlaceTerrain, PlaceUnit, PlaceWeapon, PlaceFacility,
    DeleteUnit, DeleteWeapon, DeleteFacility,
    ReplaceTexture, ClearMap, FillShape, FillArea, FillTerrain,
    PasteSelection, SymmetryPlace
}

public class EditorActionRecord
{
    public EditorActionType actionType;
    public Vector2I position;
    public GridType oldTerrain;
    public GridType newTerrain;
    public SavedUnitData oldUnitData;
    public SavedWeaponData oldWeaponData;
    public SavedFacilityData oldFacilityData;
    public List<EditorActionRecord> subActions = new();
    public long timestamp;
}

public partial class MapEditorHelper : Node
{
    [Export] public GridManager gridManager;
    [Export] public GameManager gameManager;
    [Export] public TerrainEditor terrainEditor;
    [Export] public ExtraSave extraSave;

    // ========== 图形填充状态 ==========
    public bool IsShapeFillMode { get; private set; } = false;
    private FillShapeType currentFillShape = FillShapeType.Circle;
    private GridType fillShapeTerrain = GridType.GROUND;
    private Vector2I shapeAnchorPos = new Vector2I(-1, -1);
    private Vector2I shapeCurrentPos = new Vector2I(-1, -1);
    private bool isDraggingShapeEdge = false;
    private List<Grids> shapePreviewGrids = new();
    private Color shapePreviewColor = new Color(0, 1, 0, 0.4f);

    // ========== 区域/地形填充状态 ==========
    public bool IsAreaFillMode { get; private set; } = false;
    public bool IsTerrainFillMode { get; private set; } = false;
    private GridType areaFillTargetTerrain = GridType.GROUND;
    private GridType areaFillNewTerrain = GridType.GROUND;
    private bool areaFillIncludeAdjacent = false;

    // ========== 覆盖全图状态 ==========
    public bool IsClearMapMode { get; private set; } = false;
    private GridType clearMapTerrain = GridType.GROUND;
    private bool clearMapRemoveEntities = true;

    // ========== 选区复制粘贴（矩形 + 不规则） ==========
    public bool IsSelectMode { get; private set; } = false;
    public SelectModeType CurrentSelectMode { get; private set; } = SelectModeType.None;
    public bool IsPasteMode { get; private set; } = false;

    // 矩形选区
    private Vector2I selectStartPos = new Vector2I(-1, -1);
    private Vector2I selectEndPos = new Vector2I(-1, -1);

    // 不规则选区
    private HashSet<Vector2I> irregularSelectedCells = new();
    private List<Grids> irregularPreviewGrids = new();
    private Color irregularSelectColor = new Color(0, 0.8f, 1, 0.5f);

    // 选区剪贴板
    private List<MapSelection> selectionClipboard = new();
    private MapSelection currentPasteSelection = null;
    private const int MAX_SELECTIONS = 5;
    private const int MAX_FAVORITE_SELECTIONS = 3;

    // ========== 对称放置 ==========
    public bool IsSymmetryMode { get; private set; } = false;
    public SymmetryAxis CurrentSymmetryAxis { get; private set; } = SymmetryAxis.None;
    public int symmetryAxisLine = -1;
    private List<Grids> symmetryPreviewGrids = new();

    // ========== 撤销/前进 ==========
    private List<EditorActionRecord> actionHistory = new();
    private int currentHistoryIndex = -1;
    private const int MAX_HISTORY = 20;

    // ========== UI 引用 ==========
    private Panel helperPanel;
    private Button helperToggleBtn;
    private bool isHelperPanelDragging = false;
    private Vector2 helperPanelDragOffset;
    private Label statusHintLabel;
    private Button irregularSelectModeBtn;

    public override void _Ready()
    {
        AddToGroup("map_editor_helper");
        if (gridManager == null)
            gridManager = GetTree().GetFirstNodeInGroup("grid_manager") as GridManager;
        if (gameManager == null)
            gameManager = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (terrainEditor == null)
            terrainEditor = GetTree().GetFirstNodeInGroup("terrain_editor") as TerrainEditor;
        if (extraSave == null)
            extraSave = GetTree().GetFirstNodeInGroup("extra_save") as ExtraSave;

        CreateHelperUI();
    }

    // ========== UI 创建 ==========
    private void CreateHelperUI()
    {
        helperToggleBtn = new Button();
        helperToggleBtn.Name = "HelperToggleBtn";
        helperToggleBtn.Text = "🛠️ 编辑器助手";
        helperToggleBtn.CustomMinimumSize = new Vector2(120, 40);
        helperToggleBtn.Pressed += OnHelperToggle;
        AddChild(helperToggleBtn);

        helperPanel = new Panel();
        helperPanel.Name = "HelperPanel";
        helperPanel.CustomMinimumSize = new Vector2(300, 580);
        helperPanel.Visible = false;
        helperPanel.MouseFilter = Control.MouseFilterEnum.Pass;
        AddChild(helperPanel);

        var vbox = new VBoxContainer();
        vbox.Name = "HelperVBox";
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        helperPanel.AddChild(vbox);

        var titleBar = new HBoxContainer();
        titleBar.Name = "TitleBar";
        titleBar.CustomMinimumSize = new Vector2(0, 30);
        vbox.AddChild(titleBar);

        var titleLabel = new Label();
        titleLabel.Text = "=== 编辑器助手 ===";
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleBar.AddChild(titleLabel);

        var closeBtn = new Button();
        closeBtn.Text = "X";
        closeBtn.CustomMinimumSize = new Vector2(30, 30);
        closeBtn.Pressed += () => helperPanel.Visible = false;
        titleBar.AddChild(closeBtn);
        titleBar.GuiInput += OnTitleBarGuiInput;

        statusHintLabel = new Label();
        statusHintLabel.Name = "StatusHint";
        statusHintLabel.Text = "就绪";
        statusHintLabel.HorizontalAlignment = HorizontalAlignment.Center;
        statusHintLabel.AddThemeColorOverride("font_color", Colors.Gray);
        statusHintLabel.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(statusHintLabel);

        vbox.AddChild(CreateSectionLabel("📐 图形填充"));
        var shapeGrid = new GridContainer();
        shapeGrid.Columns = 3;
        vbox.AddChild(shapeGrid);
        AddShapeButton(shapeGrid, "⭕ 圆", FillShapeType.Circle);
        AddShapeButton(shapeGrid, "⬜ 方", FillShapeType.Square);
        AddShapeButton(shapeGrid, "▭ 长方", FillShapeType.Rectangle);
        AddShapeButton(shapeGrid, "◆ 菱形", FillShapeType.Diamond);
        AddShapeButton(shapeGrid, "🔺 三角", FillShapeType.Triangle);

        var shapeTerrainBtn = new Button();
        shapeTerrainBtn.Text = "选择填充地形...";
        shapeTerrainBtn.Pressed += () => OpenTerrainPicker((gt) => fillShapeTerrain = gt);
        vbox.AddChild(shapeTerrainBtn);

        vbox.AddChild(CreateSectionLabel("🎨 填充工具"));
        var areaFillBtn = new Button();
        areaFillBtn.Text = "区域填充(相邻)";
        areaFillBtn.Pressed += () => StartAreaFillMode(true);
        vbox.AddChild(areaFillBtn);

        var terrainFillBtn = new Button();
        terrainFillBtn.Text = "地形填充(全图)";
        terrainFillBtn.Pressed += () => StartTerrainFillMode();
        vbox.AddChild(terrainFillBtn);

        var fillTerrainPicker = new Button();
        fillTerrainPicker.Text = "选择目标新地形...";
        fillTerrainPicker.Pressed += () => OpenTerrainPicker((gt) => areaFillNewTerrain = gt);
        vbox.AddChild(fillTerrainPicker);

        vbox.AddChild(CreateSectionLabel("🗑️ 覆盖全图"));
        var clearMapBtn = new Button();
        clearMapBtn.Text = "覆盖全图并清空实体";
        clearMapBtn.AddThemeColorOverride("font_color", Colors.Red);
        clearMapBtn.Pressed += StartClearMapMode;
        vbox.AddChild(clearMapBtn);

        var clearMapTerrainBtn = new Button();
        clearMapTerrainBtn.Text = "选择覆盖地形...";
        clearMapTerrainBtn.Pressed += () => OpenTerrainPicker((gt) => clearMapTerrain = gt);
        vbox.AddChild(clearMapTerrainBtn);

        vbox.AddChild(CreateSectionLabel("📋 选区操作"));
        var rectSelectBtn = new Button();
        rectSelectBtn.Text = "🔲 矩形选区";
        rectSelectBtn.Pressed += () => StartSelectMode(SelectModeType.Rectangle);
        vbox.AddChild(rectSelectBtn);

        irregularSelectModeBtn = new Button();
        irregularSelectModeBtn.Text = "✏️ 不规则选区";
        irregularSelectModeBtn.Pressed += OnIrregularSelectBtnPressed;
        vbox.AddChild(irregularSelectModeBtn);

        var pasteBtn = new Button();
        pasteBtn.Text = "📋 粘贴选区";
        pasteBtn.Pressed += ShowPasteMenu;
        vbox.AddChild(pasteBtn);

        var symmetryBtn = new Button();
        symmetryBtn.Text = "🔀 对称: 关";
        symmetryBtn.Name = "SymmetryBtn";
        symmetryBtn.Pressed += ToggleSymmetryMode;
        vbox.AddChild(symmetryBtn);

        vbox.AddChild(CreateSectionLabel("↩️ 撤销/前进"));
        var undoRedoHBox = new HBoxContainer();
        vbox.AddChild(undoRedoHBox);
        var undoBtn = new Button();
        undoBtn.Text = "↩️ 撤销";
        undoBtn.Pressed += Undo;
        undoRedoHBox.AddChild(undoBtn);
        var redoBtn = new Button();
        redoBtn.Text = "↪️ 前进";
        redoBtn.Pressed += Redo;
        undoRedoHBox.AddChild(redoBtn);

        var historyLabel = new Label();
        historyLabel.Name = "HistoryLabel";
        historyLabel.Text = "历史: 0/0";
        historyLabel.AddThemeColorOverride("font_color", Colors.Gray);
        vbox.AddChild(historyLabel);

        vbox.AddChild(CreateSectionLabel("🖼️ 贴图替换"));
        var textureReplaceBtn = new Button();
        textureReplaceBtn.Text = "替换单位/兵器贴图";
        textureReplaceBtn.Pressed += OpenTextureReplaceDialog;
        vbox.AddChild(textureReplaceBtn);

        var vp = GetViewport().GetVisibleRect().Size;
        helperPanel.Position = new Vector2(vp.X - 320, 50);
        helperToggleBtn.Position = new Vector2(vp.X - 150, 10);
    }

    private Label CreateSectionLabel(string text)
    {
        var label = new Label();
        label.Text = text;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeColorOverride("font_color", Colors.Yellow);
        label.AddThemeFontSizeOverride("font_size", 14);
        return label;
    }

    private void AddShapeButton(GridContainer parent, string text, FillShapeType shape)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(80, 35);
        btn.Pressed += () => StartShapeFillMode(shape);
        parent.AddChild(btn);
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton btn && btn.ButtonIndex == MouseButton.Left)
        {
            if (btn.Pressed)
            {
                isHelperPanelDragging = true;
                helperPanelDragOffset = helperPanel.GetGlobalMousePosition() - helperPanel.GlobalPosition;
            }
            else
            {
                isHelperPanelDragging = false;
            }
        }
    }

    public override void _Process(double delta)
    {
        if (isHelperPanelDragging && helperPanel != null)
        {
            helperPanel.GlobalPosition = helperPanel.GetGlobalMousePosition() - helperPanelDragOffset;
        }
    }

    private void OnHelperToggle()
    {
        helperPanel.Visible = !helperPanel.Visible;
        if (helperPanel.Visible) helperPanel.Size = helperPanel.CustomMinimumSize;
    }

    private void UpdateStatusHint(string text)
    {
        if (statusHintLabel != null) statusHintLabel.Text = text;
    }

    private void OpenTerrainPicker(Action<GridType> onSelected)
    {
        var dialog = new AcceptDialog();
        dialog.Title = "选择地形";
        dialog.Size = new Vector2I(400, 300);
        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(380, 250);
        dialog.AddChild(scroll);
        var grid = new GridContainer();
        grid.Columns = 4;
        scroll.AddChild(grid);
        foreach (GridType gt in Enum.GetValues(typeof(GridType)))
        {
            var btn = new Button();
            btn.Text = gt.ToString();
            btn.CustomMinimumSize = new Vector2(90, 35);
            btn.Pressed += () => { onSelected(gt); dialog.QueueFree(); };
            grid.AddChild(btn);
        }
        AddChild(dialog);
        dialog.PopupCentered();
    }

    // ========== 图形填充模式 ==========
    private void StartShapeFillMode(FillShapeType shape)
    {
        CancelAllModes();
        IsShapeFillMode = true;
        currentFillShape = shape;
        shapeAnchorPos = new Vector2I(-1, -1);
        shapeCurrentPos = new Vector2I(-1, -1);
        isDraggingShapeEdge = false;
        UpdateStatusHint($"图形填充: {shape} | 点击确定中心，拖拽调整大小");
    }

    public void OnGridClickForShapeFill(Grids grid)
    {
        if (!IsShapeFillMode || grid == null || gridManager?.map == null) return;
        var pos = grid.GridIndex;

        if (shapeAnchorPos.X < 0)
        {
            shapeAnchorPos = pos;
            shapeCurrentPos = pos;
            isDraggingShapeEdge = true;
            ShowShapePreview();
            UpdateStatusHint("拖拽调整大小，点击内部确认，点击外部取消");
        }
        else if (isDraggingShapeEdge)
        {
            if (IsPointInsideShape(pos, shapeAnchorPos, shapeCurrentPos, currentFillShape))
            {
                ApplyShapeFill();
                CancelShapeFillMode();
            }
            else
            {
                CancelShapeFillMode();
            }
        }
    }

    public void OnGridHoverForShapeFill(Grids grid)
    {
        if (!IsShapeFillMode || !isDraggingShapeEdge || grid == null) return;
        if (shapeAnchorPos.X < 0) return;
        shapeCurrentPos = grid.GridIndex;
        ShowShapePreview();
    }

    private void ShowShapePreview()
    {
        ClearShapePreview();
        var affected = GetShapeGrids(shapeAnchorPos, shapeCurrentPos, currentFillShape);
        foreach (var g in affected)
        {
            if (g != null && IsInstanceValid(g))
            {
                g.Modulate = shapePreviewColor;
                shapePreviewGrids.Add(g);
            }
        }
    }

    private void ClearShapePreview()
    {
        foreach (var g in shapePreviewGrids)
        {
            if (g != null && IsInstanceValid(g))
                g.Modulate = Colors.White;
        }
        shapePreviewGrids.Clear();
    }

    private List<Grids> GetShapeGrids(Vector2I center, Vector2I edge, FillShapeType shape)
    {
        var result = new List<Grids>();
        if (gridManager?.map == null) return result;
        int dx = Mathf.Abs(edge.X - center.X);
        int dy = Mathf.Abs(edge.Y - center.Y);
        int radius = Mathf.Max(dx, dy);
        int w = gridManager.map.GetLength(0);
        int h = gridManager.map.GetLength(1);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                bool inside = false;
                switch (shape)
                {
                    case FillShapeType.Circle:
                        inside = (x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y) <= radius * radius;
                        break;
                    case FillShapeType.Square:
                        inside = Mathf.Abs(x - center.X) <= radius && Mathf.Abs(y - center.Y) <= radius;
                        break;
                    case FillShapeType.Rectangle:
                        int minX = Mathf.Min(center.X, edge.X);
                        int maxX = Mathf.Max(center.X, edge.X);
                        int minY = Mathf.Min(center.Y, edge.Y);
                        int maxY = Mathf.Max(center.Y, edge.Y);
                        inside = x >= minX && x <= maxX && y >= minY && y <= maxY;
                        break;
                    case FillShapeType.Diamond:
                        inside = Mathf.Abs(x - center.X) + Mathf.Abs(y - center.Y) <= radius;
                        break;
                    case FillShapeType.Triangle:
                        int triHeight = Mathf.Abs(edge.Y - center.Y);
                        int triHalfBase = Mathf.Abs(edge.X - center.X);
                        if (triHeight == 0) triHeight = 1;
                        int relY = y - center.Y;
                        if (relY >= 0 && relY <= triHeight)
                        {
                            float ratio = 1f - (float)relY / triHeight;
                            int allowedHalfBase = Mathf.RoundToInt(triHalfBase * ratio);
                            inside = Mathf.Abs(x - center.X) <= allowedHalfBase;
                        }
                        break;
                }
                if (inside && x >= 0 && x < w && y >= 0 && y < h)
                {
                    var g = gridManager.map[x, y];
                    if (g != null) result.Add(g);
                }
            }
        }
        return result;
    }

    private bool IsPointInsideShape(Vector2I point, Vector2I center, Vector2I edge, FillShapeType shape)
    {
        var grids = GetShapeGrids(center, edge, shape);
        return grids.Any(g => g.GridIndex == point);
    }

    private void ApplyShapeFill()
    {
        var affected = GetShapeGrids(shapeAnchorPos, shapeCurrentPos, currentFillShape);
        var batchRecord = new EditorActionRecord
        {
            actionType = EditorActionType.FillShape,
            subActions = new List<EditorActionRecord>()
        };
        foreach (var g in affected)
        {
            if (g == null || !IsInstanceValid(g)) continue;
            batchRecord.subActions.Add(new EditorActionRecord
            {
                actionType = EditorActionType.FillShape,
                position = g.GridIndex,
                oldTerrain = g.gridType,
                newTerrain = fillShapeTerrain
            });
            g.gridType = fillShapeTerrain;
            g.UpdateTerrainIcon();
        }
        PushAction(batchRecord);
        UpdateStatusHint($"已填充 {affected.Count} 个格子");
    }

    private void CancelShapeFillMode()
    {
        ClearShapePreview();
        IsShapeFillMode = false;
        shapeAnchorPos = new Vector2I(-1, -1);
        shapeCurrentPos = new Vector2I(-1, -1);
        isDraggingShapeEdge = false;
    }

    // ========== 区域填充（相邻相同地形） ==========
    private void StartAreaFillMode(bool includeAdjacent)
    {
        CancelAllModes();
        IsAreaFillMode = true;
        areaFillIncludeAdjacent = includeAdjacent;
        UpdateStatusHint($"区域填充: 点击目标格子，相邻相同地形替换为 {areaFillNewTerrain}");
    }

    private void StartTerrainFillMode()
    {
        CancelAllModes();
        IsTerrainFillMode = true;
        areaFillIncludeAdjacent = false;
        UpdateStatusHint($"地形填充: 点击目标格子，全图相同地形替换为 {areaFillNewTerrain}");
    }

    public void OnGridClickForAreaFill(Grids grid)
    {
        if ((!IsAreaFillMode && !IsTerrainFillMode) || grid == null || gridManager?.map == null) return;
        GridType targetType = grid.gridType;
        var toFill = new List<Grids>();
        int w = gridManager.map.GetLength(0);
        int h = gridManager.map.GetLength(1);

        if (IsTerrainFillMode)
        {
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (gridManager.map[x, y]?.gridType == targetType)
                        toFill.Add(gridManager.map[x, y]);
        }
        else
        {
            var visited = new HashSet<Vector2I>();
            var queue = new Queue<Grids>();
            queue.Enqueue(grid);
            visited.Add(grid.GridIndex);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                toFill.Add(current);
                var neighbors = new Vector2I[]
                {
                    new Vector2I(current.GridIndex.X + 1, current.GridIndex.Y),
                    new Vector2I(current.GridIndex.X - 1, current.GridIndex.Y),
                    new Vector2I(current.GridIndex.X, current.GridIndex.Y + 1),
                    new Vector2I(current.GridIndex.X, current.GridIndex.Y - 1)
                };
                foreach (var n in neighbors)
                {
                    if (n.X >= 0 && n.X < w && n.Y >= 0 && n.Y < h && !visited.Contains(n))
                    {
                        var ng = gridManager.map[n.X, n.Y];
                        if (ng != null && ng.gridType == targetType)
                        {
                            visited.Add(n);
                            queue.Enqueue(ng);
                        }
                    }
                }
            }
        }

        var batchRecord = new EditorActionRecord
        {
            actionType = IsTerrainFillMode ? EditorActionType.FillTerrain : EditorActionType.FillArea,
            subActions = new List<EditorActionRecord>()
        };
        foreach (var g in toFill)
        {
            batchRecord.subActions.Add(new EditorActionRecord
            {
                actionType = batchRecord.actionType,
                position = g.GridIndex,
                oldTerrain = g.gridType,
                newTerrain = areaFillNewTerrain
            });
            g.gridType = areaFillNewTerrain;
            g.UpdateTerrainIcon();
        }
        PushAction(batchRecord);
        UpdateStatusHint($"已填充 {toFill.Count} 个格子");
        CancelAllModes();
    }

    // ========== 覆盖全图 ==========
    private void StartClearMapMode()
    {
        var dialog = new AcceptDialog();
        dialog.Title = "⚠️ 确认覆盖全图?";
        dialog.DialogText = $"这将把所有格子变为 {clearMapTerrain}，并删除所有单位/兵器/设施！\n\n确定要继续吗？";
        dialog.Size = new Vector2I(400, 200);
        dialog.GetOkButton().Text = "确认覆盖";
        dialog.GetOkButton().AddThemeColorOverride("font_color", Colors.Red);
        dialog.Confirmed += () => { ApplyClearMap(); dialog.QueueFree(); };
        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void ApplyClearMap()
    {
        if (gridManager?.map == null) return;
        int w = gridManager.map.GetLength(0);
        int h = gridManager.map.GetLength(1);
        var batchRecord = new EditorActionRecord
        {
            actionType = EditorActionType.ClearMap,
            subActions = new List<EditorActionRecord>()
        };

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var g = gridManager.map[x, y];
                if (g == null) continue;
                var sub = new EditorActionRecord
                {
                    actionType = EditorActionType.ClearMap,
                    position = g.GridIndex,
                    oldTerrain = g.gridType
                };
                if (g.infantry != null) sub.oldUnitData = SerializeUnit(g.infantry);
                if (g.weapon != null) sub.oldWeaponData = SerializeWeapon(g.weapon);
                if (g.city != null) sub.oldFacilityData = SerializeFacility(g.city);
                batchRecord.subActions.Add(sub);

                if (clearMapRemoveEntities)
                {
                    if (g.infantry != null) { g.infantry.QueueFree(); g.infantry = null; g.infantries.Clear(); }
                    if (g.weapon != null) { g.weapon.QueueFree(); g.weapon = null; g.weapons.Clear(); }
                    if (g.city != null) { g.city.QueueFree(); g.city = null; }
                }
                g.gridType = clearMapTerrain;
                g.UpdateTerrainIcon();
            }
        }
        PushAction(batchRecord);
        UpdateStatusHint($"全图已覆盖为 {clearMapTerrain}");
    }

    // ========== 选区复制粘贴（矩形 + 不规则） ==========
    private void StartSelectMode(SelectModeType mode)
    {
        CancelAllModes();
        IsSelectMode = true;
        CurrentSelectMode = mode;

        if (mode == SelectModeType.Rectangle)
        {
            selectStartPos = new Vector2I(-1, -1);
            selectEndPos = new Vector2I(-1, -1);
            UpdateStatusHint("矩形选区: 点击确定起点，再次点击确定终点");
        }
        else if (mode == SelectModeType.Irregular)
        {
            irregularSelectedCells.Clear();
            ClearIrregularPreview();
            UpdateStatusHint("不规则选区: 点击格子选中/取消，再次点击按钮完成选区");
        }
    }

    // 不规则选区按钮的特殊逻辑：点击开启，再次点击关闭并保存
    private void OnIrregularSelectBtnPressed()
    {
        if (IsSelectMode && CurrentSelectMode == SelectModeType.Irregular)
        {
            // 再次点击：关闭模式并保存选区
            if (irregularSelectedCells.Count > 0)
            {
                CreateIrregularSelection();
            }
            else
            {
                UpdateStatusHint("未选择任何格子，已取消");
            }
            CancelAllModes();
        }
        else
        {
            // 第一次点击：开启不规则选区
            StartSelectMode(SelectModeType.Irregular);
        }
    }

    public void OnGridClickForSelect(Grids grid)
    {
        if (!IsSelectMode || grid == null) return;
        var pos = grid.GridIndex;

        if (CurrentSelectMode == SelectModeType.Rectangle)
        {
            if (selectStartPos.X < 0)
            {
                selectStartPos = pos;
                UpdateStatusHint("已确定起点，点击终点完成矩形选区");
            }
            else
            {
                selectEndPos = pos;
                CreateRectangleSelection(selectStartPos, selectEndPos);
                IsSelectMode = false;
                CurrentSelectMode = SelectModeType.None;
            }
        }
        else if (CurrentSelectMode == SelectModeType.Irregular)
        {
            // 不规则选区：点击选中/取消
            if (irregularSelectedCells.Contains(pos))
            {
                irregularSelectedCells.Remove(pos);
                grid.Modulate = Colors.White;
                UpdateStatusHint($"已取消 ({pos.X},{pos.Y}) | 当前共 {irregularSelectedCells.Count} 格");
            }
            else
            {
                irregularSelectedCells.Add(pos);
                grid.Modulate = irregularSelectColor;
                UpdateStatusHint($"已选中 ({pos.X},{pos.Y}) | 当前共 {irregularSelectedCells.Count} 格");
            }
        }
    }

    private void CreateRectangleSelection(Vector2I start, Vector2I end)
    {
        if (gridManager?.map == null) return;
        int minX = Mathf.Min(start.X, end.X);
        int maxX = Mathf.Max(start.X, end.X);
        int minY = Mathf.Min(start.Y, end.Y);
        int maxY = Mathf.Max(start.Y, end.Y);

        var cells = new List<SelectionCellData>();
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (x < 0 || x >= gridManager.map.GetLength(0) || y < 0 || y >= gridManager.map.GetLength(1))
                    continue;
                var g = gridManager.map[x, y];
                if (g == null) continue;
                cells.Add(CreateCellData(g, new Vector2I(x - minX, y - minY)));
            }
        }

        var selection = new MapSelection
        {
            id = $"sel_rect_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}",
            isFavorite = false,
            bounds = new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1),
            cells = cells,
            savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            isIrregular = false
        };

        ManageSelectionClipboard(selection);
        UpdateStatusHint($"矩形选区已保存: {cells.Count} 格 | 共 {selectionClipboard.Count}/5 个选区");
    }

    private void CreateIrregularSelection()
    {
        if (gridManager?.map == null || irregularSelectedCells.Count == 0) return;

        // 计算边界
        int minX = irregularSelectedCells.Min(p => p.X);
        int maxX = irregularSelectedCells.Max(p => p.X);
        int minY = irregularSelectedCells.Min(p => p.Y);
        int maxY = irregularSelectedCells.Max(p => p.Y);

        var cells = new List<SelectionCellData>();
        foreach (var pos in irregularSelectedCells)
        {
            if (pos.X < 0 || pos.X >= gridManager.map.GetLength(0) || pos.Y < 0 || pos.Y >= gridManager.map.GetLength(1))
                continue;
            var g = gridManager.map[pos.X, pos.Y];
            if (g == null) continue;
            cells.Add(CreateCellData(g, new Vector2I(pos.X - minX, pos.Y - minY)));
        }

        var selection = new MapSelection
        {
            id = $"sel_irr_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}",
            isFavorite = false,
            bounds = new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1),
            cells = cells,
            savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            isIrregular = true
        };

        ManageSelectionClipboard(selection);
        ClearIrregularPreview();
        UpdateStatusHint($"不规则选区已保存: {cells.Count} 格 | 共 {selectionClipboard.Count}/5 个选区");
    }

    private SelectionCellData CreateCellData(Grids grid, Vector2I relativePos)
    {
        var cell = new SelectionCellData
        {
            gridPos = relativePos,
            gridType = grid.gridType
        };
        cell.gridProperties["canDestroyUnit"] = grid.canDestroyUnit.ToString();
        cell.gridProperties["fixedDamagePerTurn"] = grid.fixedDamagePerTurn.ToString();
        cell.gridProperties["fixedAttackPerTurn"] = grid.fixedAttackPerTurn.ToString();
        cell.gridProperties["visionBonus"] = grid.visionBonus.ToString();
        if (grid.infantry != null) cell.unitData = SerializeUnit(grid.infantry);
        if (grid.weapon != null) cell.weaponData = SerializeWeapon(grid.weapon);
        if (grid.city != null) cell.facilityData = SerializeFacility(grid.city);
        return cell;
    }

    private void ClearIrregularPreview()
    {
        foreach (var pos in irregularSelectedCells)
        {
            if (pos.X < 0 || pos.X >= gridManager.map.GetLength(0) || pos.Y < 0 || pos.Y >= gridManager.map.GetLength(1))
                continue;
            var g = gridManager.map[pos.X, pos.Y];
            if (g != null && IsInstanceValid(g))
                g.Modulate = Colors.White;
        }
        irregularSelectedCells.Clear();
    }

    private void ManageSelectionClipboard(MapSelection newSelection)
    {
        while (selectionClipboard.Count >= MAX_SELECTIONS)
        {
            var toRemove = selectionClipboard.FirstOrDefault(s => !s.isFavorite);
            if (toRemove == null) break;
            selectionClipboard.Remove(toRemove);
        }
        selectionClipboard.Add(newSelection);
    }

    private void ShowPasteMenu()
    {
        if (selectionClipboard.Count == 0)
        {
            UpdateStatusHint("没有可粘贴的选区！");
            return;
        }

        var dialog = new AcceptDialog();
        dialog.Title = "选择要粘贴的选区";
        dialog.Size = new Vector2I(450, 400);

        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(430, 330);
        dialog.AddChild(scroll);

        var vbox = new VBoxContainer();
        scroll.AddChild(vbox);

        for (int i = 0; i < selectionClipboard.Count; i++)
        {
            var sel = selectionClipboard[i];
            var hbox = new HBoxContainer();
            vbox.AddChild(hbox);

            var favLabel = new Label();
            favLabel.Text = sel.isFavorite ? "⭐" : "  ";
            hbox.AddChild(favLabel);

            var typeLabel = new Label();
            typeLabel.Text = sel.isIrregular ? "✏️" : "🔲";
            hbox.AddChild(typeLabel);

            var infoLabel = new Label();
            infoLabel.Text = $"[{i+1}] {sel.bounds.Size.X}x{sel.bounds.Size.Y} | {sel.cells.Count}格";
            infoLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(infoLabel);

            var pasteBtn = new Button();
            pasteBtn.Text = "粘贴";
            pasteBtn.Pressed += () => { StartPasteMode(sel); dialog.QueueFree(); };
            hbox.AddChild(pasteBtn);

            var favBtn = new Button();
            favBtn.Text = sel.isFavorite ? "取消收藏" : "收藏";
            favBtn.Pressed += () =>
            {
                if (sel.isFavorite)
                {
                    sel.isFavorite = false;
                    favBtn.Text = "收藏";
                    favLabel.Text = "  ";
                }
                else
                {
                    int favCount = selectionClipboard.Count(s => s.isFavorite);
                    if (favCount >= MAX_FAVORITE_SELECTIONS)
                    {
                        UpdateStatusHint("收藏已达上限(3个)！");
                        return;
                    }
                    sel.isFavorite = true;
                    favBtn.Text = "取消收藏";
                    favLabel.Text = "⭐";
                }
            };
            hbox.AddChild(favBtn);

            var delBtn = new Button();
            delBtn.Text = "删除";
            delBtn.AddThemeColorOverride("font_color", Colors.Red);
            delBtn.Pressed += () =>
            {
                selectionClipboard.Remove(sel);
                hbox.QueueFree();
            };
            hbox.AddChild(delBtn);
        }

        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void StartPasteMode(MapSelection selection)
    {
        CancelAllModes();
        IsPasteMode = true;
        currentPasteSelection = selection;
        UpdateStatusHint("粘贴模式: 点击目标位置粘贴选区，ESC取消");
    }

    public void OnGridClickForPaste(Grids grid)
    {
        if (!IsPasteMode || grid == null || currentPasteSelection == null) return;

        var offset = grid.GridIndex;
        var batchRecord = new EditorActionRecord
        {
            actionType = EditorActionType.PasteSelection,
            subActions = new List<EditorActionRecord>()
        };

        foreach (var cell in currentPasteSelection.cells)
        {
            int targetX = offset.X + cell.gridPos.X;
            int targetY = offset.Y + cell.gridPos.Y;

            if (targetX < 0 || targetX >= gridManager.map.GetLength(0) ||
                targetY < 0 || targetY >= gridManager.map.GetLength(1))
                continue;

            var g = gridManager.map[targetX, targetY];
            if (g == null) continue;

            var sub = new EditorActionRecord
            {
                actionType = EditorActionType.PasteSelection,
                position = g.GridIndex,
                oldTerrain = g.gridType
            };
            if (g.infantry != null) sub.oldUnitData = SerializeUnit(g.infantry);
            if (g.weapon != null) sub.oldWeaponData = SerializeWeapon(g.weapon);
            if (g.city != null) sub.oldFacilityData = SerializeFacility(g.city);
            batchRecord.subActions.Add(sub);

            g.gridType = cell.gridType;
            g.UpdateTerrainIcon();

            if (cell.gridProperties.TryGetValue("canDestroyUnit", out string cdu))
                g.canDestroyUnit = bool.Parse(cdu);
            if (cell.gridProperties.TryGetValue("fixedDamagePerTurn", out string fdp))
                g.fixedDamagePerTurn = int.Parse(fdp);
            if (cell.gridProperties.TryGetValue("fixedAttackPerTurn", out string fap))
                g.fixedAttackPerTurn = int.Parse(fap);
            if (cell.gridProperties.TryGetValue("visionBonus", out string vb))
                g.visionBonus = int.Parse(vb);

            if (cell.unitData != null) SpawnUnitFromData(g, cell.unitData);
            if (cell.weaponData != null) SpawnWeaponFromData(g, cell.weaponData);
            if (cell.facilityData != null) SpawnFacilityFromData(g, cell.facilityData);
        }

        PushAction(batchRecord);
        UpdateStatusHint($"已粘贴 {currentPasteSelection.cells.Count} 个格子");
        CancelAllModes();
    }

    // ========== 对称放置 ==========
    private void ToggleSymmetryMode()
    {
        if (IsSymmetryMode)
        {
            IsSymmetryMode = false;
            CurrentSymmetryAxis = SymmetryAxis.None;
            symmetryAxisLine = -1;
            ClearSymmetryPreview();
            UpdateStatusHint("对称放置已关闭");
            var btn = helperPanel?.GetNodeOrNull<Button>("HelperVBox/SymmetryBtn");
            if (btn != null) btn.Text = "🔀 对称: 关";
        }
        else
        {
            var dialog = new AcceptDialog();
            dialog.Title = "选择对称轴";
            dialog.Size = new Vector2I(300, 150);
            var vbox = new VBoxContainer();
            dialog.AddChild(vbox);

            var vertBtn = new Button();
            vertBtn.Text = "竖轴对称 (左右镜像)";
            vertBtn.Pressed += () => { EnableSymmetry(SymmetryAxis.Vertical); dialog.QueueFree(); };
            vbox.AddChild(vertBtn);

            var horizBtn = new Button();
            horizBtn.Text = "横轴对称 (上下镜像)";
            horizBtn.Pressed += () => { EnableSymmetry(SymmetryAxis.Horizontal); dialog.QueueFree(); };
            vbox.AddChild(horizBtn);

            AddChild(dialog);
            dialog.PopupCentered();
        }
    }

    private void EnableSymmetry(SymmetryAxis axis)
    {
        IsSymmetryMode = true;
        CurrentSymmetryAxis = axis;
        UpdateStatusHint($"对称放置已开启: {axis} | 点击格子设置对称轴");
        var btn = helperPanel?.GetNodeOrNull<Button>("HelperVBox/SymmetryBtn");
        if (btn != null) btn.Text = $"🔀 对称: {axis}";
    }

    public void OnGridClickForSymmetry(Grids grid)
    {
        if (!IsSymmetryMode || grid == null) return;
        if (symmetryAxisLine < 0)
        {
            symmetryAxisLine = CurrentSymmetryAxis == SymmetryAxis.Vertical ? grid.GridIndex.X : grid.GridIndex.Y;
            UpdateStatusHint($"对称轴已设置在 {symmetryAxisLine}，现在放置单位会自动镜像");
            ShowSymmetryPreview();
        }
    }

    public Vector2I GetMirroredPosition(Vector2I original)
    {
        if (!IsSymmetryMode || symmetryAxisLine < 0) return original;
        if (CurrentSymmetryAxis == SymmetryAxis.Vertical)
        {
            int dist = original.X - symmetryAxisLine;
            return new Vector2I(symmetryAxisLine - dist, original.Y);
        }
        else
        {
            int dist = original.Y - symmetryAxisLine;
            return new Vector2I(original.X, symmetryAxisLine - dist);
        }
    }

    public void OnEntityPlacedWithSymmetry(Vector2I pos, Action<Vector2I> placeAction)
    {
        placeAction(pos);
        var mirrored = GetMirroredPosition(pos);
        if (mirrored != pos) placeAction(mirrored);
    }

    private void ShowSymmetryPreview()
    {
        ClearSymmetryPreview();
        if (gridManager?.map == null || symmetryAxisLine < 0) return;
        int w = gridManager.map.GetLength(0);
        int h = gridManager.map.GetLength(1);
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                bool onAxis = false;
                if (CurrentSymmetryAxis == SymmetryAxis.Vertical && x == symmetryAxisLine) onAxis = true;
                if (CurrentSymmetryAxis == SymmetryAxis.Horizontal && y == symmetryAxisLine) onAxis = true;
                if (onAxis)
                {
                    var g = gridManager.map[x, y];
                    if (g != null)
                    {
                        g.Modulate = new Color(1, 0, 1, 0.5f);
                        symmetryPreviewGrids.Add(g);
                    }
                }
            }
        }
    }

    private void ClearSymmetryPreview()
    {
        foreach (var g in symmetryPreviewGrids)
        {
            if (g != null && IsInstanceValid(g))
                g.Modulate = Colors.White;
        }
        symmetryPreviewGrids.Clear();
    }

    // ========== 撤销/前进系统 ==========
    public void PushAction(EditorActionRecord action)
    {
        if (currentHistoryIndex < actionHistory.Count - 1)
        {
            actionHistory.RemoveRange(currentHistoryIndex + 1, actionHistory.Count - currentHistoryIndex - 1);
        }
        action.timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        actionHistory.Add(action);
        if (actionHistory.Count > MAX_HISTORY)
        {
            actionHistory.RemoveAt(0);
        }
        else
        {
            currentHistoryIndex++;
        }
        UpdateHistoryLabel();
    }

    public void Undo()
    {
        if (currentHistoryIndex < 0) return;
        var action = actionHistory[currentHistoryIndex];
        UndoAction(action);
        currentHistoryIndex--;
        UpdateHistoryLabel();
        UpdateStatusHint($"已撤销: {action.actionType}");
    }

    public void Redo()
    {
        if (currentHistoryIndex >= actionHistory.Count - 1) return;
        currentHistoryIndex++;
        var action = actionHistory[currentHistoryIndex];
        RedoAction(action);
        UpdateHistoryLabel();
        UpdateStatusHint($"已前进: {action.actionType}");
    }

    private void UndoAction(EditorActionRecord action)
    {
        if (action.subActions != null && action.subActions.Count > 0)
        {
            foreach (var sub in action.subActions)
                UndoSingleAction(sub);
        }
        else
        {
            UndoSingleAction(action);
        }
    }

    private void RedoAction(EditorActionRecord action)
    {
        if (action.subActions != null && action.subActions.Count > 0)
        {
            foreach (var sub in action.subActions)
                RedoSingleAction(sub);
        }
        else
        {
            RedoSingleAction(action);
        }
    }

    private void UndoSingleAction(EditorActionRecord record)
    {
        if (gridManager?.map == null) return;
        int x = record.position.X, y = record.position.Y;
        if (x < 0 || x >= gridManager.map.GetLength(0) || y < 0 || y >= gridManager.map.GetLength(1))
            return;
        var g = gridManager.map[x, y];
        if (g == null) return;

        g.gridType = record.oldTerrain;
        g.UpdateTerrainIcon();

        if (record.oldUnitData == null && g.infantry != null) { g.infantry.QueueFree(); g.infantry = null; g.infantries.Clear(); }
        if (record.oldWeaponData == null && g.weapon != null) { g.weapon.QueueFree(); g.weapon = null; g.weapons.Clear(); }
        if (record.oldFacilityData == null && g.city != null) { g.city.QueueFree(); g.city = null; }

        if (record.oldUnitData != null) SpawnUnitFromData(g, record.oldUnitData);
        if (record.oldWeaponData != null) SpawnWeaponFromData(g, record.oldWeaponData);
        if (record.oldFacilityData != null) SpawnFacilityFromData(g, record.oldFacilityData);
    }

    private void RedoSingleAction(EditorActionRecord record)
    {
        if (gridManager?.map == null) return;
        int x = record.position.X, y = record.position.Y;
        if (x < 0 || x >= gridManager.map.GetLength(0) || y < 0 || y >= gridManager.map.GetLength(1))
            return;
        var g = gridManager.map[x, y];
        if (g == null) return;
        g.gridType = record.newTerrain;
        g.UpdateTerrainIcon();
    }

    private void UpdateHistoryLabel()
    {
        var label = helperPanel?.GetNodeOrNull<Label>("HelperVBox/HistoryLabel");
        if (label != null)
            label.Text = $"历史: {currentHistoryIndex + 1}/{actionHistory.Count}";
    }

    // ========== 贴图替换 ==========
    private void OpenTextureReplaceDialog()
    {
        var dialog = new AcceptDialog();
        dialog.Title = "🖼️ 贴图替换";
        dialog.Size = new Vector2I(450, 400);
        var vbox = new VBoxContainer();
        dialog.AddChild(vbox);

        var hintLabel = new Label();
        hintLabel.Text = "点击地图上的单位/兵器/设施来替换其贴图";
        vbox.AddChild(hintLabel);

        var fileBtn = new Button();
        fileBtn.Text = "选择贴图文件...";
        fileBtn.Pressed += () =>
        {
            var fileDialog = new FileDialog();
            fileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
            fileDialog.Access = FileDialog.AccessEnum.Filesystem;
            fileDialog.AddFilter("*.png", "PNG Images");
            fileDialog.AddFilter("*.jpg,*.jpeg", "JPEG Images");
            fileDialog.AddFilter("*.webp", "WEBP Images");
            fileDialog.AddFilter("*.gif", "GIF Images");
            fileDialog.FileSelected += (path) =>
            {
                StartTextureReplaceMode(path);
                fileDialog.QueueFree();
            };
            AddChild(fileDialog);
            fileDialog.PopupCentered(new Vector2I(600, 400));
        };
        vbox.AddChild(fileBtn);

        var typeLabel = new Label();
        typeLabel.Text = "或选择要替换的类型:";
        vbox.AddChild(typeLabel);

        var typeGrid = new GridContainer();
        typeGrid.Columns = 3;
        vbox.AddChild(typeGrid);

        string[] types = { "Infantry", "Weapon", "Facility", "Grid" };
        foreach (var t in types)
        {
            var btn = new Button();
            btn.Text = t;
            btn.Pressed += () => { StartTextureReplaceModeForType(t); dialog.QueueFree(); };
            typeGrid.AddChild(btn);
        }

        AddChild(dialog);
        dialog.PopupCentered();
    }

    public string pendingTexturePath = "";
    private string pendingTextureTargetType = "";

    private void StartTextureReplaceMode(string texturePath)
    {
        pendingTexturePath = texturePath;
        terrainEditor?.SetTextureApplyMode(texturePath, "");
        UpdateStatusHint("贴图替换: 点击目标单位/兵器/设施来应用贴图");
    }

    private void StartTextureReplaceModeForType(string targetType)
    {
        pendingTextureTargetType = targetType;
        terrainEditor?.SetTextureApplyMode("", targetType);
        UpdateStatusHint($"贴图替换模式: 选择贴图文件，然后点击地图上的 {targetType} 来替换");
    }

    public void OnEntityClickForTextureReplace(Node entity)
    {
        if (string.IsNullOrEmpty(pendingTexturePath)) return;

        // ✅ 使用TerrainEditor的纹理加载方法（支持GIF/PNG/JPG/WebP）
        var texture = terrainEditor?.LoadTextureFromPath(pendingTexturePath);
        if (texture == null)
        {
            UpdateStatusHint("贴图加载失败！");
            return;
        }

        bool isGif = pendingTexturePath.ToLower().EndsWith(".gif");
        List<Texture2D> gifFrames = null;
        if (isGif && terrainEditor != null)
            gifFrames = terrainEditor.TryExtractGifFrames(pendingTexturePath);

        var record = new EditorActionRecord { actionType = EditorActionType.ReplaceTexture };
        if (entity is Infantry inf)
        {
            record.oldUnitData = SerializeUnit(inf);
            var sprite = inf.GetNodeOrNull<Sprite2D>("Sprite2D");
            if (sprite != null) sprite.Texture = texture;
            // ✅ 支持AnimatedSprite2D单位（Mech/Flare等）
            if (inf is Mech mech && mech.animSprite != null)
                terrainEditor?.ApplyFramesToAnimSprite(mech.animSprite, gifFrames ?? new List<Texture2D>{texture}, inf.team == "Player2" ? "mech2" : "mech1");
            else if (inf is Flare flare && flare.animSprite != null)
                terrainEditor?.ApplyFramesToAnimSprite(flare.animSprite, gifFrames ?? new List<Texture2D>{texture}, inf.team == "Player2" ? "Flare2" : "Flare1");
        }
        else if (entity is Weapon wp)
        {
            record.oldWeaponData = SerializeWeapon(wp);
            var sprite = wp.GetNodeOrNull<Sprite2D>("Sprite2D");
            if (sprite != null) sprite.Texture = texture;
            else if (wp.animSprite != null)
                terrainEditor?.ApplyFramesToAnimSprite(wp.animSprite, gifFrames ?? new List<Texture2D>{texture}, "default");
        }
        else if (entity is Facility fac)
        {
            record.oldFacilityData = SerializeFacility(fac);
            var sprite = fac.GetNodeOrNull<Sprite2D>("Sprite2D");
            if (sprite != null) sprite.Texture = texture;
        }

        PushAction(record);
        UpdateStatusHint("贴图已替换！");
        pendingTexturePath = "";
    }

    // ========== 取消所有模式 ==========
    public void CancelAllModes()
    {
        IsShapeFillMode = false;
        IsAreaFillMode = false;
        IsTerrainFillMode = false;
        IsClearMapMode = false;
        IsSelectMode = false;
        CurrentSelectMode = SelectModeType.None;
        IsPasteMode = false;
        IsSymmetryMode = false;
        ClearShapePreview();
        ClearSymmetryPreview();
        ClearIrregularPreview();
        shapeAnchorPos = new Vector2I(-1, -1);
        selectStartPos = new Vector2I(-1, -1);
        symmetryAxisLine = -1;
        if (irregularSelectModeBtn != null)
            irregularSelectModeBtn.Text = "✏️ 不规则选区";
        var symBtn = helperPanel?.GetNodeOrNull<Button>("HelperVBox/SymmetryBtn");
        if (symBtn != null) symBtn.Text = "🔀 对称: 关";
    }

    // ========== 序列化辅助 ==========
    private SavedUnitData SerializeUnit(Infantry unit)
    {
        if (unit == null) return null;
        return new SavedUnitData
        {
            scenePath = unit.SceneFilePath,
            typeName = unit.GetType().Name,
            team = unit.team,
            fieldValues = new Dictionary<string, string>()
        };
    }

    private SavedWeaponData SerializeWeapon(Weapon weapon)
    {
        if (weapon == null) return null;
        var data = new SavedWeaponData
        {
            scenePath = weapon.SceneFilePath,
            typeName = weapon.GetType().Name,
            team = weapon.team,
            isMultiTile = weapon.isMultiTile,
            sizeX = weapon.size.X,
            sizeY = weapon.size.Y
        };
        var dirField = weapon.GetType().GetField("direction", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (dirField != null)
            data.direction = (int)dirField.GetValue(weapon);
        return data;
    }

    private SavedFacilityData SerializeFacility(Facility facility)
    {
        if (facility == null) return null;
        return new SavedFacilityData
        {
            scenePath = facility.SceneFilePath,
            typeName = facility.GetType().Name,
            team = facility.facilityTeam
        };
    }

    private void SpawnUnitFromData(Grids grid, SavedUnitData data)
    {
        if (string.IsNullOrEmpty(data.scenePath)) return;
        var scene = GD.Load<PackedScene>(data.scenePath);
        if (scene == null) return;
        var unit = scene.Instantiate<Infantry>();
        if (unit == null) return;
        unit.team = data.team;
        grid.AddChild(unit);
        grid.infantry = unit;
        grid.infantries.Add(unit);
        unit.GlobalPosition = grid.GlobalPosition;
    }

    private void SpawnWeaponFromData(Grids grid, SavedWeaponData data)
    {
        if (string.IsNullOrEmpty(data.scenePath)) return;
        var scene = GD.Load<PackedScene>(data.scenePath);
        if (scene == null) return;
        var weapon = scene.Instantiate<Weapon>();
        if (weapon == null) return;
        weapon.team = data.team;
        weapon.isMultiTile = data.isMultiTile;
        weapon.size = new Vector2I(data.sizeX, data.sizeY);
        if (weapon is BlackCannon bc && data.direction != 0) bc.direction = (BlackCannon.CannonDirection)data.direction;
        else if (weapon is DeathRay dr && data.direction != 0) dr.direction = (DeathRay.CannonDirection)data.direction;
        else if (weapon is LargeCannon lc && data.direction != 0) lc.direction = (LargeCannon.CannonDirection)data.direction;
        grid.AddChild(weapon);
        grid.weapon = weapon;
        grid.weapons.Add(weapon);
        weapon.GlobalPosition = grid.GlobalPosition;
    }

    private void SpawnFacilityFromData(Grids grid, SavedFacilityData data)
    {
        if (string.IsNullOrEmpty(data.scenePath)) return;
        var scene = GD.Load<PackedScene>(data.scenePath);
        if (scene == null) return;
        var facility = scene.Instantiate<Facility>();
        if (facility == null) return;
        facility.facilityTeam = data.team;
        grid.AddChild(facility);
        grid.city = facility;
        facility.GlobalPosition = grid.GlobalPosition;
    }
}
