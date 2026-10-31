// MapEditorHelper.cs - 编辑器大更新：图形填充、区域填充、地形填充、覆盖全图、选区复制粘贴（矩形+不规则）、对称放置
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MapEditorHelper : Node
{
    [ExportGroup("")]
    [Export] public GridManager gridManager;
    [Export] public GameManager gameManager;
    [Export] public TerrainEditor terrainEditor;
    [Export] public ExtraSave extraSave;

    // ========== UI 引用 ==========
    private Panel helperPanel;
    private Button helperToggleBtn;
    private bool isHelperPanelDragging = false;
    private Vector2 helperPanelDragOffset;
    private Label statusHintLabel;
    private Button irregularSelectModeBtn;

    // ✅ 操作按钮栏（确认/取消）—— 手机友好
    private HBoxContainer actionButtonBar;
    private Button confirmBtn;
    private Button cancelBtn;

    public bool IsPanelVisible => helperPanel?.Visible == true;

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
        ShowActionButtons(false);
        if (irregularSelectModeBtn != null)
            TranslateGroup.SetText(irregularSelectModeBtn, "✏️ 不规则选区");
        var symBtn = helperPanel?.GetNodeOrNull<Button>("HelperVBox/SymmetryBtn");
        if (symBtn != null) symBtn.Text = TranslateGroup.T("🔀 对称: 关");
    }
}
