// EditorSaveSlots.cs - 编辑器保存槽位界面：单位/兵器/设施/地形各10槽 + 收藏 + 战局5存档
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class EditorSaveSlots : Control
{
    [Export] public ExtraSave extraSave;
    [Export] public TerrainEditor terrainEditor;
    [Export] public GridManager gridManager;
    [Export] public GameManager gameManager;

    private Panel slotsPanel;
    private Button slotsToggleBtn;
    private TabContainer tabContainer;
    private bool isDraggingPanel = false;
    private Vector2 panelDragOffset;

    // ✅ 等待点击目标保存
    private int pendingSaveSlotIndex = -1;
    private string pendingSaveType = "";
    public bool IsWaitingForSaveTarget => pendingSaveSlotIndex >= 0;

    public override void _Ready()
    {
        AddToGroup("editor_save_slots");
        if (extraSave == null)
            extraSave = GetTree().GetFirstNodeInGroup("extra_save") as ExtraSave;
        if (terrainEditor == null)
            terrainEditor = GetTree().GetFirstNodeInGroup("terrain_editor") as TerrainEditor;
        if (gridManager == null)
            gridManager = GetTree().GetFirstNodeInGroup("grid_manager") as GridManager;
        if (gameManager == null)
            gameManager = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;

        CreateUI();
    }

    private void CreateUI()
    {
        slotsToggleBtn = new Button();
        slotsToggleBtn.Name = "SlotsToggleBtn";
        slotsToggleBtn.Text = "💾 保存槽";
        slotsToggleBtn.CustomMinimumSize = new Vector2(100, 40);
        slotsToggleBtn.Pressed += OnSlotsToggle;
        AddChild(slotsToggleBtn);

        slotsPanel = new Panel();
        slotsPanel.Name = "SlotsPanel";
        slotsPanel.CustomMinimumSize = new Vector2(500, 450);
        slotsPanel.Visible = false;
        AddChild(slotsPanel);

        var vbox = new VBoxContainer();
        vbox.Name = "SlotsVBox";
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        vbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        slotsPanel.AddChild(vbox);

        var titleBar = new HBoxContainer();
        titleBar.Name = "TitleBar";
        titleBar.CustomMinimumSize = new Vector2(0, 30);
        vbox.AddChild(titleBar);

        var titleLabel = new Label();
        titleLabel.Text = "=== Extra 保存槽 ===";
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleBar.AddChild(titleLabel);

        var closeBtn = new Button();
        closeBtn.Text = "X";
        closeBtn.CustomMinimumSize = new Vector2(30, 30);
        closeBtn.Pressed += () => slotsPanel.Visible = false;
        titleBar.AddChild(closeBtn);
        titleBar.GuiInput += OnTitleBarGuiInput;

        tabContainer = new TabContainer();
        tabContainer.Name = "TabContainer";
        tabContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        tabContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(tabContainer);

        CreateUnitSlotsTab();
        CreateWeaponSlotsTab();
        CreateFacilitySlotsTab();
        CreateGridSlotsTab();
        CreateMapSlotsTab();

        var vp = GetViewport().GetVisibleRect().Size;
        slotsPanel.Position = new Vector2(vp.X / 2 - 250, 50);
        slotsToggleBtn.Position = new Vector2(vp.X - 260, 10);
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton btn && btn.ButtonIndex == MouseButton.Left)
        {
            if (btn.Pressed)
            {
                isDraggingPanel = true;
                panelDragOffset = slotsPanel.GetGlobalMousePosition() - slotsPanel.GlobalPosition;
            }
            else
            {
                isDraggingPanel = false;
            }
        }
    }

    public override void _Process(double delta)
    {
        if (isDraggingPanel && slotsPanel != null)
        {
            slotsPanel.GlobalPosition = slotsPanel.GetGlobalMousePosition() - panelDragOffset;
        }
    }

    private void OnSlotsToggle()
    {
        slotsPanel.Visible = !slotsPanel.Visible;
        if (slotsPanel.Visible)
        {
            RefreshAllTabs();
            slotsPanel.Size = slotsPanel.CustomMinimumSize;
        }
    }

    private void CreateUnitSlotsTab()
    {
        var scroll = new ScrollContainer();
        scroll.Name = "UnitSlots";
        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(vbox);

        var hint = new Label();
        hint.Text = "点击地图上的单位来保存到槽位，或从槽位加载到地图";
        hint.AddThemeColorOverride("font_color", Colors.Gray);
        vbox.AddChild(hint);

        var grid = new GridContainer();
        grid.Name = "UnitSlotGrid";
        grid.Columns = 5;
        vbox.AddChild(grid);

        for (int i = 0; i < ExtraSave.MAX_SLOTS; i++)
            grid.AddChild(CreateSlotButton(i, "unit"));

        tabContainer.AddChild(scroll);
    }

    private void CreateWeaponSlotsTab()
    {
        var scroll = new ScrollContainer();
        scroll.Name = "WeaponSlots";
        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(vbox);
        var hint = new Label();
        hint.Text = "点击地图上的兵器来保存到槽位";
        hint.AddThemeColorOverride("font_color", Colors.Gray);
        vbox.AddChild(hint);
        var grid = new GridContainer();
        grid.Name = "WeaponSlotGrid";
        grid.Columns = 5;
        vbox.AddChild(grid);
        for (int i = 0; i < ExtraSave.MAX_SLOTS; i++)
            grid.AddChild(CreateSlotButton(i, "weapon"));
        tabContainer.AddChild(scroll);
    }

    private void CreateFacilitySlotsTab()
    {
        var scroll = new ScrollContainer();
        scroll.Name = "FacilitySlots";
        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(vbox);
        var hint = new Label();
        hint.Text = "点击地图上的设施来保存到槽位";
        hint.AddThemeColorOverride("font_color", Colors.Gray);
        vbox.AddChild(hint);
        var grid = new GridContainer();
        grid.Name = "FacilitySlotGrid";
        grid.Columns = 5;
        vbox.AddChild(grid);
        for (int i = 0; i < ExtraSave.MAX_SLOTS; i++)
            grid.AddChild(CreateSlotButton(i, "facility"));
        tabContainer.AddChild(scroll);
    }

    private void CreateGridSlotsTab()
    {
        var scroll = new ScrollContainer();
        scroll.Name = "GridSlots";
        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(vbox);
        var hint = new Label();
        hint.Text = "点击地图上的格子来保存地形到槽位";
        hint.AddThemeColorOverride("font_color", Colors.Gray);
        vbox.AddChild(hint);
        var grid = new GridContainer();
        grid.Name = "GridSlotGrid";
        grid.Columns = 5;
        vbox.AddChild(grid);
        for (int i = 0; i < ExtraSave.MAX_SLOTS; i++)
            grid.AddChild(CreateSlotButton(i, "grid"));
        tabContainer.AddChild(scroll);
    }

    private void CreateMapSlotsTab()
    {
        var scroll = new ScrollContainer();
        scroll.Name = "MapSlots";
        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(vbox);
        var hint = new Label();
        hint.Text = "保存/加载完整战局地图（包含所有单位、兵器、设施、地形）";
        hint.AddThemeColorOverride("font_color", Colors.Gray);
        vbox.AddChild(hint);
        var grid = new GridContainer();
        grid.Name = "MapSlotGrid";
        grid.Columns = 5;
        vbox.AddChild(grid);
        for (int i = 0; i < ExtraSave.MAX_MAP_SLOTS; i++)
            grid.AddChild(CreateMapSlotButton(i));
        tabContainer.AddChild(scroll);
    }

    private Button CreateSlotButton(int index, string type)
    {
        var btn = new Button();
        btn.Name = $"Slot_{type}_{index}";
        btn.CustomMinimumSize = new Vector2(90, 60);
        btn.ClipText = true;
        btn.Pressed += () => OnSlotClicked(index, type);
        return btn;
    }

    private Button CreateMapSlotButton(int index)
    {
        var btn = new Button();
        btn.Name = $"MapSlot_{index}";
        btn.CustomMinimumSize = new Vector2(90, 80);
        btn.ClipText = true;
        btn.Pressed += () => OnMapSlotClicked(index);
        return btn;
    }

    private void OnSlotClicked(int index, string type)
    {
        var dialog = new AcceptDialog();
        dialog.Title = $"槽位 [{index + 1}] - {type}";
        dialog.Size = new Vector2I(300, 200);
        var vbox = new VBoxContainer();
        dialog.AddChild(vbox);

        var saveBtn = new Button();
        saveBtn.Text = "💾 保存当前选中";
        saveBtn.Pressed += () => { SaveToSlot(index, type); dialog.QueueFree(); };
        vbox.AddChild(saveBtn);

        var loadBtn = new Button();
        loadBtn.Text = "📂 加载到地图";
        loadBtn.Pressed += () => { LoadFromSlot(index, type); dialog.QueueFree(); };
        vbox.AddChild(loadBtn);

        var favBtn = new Button();
        bool isFav = IsSlotFavorite(index, type);
        favBtn.Text = isFav ? "⭐ 取消收藏" : "☆ 收藏";
        favBtn.Pressed += () => { ToggleFavorite(index, type); dialog.QueueFree(); };
        vbox.AddChild(favBtn);

        var delBtn = new Button();
        delBtn.Text = "🗑️ 清空槽位";
        delBtn.AddThemeColorOverride("font_color", Colors.Red);
        delBtn.Pressed += () => { ClearSlot(index, type); dialog.QueueFree(); };
        vbox.AddChild(delBtn);

        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void OnMapSlotClicked(int index)
    {
        var dialog = new AcceptDialog();
        dialog.Title = $"战局地图槽位 [{index + 1}]";
        dialog.Size = new Vector2I(300, 200);
        var vbox = new VBoxContainer();
        dialog.AddChild(vbox);

        var saveBtn = new Button();
        saveBtn.Text = "💾 保存当前战局";
        saveBtn.Pressed += () => { SaveMapToSlot(index); dialog.QueueFree(); };
        vbox.AddChild(saveBtn);

        var loadBtn = new Button();
        loadBtn.Text = "📂 加载战局";
        loadBtn.Pressed += () => { LoadMapFromSlot(index); dialog.QueueFree(); };
        vbox.AddChild(loadBtn);

        var delBtn = new Button();
        delBtn.Text = "🗑️ 清空槽位";
        delBtn.AddThemeColorOverride("font_color", Colors.Red);
        delBtn.Pressed += () => { ClearMapSlot(index); dialog.QueueFree(); };
        vbox.AddChild(delBtn);

        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void SaveToSlot(int index, string type)
    {
        // ✅ 进入等待点击目标模式
        pendingSaveSlotIndex = index;
        pendingSaveType = type;
        var typeName = type switch { "unit" => "单位", "weapon" => "兵器", "facility" => "设施", "grid" => "地形", _ => type };
        if (terrainEditor != null)
            terrainEditor.spawnHintLabel.Text = $"💾 请点击地图上的{typeName}来保存到槽位 {index + 1}";
    }

    public void OnGridClickedForSave(Grids grid)
    {
        if (pendingSaveSlotIndex < 0 || extraSave == null) return;
        var data = extraSave.Data;
        int index = pendingSaveSlotIndex;
        string type = pendingSaveType;

        switch (type)
        {
            case "unit":
                if (grid.infantry != null)
                {
                    var saved = SerializeUnitToSlot(grid.infantry, index);
                    EnsureListSize(data.savedUnits, ExtraSave.MAX_SLOTS);
                    data.savedUnits[index] = saved;
                    if (terrainEditor != null)
                        terrainEditor.spawnHintLabel.Text = $"✅ 单位已保存到槽位 {index + 1}";
                }
                break;
            case "weapon":
                if (grid.weapon != null)
                {
                    var saved = SerializeWeaponToSlot(grid.weapon, index);
                    EnsureListSize(data.savedWeapons, ExtraSave.MAX_SLOTS);
                    data.savedWeapons[index] = saved;
                    if (terrainEditor != null)
                        terrainEditor.spawnHintLabel.Text = $"✅ 兵器已保存到槽位 {index + 1}";
                }
                break;
            case "facility":
                if (grid.city != null)
                {
                    var saved = SerializeFacilityToSlot(grid.city, index);
                    EnsureListSize(data.savedFacilities, ExtraSave.MAX_SLOTS);
                    data.savedFacilities[index] = saved;
                    if (terrainEditor != null)
                        terrainEditor.spawnHintLabel.Text = $"✅ 设施已保存到槽位 {index + 1}";
                }
                break;
            case "grid":
                {
                    var saved = SerializeGridToSlot(grid, index);
                    EnsureListSize(data.savedGridTerrains, ExtraSave.MAX_SLOTS);
                    data.savedGridTerrains[index] = saved;
                    if (terrainEditor != null)
                        terrainEditor.spawnHintLabel.Text = $"✅ 地形已保存到槽位 {index + 1}";
                }
                break;
        }

        pendingSaveSlotIndex = -1;
        pendingSaveType = "";
        extraSave.SaveAllData();
        RefreshTab(type);
    }

    private void LoadFromSlot(int index, string type)
    {
        if (extraSave == null || terrainEditor?.selectedGrid == null) return;
        var data = extraSave.Data;
        var grid = terrainEditor.selectedGrid;

        switch (type)
        {
            case "unit":
                if (index < data.savedUnits.Count && data.savedUnits[index] != null)
                    SpawnUnitFromSlot(data.savedUnits[index], grid);
                break;
            case "weapon":
                if (index < data.savedWeapons.Count && data.savedWeapons[index] != null)
                    SpawnWeaponFromSlot(data.savedWeapons[index], grid);
                break;
            case "facility":
                if (index < data.savedFacilities.Count && data.savedFacilities[index] != null)
                    SpawnFacilityFromSlot(data.savedFacilities[index], grid);
                break;
            case "grid":
                if (index < data.savedGridTerrains.Count && data.savedGridTerrains[index] != null)
                    ApplyGridTerrainFromSlot(data.savedGridTerrains[index], grid);
                break;
        }

        extraSave.SaveAllData();
        RefreshTab(type);
    }

    private void SaveMapToSlot(int index)
    {
        if (extraSave == null || gridManager?.map == null) return;

        var mapData = new SavedMapData
        {
            slotName = $"map_{index}",
            displayName = $"战局地图 {index + 1}",
            savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            width = gridManager.map.GetLength(0),
            height = gridManager.map.GetLength(1),
            fogOfWarEnabled = gameManager?.fogOfWarManager?.isFogOfWarEnabled ?? false,
            p1Funds = gameManager?.p1Funds ?? 0,
            p2Funds = gameManager?.p2Funds ?? 0
        };

        int w = mapData.width;
        int h = mapData.height;

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                mapData.terrainIds.Add((int)gridManager.map[x, y].gridType);

        foreach (var grid in gridManager.grids)
        {
            if (grid?.infantry != null)
                mapData.units.Add(SerializeUnitFull(grid.infantry, grid.GridIndex));
            if (grid?.weapon != null)
                mapData.weapons.Add(SerializeWeaponFull(grid.weapon, grid.GridIndex));
            if (grid?.city != null)
                mapData.facilities.Add(SerializeFacilityFull(grid.city, grid.GridIndex));
        }

        EnsureListSize(extraSave.Data.savedMaps, ExtraSave.MAX_MAP_SLOTS);
        extraSave.Data.savedMaps[index] = mapData;
        extraSave.SaveAllData();
        RefreshTab("map");
    }

    private void LoadMapFromSlot(int index)
    {
        if (extraSave == null) return;
        var data = extraSave.Data;
        if (index >= data.savedMaps.Count || data.savedMaps[index] == null) return;
        var mapData = data.savedMaps[index];
        GD.Print($"[EditorSaveSlots] 加载地图: {mapData.displayName} ({mapData.width}x{mapData.height})");
    }

    private void ClearSlot(int index, string type)
    {
        if (extraSave == null) return;
        var data = extraSave.Data;
        switch (type)
        {
            case "unit":
                if (index < data.savedUnits.Count) data.savedUnits[index] = null;
                data.favoriteUnitSlots.Remove($"unit_{index}");
                break;
            case "weapon":
                if (index < data.savedWeapons.Count) data.savedWeapons[index] = null;
                data.favoriteWeaponSlots.Remove($"weapon_{index}");
                break;
            case "facility":
                if (index < data.savedFacilities.Count) data.savedFacilities[index] = null;
                data.favoriteFacilitySlots.Remove($"facility_{index}");
                break;
            case "grid":
                if (index < data.savedGridTerrains.Count) data.savedGridTerrains[index] = null;
                data.favoriteGridSlots.Remove($"grid_{index}");
                break;
        }
        extraSave.SaveAllData();
        RefreshTab(type);
    }

    private void ClearMapSlot(int index)
    {
        if (extraSave == null) return;
        if (index < extraSave.Data.savedMaps.Count)
            extraSave.Data.savedMaps[index] = null;
        extraSave.SaveAllData();
        RefreshTab("map");
    }

    private bool IsSlotFavorite(int index, string type)
    {
        if (extraSave == null) return false;
        var key = $"{type}_{index}";
        return type switch
        {
            "unit" => extraSave.Data.favoriteUnitSlots.Contains(key),
            "weapon" => extraSave.Data.favoriteWeaponSlots.Contains(key),
            "facility" => extraSave.Data.favoriteFacilitySlots.Contains(key),
            "grid" => extraSave.Data.favoriteGridSlots.Contains(key),
            _ => false
        };
    }

    private void ToggleFavorite(int index, string type)
    {
        if (extraSave == null) return;
        var key = $"{type}_{index}";
        var list = type switch
        {
            "unit" => extraSave.Data.favoriteUnitSlots,
            "weapon" => extraSave.Data.favoriteWeaponSlots,
            "facility" => extraSave.Data.favoriteFacilitySlots,
            "grid" => extraSave.Data.favoriteGridSlots,
            _ => null
        };
        if (list == null) return;

        if (list.Contains(key))
        {
            list.Remove(key);
        }
        else
        {
            if (list.Count >= ExtraSave.MAX_FAVORITES)
            {
                GD.PrintErr("[EditorSaveSlots] 收藏已达上限");
                return;
            }
            list.Add(key);
        }
        extraSave.SaveAllData();
        RefreshTab(type);
    }

    private void RefreshAllTabs()
    {
        RefreshTab("unit");
        RefreshTab("weapon");
        RefreshTab("facility");
        RefreshTab("grid");
        RefreshTab("map");
    }

    private void RefreshTab(string type)
    {
        if (extraSave == null) return;
        var data = extraSave.Data;
        GridContainer grid = null;

        switch (type)
        {
            case "unit": grid = slotsPanel?.GetNodeOrNull<GridContainer>("SlotsVBox/TabContainer/UnitSlots/UnitSlotGrid"); break;
            case "weapon": grid = slotsPanel?.GetNodeOrNull<GridContainer>("SlotsVBox/TabContainer/WeaponSlots/WeaponSlotGrid"); break;
            case "facility": grid = slotsPanel?.GetNodeOrNull<GridContainer>("SlotsVBox/TabContainer/FacilitySlots/FacilitySlotGrid"); break;
            case "grid": grid = slotsPanel?.GetNodeOrNull<GridContainer>("SlotsVBox/TabContainer/GridSlots/GridSlotGrid"); break;
            case "map": grid = slotsPanel?.GetNodeOrNull<GridContainer>("SlotsVBox/TabContainer/MapSlots/MapSlotGrid"); break;
        }
        if (grid == null) return;

        for (int i = 0; i < grid.GetChildCount(); i++)
        {
            var btn = grid.GetChild(i) as Button;
            if (btn == null) continue;

            bool isFav = IsSlotFavorite(i, type);
            object slotData = null;
            switch (type)
            {
                case "unit": if (i < data.savedUnits.Count) slotData = data.savedUnits[i]; break;
                case "weapon": if (i < data.savedWeapons.Count) slotData = data.savedWeapons[i]; break;
                case "facility": if (i < data.savedFacilities.Count) slotData = data.savedFacilities[i]; break;
                case "grid": if (i < data.savedGridTerrains.Count) slotData = data.savedGridTerrains[i]; break;
                case "map": if (i < data.savedMaps.Count) slotData = data.savedMaps[i]; break;
            }

            string displayName = slotData == null ? $"[{i + 1}] 空" : GetSlotDisplayName(slotData);
            string timeStr = slotData == null ? "" : GetSlotTimeString(slotData);

            btn.Text = (isFav ? "⭐ " : "") + displayName + (string.IsNullOrEmpty(timeStr) ? "" : $"{timeStr}");
            btn.Modulate = slotData == null ? Colors.White : new Color(0.8f, 1f, 0.8f);
        }
    }

    private string GetSlotDisplayName(object slotData)
    {
        return slotData switch
        {
            SavedUnitData u => u.displayName,
            SavedWeaponData w => w.displayName,
            SavedFacilityData f => f.displayName,
            SavedGridTerrainData g => g.displayName,
            SavedMapData m => m.displayName,
            _ => "未知"
        };
    }

    private string GetSlotTimeString(object slotData)
    {
        long ts = slotData switch
        {
            SavedUnitData u => u.savedTimestamp,
            SavedWeaponData w => w.savedTimestamp,
            SavedFacilityData f => f.savedTimestamp,
            SavedGridTerrainData g => g.savedTimestamp,
            SavedMapData m => m.savedTimestamp,
            _ => 0
        };
        return ts > 0 ? DateTimeOffset.FromUnixTimeSeconds(ts).ToString("MM-dd HH:mm") : "";
    }

    private void EnsureListSize<T>(List<T> list, int size) where T : class
    {
        while (list.Count < size) list.Add(null);
    }

    private SavedUnitData SerializeUnitToSlot(Infantry unit, int slotIndex)
    {
        return new SavedUnitData
        {
            slotName = $"unit_{slotIndex}",
            displayName = unit.Name ?? unit.GetType().Name,
            scenePath = unit.SceneFilePath,
            typeName = unit.GetType().Name,
            team = unit.team,
            savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds()
        };
    }

    private SavedWeaponData SerializeWeaponToSlot(Weapon weapon, int slotIndex)
    {
        var data = new SavedWeaponData
        {
            slotName = $"weapon_{slotIndex}",
            displayName = weapon.Name ?? weapon.GetType().Name,
            scenePath = weapon.SceneFilePath,
            typeName = weapon.GetType().Name,
            team = weapon.team,
            savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            isMultiTile = weapon.isMultiTile,
            sizeX = weapon.size.X,
            sizeY = weapon.size.Y
        };
        var dirField = weapon.GetType().GetField("direction", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (dirField != null)
            data.direction = (int)dirField.GetValue(weapon);
        return data;
    }

    private SavedFacilityData SerializeFacilityToSlot(Facility facility, int slotIndex)
    {
        return new SavedFacilityData
        {
            slotName = $"facility_{slotIndex}",
            displayName = facility.Name ?? facility.GetType().Name,
            scenePath = facility.SceneFilePath,
            typeName = facility.GetType().Name,
            team = facility.facilityTeam,
            savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds()
        };
    }

    private SavedGridTerrainData SerializeGridToSlot(Grids grid, int slotIndex)
    {
        return new SavedGridTerrainData
        {
            slotName = $"grid_{slotIndex}",
            displayName = grid.gridType.ToString(),
            gridType = grid.gridType,
            canDestroyUnit = grid.canDestroyUnit,
            fixedDamagePerTurn = grid.fixedDamagePerTurn,
            fixedAttackPerTurn = grid.fixedAttackPerTurn,
            visionBonus = grid.visionBonus,
            savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds()
        };
    }

    private SavedUnitData SerializeUnitFull(Infantry unit, Vector2I pos)
    {
        var data = SerializeUnitToSlot(unit, 0);
        data.fieldValues["gridX"] = pos.X.ToString();
        data.fieldValues["gridY"] = pos.Y.ToString();
        return data;
    }

    private SavedWeaponData SerializeWeaponFull(Weapon weapon, Vector2I pos)
    {
        var data = SerializeWeaponToSlot(weapon, 0);
        data.fieldValues["gridX"] = pos.X.ToString();
        data.fieldValues["gridY"] = pos.Y.ToString();
        return data;
    }

    private SavedFacilityData SerializeFacilityFull(Facility facility, Vector2I pos)
    {
        var data = SerializeFacilityToSlot(facility, 0);
        data.fieldValues["gridX"] = pos.X.ToString();
        data.fieldValues["gridY"] = pos.Y.ToString();
        return data;
    }

    private void SpawnUnitFromSlot(SavedUnitData data, Grids grid)
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
        data.lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
    }

    private void SpawnWeaponFromSlot(SavedWeaponData data, Grids grid)
    {
        if (string.IsNullOrEmpty(data.scenePath)) return;
        var scene = GD.Load<PackedScene>(data.scenePath);
        if (scene == null) return;
        var weapon = scene.Instantiate<Weapon>();
        if (weapon == null) return;
        weapon.team = data.team;
        weapon.isMultiTile = data.isMultiTile;
        weapon.size.X = data.sizeX;
        weapon.size.Y = data.sizeY;
        grid.AddChild(weapon);
        grid.weapon = weapon;
        grid.weapons.Add(weapon);
        weapon.GlobalPosition = grid.GlobalPosition;
        data.lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
    }

    private void SpawnFacilityFromSlot(SavedFacilityData data, Grids grid)
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
        data.lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
    }

    private void ApplyGridTerrainFromSlot(SavedGridTerrainData data, Grids grid)
    {
        grid.gridType = data.gridType;
        grid.canDestroyUnit = data.canDestroyUnit;
        grid.fixedDamagePerTurn = data.fixedDamagePerTurn;
        grid.fixedAttackPerTurn = data.fixedAttackPerTurn;
        grid.visionBonus = data.visionBonus;
        grid.UpdateTerrainIcon();
        data.lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
    }
}
