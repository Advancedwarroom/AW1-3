using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public partial class CustomConfigUI : Control
{
    [Export] public ExtraSave extraSave;
    [Export] public TerrainEditor terrainEditor;
    [Export] public GameManager gameManager;

    private Panel mainPanel;
    private TabContainer tabContainer;

    // ✅ 拖拽字段
    private bool isDraggingMenu = false;
    private Vector2 dragOffset = Vector2.Zero;

    // ✅ 贴图应用模式
    private string pendingApplyTexturePath = "";
    private string pendingApplyTargetType = "";

    // 子面板
    private VBoxContainer moveTypePanel;
    private VBoxContainer terrainTypePanel;
    private VBoxContainer textureUploadPanel;
    private VBoxContainer saveSlotPanel;

    public override void _Ready()
    {
        AddToGroup("custom_config_ui");

        if (extraSave == null)
            extraSave = GetTree().GetFirstNodeInGroup("extra_save") as ExtraSave;
        if (terrainEditor == null)
            terrainEditor = GetTree().GetFirstNodeInGroup("terrain_editor") as TerrainEditor;
        if (gameManager == null)
            gameManager = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;

        CreateMainPanel();
        CreateTabs();
        CreateOpenButton(); // ✅ 创建编辑模式入口按钮
        Hide();
    }

    // ✅ 新增：编辑模式下的 Extra配置 打开按钮
    private Button openConfigBtn;

    private void CreateOpenButton()
    {
        openConfigBtn = new Button();
        openConfigBtn.Name = "OpenCustomConfigBtn";
        openConfigBtn.Text = "📦 Extra配置";
        openConfigBtn.CustomMinimumSize = new Vector2(120, 50);
        openConfigBtn.AddThemeFontSizeOverride("font_size", 14);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.3f, 0.5f, 0.7f, 0.9f);
        style.SetCornerRadiusAll(8);
        openConfigBtn.AddThemeStyleboxOverride("normal", style);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(0.4f, 0.6f, 0.8f, 0.95f);
        hoverStyle.SetCornerRadiusAll(8);
        openConfigBtn.AddThemeStyleboxOverride("hover", hoverStyle);

        openConfigBtn.AddThemeColorOverride("font_color", Colors.White);
        openConfigBtn.Pressed += () => {
            if (mainPanel?.Visible == true)
                Hide();
            else
                Show();
        };

        AddChild(openConfigBtn);

        var viewportSize = GetViewport().GetVisibleRect().Size;
        openConfigBtn.SetAnchorsPreset(LayoutPreset.TopLeft);
        openConfigBtn.Position = new Vector2(viewportSize.X - 290, viewportSize.Y - 130);
        openConfigBtn.Visible = false; // 初始隐藏，编辑模式时显示
    }

    /// <summary>控制Extra配置按钮的显示/隐藏（由TerrainEditor调用）</summary>
    public void SetOpenButtonVisible(bool visible)
    {
        if (openConfigBtn != null)
            openConfigBtn.Visible = visible;
    }

    private void CreateMainPanel()
    {
        mainPanel = new Panel();
        mainPanel.Name = "CustomConfigPanel";
        mainPanel.CustomMinimumSize = new Vector2(500, 700);
        mainPanel.SetAnchorsPreset(LayoutPreset.Center);
        mainPanel.ZIndex = 1500;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.08f, 0.1f, 0.15f, 0.98f);
        style.SetCornerRadiusAll(16);
        style.SetBorderWidthAll(3);
        style.BorderColor = new Color(0.5f, 0.7f, 0.9f);
        mainPanel.AddThemeStyleboxOverride("panel", style);

        AddChild(mainPanel);

        // ✅ 拖拽手柄
        var dragHandle = new Panel();
        dragHandle.Name = "DragHandle";
        dragHandle.CustomMinimumSize = new Vector2(500, 35);
        dragHandle.Position = new Vector2(0, 0);
        var handleStyle = new StyleBoxFlat();
        handleStyle.BgColor = new Color(0.3f, 0.3f, 0.4f, 0.5f);
        handleStyle.SetCornerRadiusAll(16);
        dragHandle.AddThemeStyleboxOverride("panel", handleStyle);

        var dragHint = new Label();
        dragHint.Text = "⋮⋮ 拖拽移动";
        dragHint.AddThemeFontSizeOverride("font_size", 10);
        dragHint.AddThemeColorOverride("font_color", new Color(0.6f, 0.7f, 0.8f));
        dragHint.Position = new Vector2(8, 2);
        dragHandle.AddChild(dragHint);

        mainPanel.AddChild(dragHandle);
        MakeMenuDraggable(mainPanel, dragHandle);

        // 标题栏
        var titleBar = new HBoxContainer();
        titleBar.Position = new Vector2(10, 40);
        titleBar.Size = new Vector2(480, 40);

        var title = new Label();
        title.Text = "⚙️ 自定义配置中心";
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", new Color(0.5f, 0.9f, 1f));
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titleBar.AddChild(title);

        var closeBtn = new Button();
        closeBtn.Text = "✕";
        closeBtn.CustomMinimumSize = new Vector2(36, 36);
        closeBtn.Pressed += Hide;
        titleBar.AddChild(closeBtn);

        mainPanel.AddChild(titleBar);

        // Tab容器
        tabContainer = new TabContainer();
        tabContainer.Position = new Vector2(10, 85);
        tabContainer.Size = new Vector2(480, 600);
        mainPanel.AddChild(tabContainer);
    }

    private void CreateTabs()
    {
        // 1. 自定义移动方式
        moveTypePanel = CreateMoveTypeTab();
        tabContainer.AddChild(moveTypePanel);
        tabContainer.SetTabTitle(0, "🚶 移动方式");

        // 2. 自定义地形
        terrainTypePanel = CreateTerrainTypeTab();
        tabContainer.AddChild(terrainTypePanel);
        tabContainer.SetTabTitle(1, "🗺️ 地形种类");

        // 3. 贴图上传
        textureUploadPanel = CreateTextureUploadTab();
        tabContainer.AddChild(textureUploadPanel);
        tabContainer.SetTabTitle(2, "🎨 贴图上传");

        // 4. 保存槽位
        saveSlotPanel = CreateSaveSlotTab();
        tabContainer.AddChild(saveSlotPanel);
        tabContainer.SetTabTitle(3, "💾 保存/加载");
    }

    // ==================== 自定义移动方式Tab ====================

    private VBoxContainer CreateMoveTypeTab()
    {
        var panel = new VBoxContainer();
        panel.AddThemeConstantOverride("separation", 10);
        panel.AddThemeConstantOverride("margin_left", 10);
        panel.AddThemeConstantOverride("margin_right", 10);
        panel.AddThemeConstantOverride("margin_top", 10);
        panel.AddThemeConstantOverride("margin_bottom", 10);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;

        var container = new VBoxContainer();
        container.AddThemeConstantOverride("separation", 8);

        // 新建移动方式按钮
        var newBtn = new Button();
        newBtn.Text = "➕ 新建自定义移动方式";
        newBtn.CustomMinimumSize = new Vector2(0, 40);
        newBtn.AddThemeFontSizeOverride("font_size", 14);
        var newStyle = new StyleBoxFlat();
        newStyle.BgColor = new Color(0.2f, 0.5f, 0.3f, 0.9f);
        newStyle.SetCornerRadiusAll(8);
        newBtn.AddThemeStyleboxOverride("normal", newStyle);
        newBtn.Pressed += () => ShowNewMoveTypeDialog();
        container.AddChild(newBtn);

        // 分隔线
        container.AddChild(new HSeparator());

        // 现有移动方式列表
        var listLabel = new Label();
        listLabel.Text = "=== 现有移动方式 ===";
        listLabel.AddThemeFontSizeOverride("font_size", 14);
        listLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        container.AddChild(listLabel);

        // 内置移动方式（只读显示）
        foreach (var moveType in ExtraSave.BuiltInMoveTypes)
        {
            var row = CreateMoveTypeRow(moveType, true);
            container.AddChild(row);
        }

        // 自定义移动方式
        var customLabel = new Label();
        customLabel.Text = "=== 自定义移动方式 ===";
        customLabel.AddThemeFontSizeOverride("font_size", 14);
        customLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        container.AddChild(customLabel);

        // 动态刷新自定义列表
        var customListContainer = new VBoxContainer();
        customListContainer.Name = "CustomMoveTypeList";
        customListContainer.AddThemeConstantOverride("separation", 6);
        container.AddChild(customListContainer);

        scroll.AddChild(container);
        panel.AddChild(scroll);

        return panel;
    }

    private HBoxContainer CreateMoveTypeRow(string moveTypeId, bool isBuiltIn)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);
        hbox.CustomMinimumSize = new Vector2(0, 36);

        var nameLabel = new Label();
        nameLabel.Text = moveTypeId;
        nameLabel.CustomMinimumSize = new Vector2(150, 28);
        nameLabel.AddThemeFontSizeOverride("font_size", 12);
        nameLabel.AddThemeColorOverride("font_color", isBuiltIn ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.4f, 1f, 0.4f));
        hbox.AddChild(nameLabel);

        var infoLabel = new Label();
        infoLabel.Text = isBuiltIn ? "[内置]" : "[自定义]";
        infoLabel.AddThemeFontSizeOverride("font_size", 11);
        infoLabel.AddThemeColorOverride("font_color", isBuiltIn ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.4f, 1f, 0.4f));
        hbox.AddChild(infoLabel);

        if (!isBuiltIn)
        {
            var editBtn = new Button();
            editBtn.Text = "编辑";
            editBtn.CustomMinimumSize = new Vector2(60, 28);
            editBtn.AddThemeFontSizeOverride("font_size", 11);
            editBtn.Pressed += () => ShowEditMoveTypeDialog(moveTypeId);
            hbox.AddChild(editBtn);

            var delBtn = new Button();
            delBtn.Text = "删除";
            delBtn.CustomMinimumSize = new Vector2(60, 28);
            delBtn.AddThemeFontSizeOverride("font_size", 11);
            delBtn.AddThemeColorOverride("font_color", new Color(1, 0.4f, 0.4f));
            delBtn.Pressed += () => {
                extraSave?.RemoveCustomMoveType(moveTypeId);
                RefreshCustomMoveTypeList();
            };
            hbox.AddChild(delBtn);

            // 贴图按钮
            var texBtn = new Button();
            texBtn.Text = "🎨";
            texBtn.CustomMinimumSize = new Vector2(32, 28);
            texBtn.AddThemeFontSizeOverride("font_size", 10);
            texBtn.Pressed += () => ShowTextureApplyDialog("移动方式", moveTypeId);
            hbox.AddChild(texBtn);
        }
        else
        {
            var viewBtn = new Button();
            viewBtn.Text = "查看消耗";
            viewBtn.CustomMinimumSize = new Vector2(80, 28);
            viewBtn.AddThemeFontSizeOverride("font_size", 11);
            viewBtn.Pressed += () => ShowBuiltInMoveTypeCosts(moveTypeId);
            hbox.AddChild(viewBtn);
        }

        return hbox;
    }

    private void ShowNewMoveTypeDialog()
    {
        var dialog = new Panel();
        dialog.CustomMinimumSize = new Vector2(400, 500);
        dialog.SetAnchorsPreset(LayoutPreset.Center);
        dialog.ZIndex = 1600;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.1f, 0.12f, 0.18f, 0.98f);
        style.SetCornerRadiusAll(12);
        style.SetBorderWidthAll(2);
        style.BorderColor = new Color(0.4f, 0.7f, 0.9f);
        dialog.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("margin_left", 15);
        vbox.AddThemeConstantOverride("margin_right", 15);
        vbox.AddThemeConstantOverride("margin_top", 15);
        vbox.AddThemeConstantOverride("margin_bottom", 15);
        vbox.AddThemeConstantOverride("separation", 10);

        var title = new Label();
        title.Text = "➕ 新建自定义移动方式";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        vbox.AddChild(title);

        // ID输入
        var idHBox = new HBoxContainer();
        idHBox.AddThemeConstantOverride("separation", 8);
        var idLabel = new Label();
        idLabel.Text = "ID (英文):";
        idLabel.CustomMinimumSize = new Vector2(80, 28);
        idHBox.AddChild(idLabel);
        var idEdit = new LineEdit();
        idEdit.Name = "MoveTypeIdEdit";
        idEdit.CustomMinimumSize = new Vector2(200, 28);
        idEdit.PlaceholderText = "如: MyHover";
        idHBox.AddChild(idEdit);
        vbox.AddChild(idHBox);

        // 显示名称
        var nameHBox = new HBoxContainer();
        nameHBox.AddThemeConstantOverride("separation", 8);
        var nameLabel = new Label();
        nameLabel.Text = "显示名称:";
        nameLabel.CustomMinimumSize = new Vector2(80, 28);
        nameHBox.AddChild(nameLabel);
        var nameEdit = new LineEdit();
        nameEdit.Name = "MoveTypeNameEdit";
        nameEdit.CustomMinimumSize = new Vector2(200, 28);
        nameEdit.PlaceholderText = "如: 我的气垫";
        nameHBox.AddChild(nameEdit);
        vbox.AddChild(nameHBox);

        // 地形消耗配置
        var costLabel = new Label();
        costLabel.Text = "=== 地形移动消耗配置 ===";
        costLabel.AddThemeFontSizeOverride("font_size", 14);
        costLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        vbox.AddChild(costLabel);

        var hintLabel = new Label();
        hintLabel.Text = "未配置 = 999 (不可移动)";
        hintLabel.AddThemeFontSizeOverride("font_size", 10);
        hintLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        vbox.AddChild(hintLabel);

        var costScroll = new ScrollContainer();
        costScroll.CustomMinimumSize = new Vector2(0, 250);

        var costContainer = new VBoxContainer();
        costContainer.Name = "CostContainer";
        costContainer.AddThemeConstantOverride("separation", 4);

        // 内置地形
        foreach (var terrain in ExtraSave.BuiltInTerrainTypes)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var tLabel = new Label();
            tLabel.Text = terrain;
            tLabel.CustomMinimumSize = new Vector2(120, 24);
            tLabel.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(tLabel);

            var spinBox = new SpinBox();
            spinBox.Name = $"Cost_{terrain}";
            spinBox.MinValue = 0;
            spinBox.MaxValue = 999;
            spinBox.Value = 999;
            spinBox.CustomMinimumSize = new Vector2(80, 24);
            spinBox.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(spinBox);

            costContainer.AddChild(row);
        }

        // 自定义地形
        if (extraSave?.CustomTerrainTypes != null)
        {
            foreach (var kvp in extraSave.CustomTerrainTypes)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);

                var tLabel = new Label();
                tLabel.Text = $"{kvp.Value.displayName}({kvp.Key})";
                tLabel.CustomMinimumSize = new Vector2(120, 24);
                tLabel.AddThemeFontSizeOverride("font_size", 11);
                tLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
                row.AddChild(tLabel);

                var spinBox = new SpinBox();
                spinBox.Name = $"Cost_{kvp.Key}";
                spinBox.MinValue = 0;
                spinBox.MaxValue = 999;
                spinBox.Value = 999;
                spinBox.CustomMinimumSize = new Vector2(80, 24);
                spinBox.AddThemeFontSizeOverride("font_size", 11);
                row.AddChild(spinBox);

                costContainer.AddChild(row);
            }
        }

        costScroll.AddChild(costContainer);
        vbox.AddChild(costScroll);

        // 按钮
        var btnHBox = new HBoxContainer();
        btnHBox.AddThemeConstantOverride("separation", 10);

        var confirmBtn = new Button();
        confirmBtn.Text = "✅ 确认创建";
        confirmBtn.CustomMinimumSize = new Vector2(120, 36);
        confirmBtn.AddThemeFontSizeOverride("font_size", 13);
        var confirmStyle = new StyleBoxFlat();
        confirmStyle.BgColor = new Color(0.2f, 0.5f, 0.3f, 0.9f);
        confirmStyle.SetCornerRadiusAll(8);
        confirmBtn.AddThemeStyleboxOverride("normal", confirmStyle);
        confirmBtn.Pressed += () => {
            string id = idEdit.Text.Trim();
            string displayName = nameEdit.Text.Trim();
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(displayName))
            {
                ShowToast("ID和名称不能为空");
                return;
            }

            var costs = new Dictionary<string, int>();
            foreach (var child in costContainer.GetChildren())
            {
                if (child is HBoxContainer row)
                {
                    var spinBox = row.GetChildren().OfType<SpinBox>().FirstOrDefault();
                    if (spinBox != null)
                    {
                        string terrainId = spinBox.Name.ToString().Replace("Cost_", "");
                        costs[terrainId] = (int)spinBox.Value;
                    }
                }
            }

            extraSave?.AddCustomMoveType(id, displayName, costs);
            dialog.QueueFree();
            RefreshCustomMoveTypeList();
            ShowToast("自定义移动方式已创建！");
        };
        btnHBox.AddChild(confirmBtn);

        var cancelBtn = new Button();
        cancelBtn.Text = "❌ 取消";
        cancelBtn.CustomMinimumSize = new Vector2(120, 36);
        cancelBtn.AddThemeFontSizeOverride("font_size", 13);
        cancelBtn.Pressed += () => dialog.QueueFree();
        btnHBox.AddChild(cancelBtn);

        vbox.AddChild(btnHBox);
        dialog.AddChild(vbox);
        AddChild(dialog);
        MakeMenuDraggable(dialog); // ✅ 使对话框可拖拽
    }

    private void RefreshCustomMoveTypeList()
    {
        var list = moveTypePanel?.GetNodeOrNull<VBoxContainer>("CustomMoveTypeList");
        if (list == null) return;

        foreach (var child in list.GetChildren())
            child.QueueFree();

        if (extraSave?.CustomMoveTypes == null || extraSave.CustomMoveTypes.Count == 0)
        {
            var emptyLabel = new Label();
            emptyLabel.Text = "暂无自定义移动方式";
            emptyLabel.AddThemeFontSizeOverride("font_size", 12);
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            list.AddChild(emptyLabel);
            return;
        }

        foreach (var kvp in extraSave.CustomMoveTypes)
        {
            var row = CreateMoveTypeRow(kvp.Key, false);
            list.AddChild(row);
        }
    }
        // ==================== 自定义地形Tab ====================

    private VBoxContainer CreateTerrainTypeTab()
    {
        var panel = new VBoxContainer();
        panel.AddThemeConstantOverride("separation", 10);
        panel.AddThemeConstantOverride("margin_left", 10);
        panel.AddThemeConstantOverride("margin_right", 10);
        panel.AddThemeConstantOverride("margin_top", 10);
        panel.AddThemeConstantOverride("margin_bottom", 10);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;

        var container = new VBoxContainer();
        container.AddThemeConstantOverride("separation", 8);

        // 新建地形按钮
        var newBtn = new Button();
        newBtn.Text = "➕ 新建自定义地形";
        newBtn.CustomMinimumSize = new Vector2(0, 40);
        newBtn.AddThemeFontSizeOverride("font_size", 14);
        var newStyle = new StyleBoxFlat();
        newStyle.BgColor = new Color(0.2f, 0.5f, 0.3f, 0.9f);
        newStyle.SetCornerRadiusAll(8);
        newBtn.AddThemeStyleboxOverride("normal", newStyle);
        newBtn.Pressed += () => ShowNewTerrainTypeDialog();
        container.AddChild(newBtn);

        container.AddChild(new HSeparator());

        // 现有地形列表
        var listLabel = new Label();
        listLabel.Text = "=== 现有地形种类 ===";
        listLabel.AddThemeFontSizeOverride("font_size", 14);
        listLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        container.AddChild(listLabel);

        // 内置地形（只读）
        foreach (var terrain in ExtraSave.BuiltInTerrainTypes)
        {
            var row = CreateTerrainTypeRow(terrain, true);
            container.AddChild(row);
        }

        // 自定义地形
        var customLabel = new Label();
        customLabel.Text = "=== 自定义地形 ===";
        customLabel.AddThemeFontSizeOverride("font_size", 14);
        customLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        container.AddChild(customLabel);

        var customListContainer = new VBoxContainer();
        customListContainer.Name = "CustomTerrainTypeList";
        customListContainer.AddThemeConstantOverride("separation", 6);
        container.AddChild(customListContainer);

        scroll.AddChild(container);
        panel.AddChild(scroll);

        return panel;
    }

    private HBoxContainer CreateTerrainTypeRow(string terrainId, bool isBuiltIn)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);
        hbox.CustomMinimumSize = new Vector2(0, 36);

        string displayName = terrainId;
        Color nameColor = isBuiltIn ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.4f, 1f, 0.4f);

        if (!isBuiltIn && extraSave?.CustomTerrainTypes.TryGetValue(terrainId, out var customTerrain) == true)
        {
            displayName = customTerrain.displayName;
        }

        var nameLabel = new Label();
        nameLabel.Text = displayName;
        nameLabel.CustomMinimumSize = new Vector2(150, 28);
        nameLabel.AddThemeFontSizeOverride("font_size", 12);
        nameLabel.AddThemeColorOverride("font_color", nameColor);
        hbox.AddChild(nameLabel);

        var infoLabel = new Label();
        infoLabel.Text = isBuiltIn ? "[内置]" : "[自定义]";
        infoLabel.AddThemeFontSizeOverride("font_size", 11);
        infoLabel.AddThemeColorOverride("font_color", isBuiltIn ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.4f, 1f, 0.4f));
        hbox.AddChild(infoLabel);

        if (!isBuiltIn)
        {
            var editBtn = new Button();
            editBtn.Text = "编辑";
            editBtn.CustomMinimumSize = new Vector2(60, 28);
            editBtn.AddThemeFontSizeOverride("font_size", 11);
            editBtn.Pressed += () => ShowEditTerrainTypeDialog(terrainId);
            hbox.AddChild(editBtn);

            var delBtn = new Button();
            delBtn.Text = "删除";
            delBtn.CustomMinimumSize = new Vector2(60, 28);
            delBtn.AddThemeFontSizeOverride("font_size", 11);
            delBtn.AddThemeColorOverride("font_color", new Color(1, 0.4f, 0.4f));
            delBtn.Pressed += () => {
                extraSave?.RemoveCustomTerrainType(terrainId);
                RefreshCustomTerrainTypeList();
            };
            hbox.AddChild(delBtn);

            // 贴图按钮
            var texBtn = new Button();
            texBtn.Text = "🎨";
            texBtn.CustomMinimumSize = new Vector2(32, 28);
            texBtn.AddThemeFontSizeOverride("font_size", 10);
            texBtn.Pressed += () => ShowTextureApplyDialog("地形", terrainId);
            hbox.AddChild(texBtn);
        }

        return hbox;
    }

    private void ShowNewTerrainTypeDialog()
    {
        var dialog = new Panel();
        dialog.CustomMinimumSize = new Vector2(400, 550);
        dialog.SetAnchorsPreset(LayoutPreset.Center);
        dialog.ZIndex = 1600;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.1f, 0.12f, 0.18f, 0.98f);
        style.SetCornerRadiusAll(12);
        style.SetBorderWidthAll(2);
        style.BorderColor = new Color(0.4f, 0.7f, 0.9f);
        dialog.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("margin_left", 15);
        vbox.AddThemeConstantOverride("margin_right", 15);
        vbox.AddThemeConstantOverride("margin_top", 15);
        vbox.AddThemeConstantOverride("margin_bottom", 15);
        vbox.AddThemeConstantOverride("separation", 10);

        var title = new Label();
        title.Text = "➕ 新建自定义地形";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        vbox.AddChild(title);

        // ID
        var idHBox = new HBoxContainer();
        idHBox.AddThemeConstantOverride("separation", 8);
        var idLabel = new Label();
        idLabel.Text = "ID (英文):";
        idLabel.CustomMinimumSize = new Vector2(80, 28);
        idHBox.AddChild(idLabel);
        var idEdit = new LineEdit();
        idEdit.CustomMinimumSize = new Vector2(200, 28);
        idEdit.PlaceholderText = "如: MyForest";
        idHBox.AddChild(idEdit);
        vbox.AddChild(idHBox);

        // 显示名称
        var nameHBox = new HBoxContainer();
        nameHBox.AddThemeConstantOverride("separation", 8);
        var nameLabel = new Label();
        nameLabel.Text = "显示名称:";
        nameLabel.CustomMinimumSize = new Vector2(80, 28);
        nameHBox.AddChild(nameLabel);
        var nameEdit = new LineEdit();
        nameEdit.CustomMinimumSize = new Vector2(200, 28);
        nameEdit.PlaceholderText = "如: 我的森林";
        nameHBox.AddChild(nameEdit);
        vbox.AddChild(nameHBox);

        // 防御加成
        var defHBox = new HBoxContainer();
        defHBox.AddThemeConstantOverride("separation", 8);
        var defLabel = new Label();
        defLabel.Text = "防御加成:";
        defLabel.CustomMinimumSize = new Vector2(80, 28);
        defHBox.AddChild(defLabel);
        var defSpin = new SpinBox();
        defSpin.MinValue = 0;
        defSpin.MaxValue = 1;
        defSpin.Step = 0.05;
        defSpin.Value = 0.1;
        defSpin.CustomMinimumSize = new Vector2(100, 28);
        defHBox.AddChild(defSpin);
        vbox.AddChild(defHBox);

        // 颜色
        var colorHBox = new HBoxContainer();
        colorHBox.AddThemeConstantOverride("separation", 8);
        var colorLabel = new Label();
        colorLabel.Text = "地形颜色:";
        colorLabel.CustomMinimumSize = new Vector2(80, 28);
        colorHBox.AddChild(colorLabel);
        var colorPicker = new ColorPickerButton();
        colorPicker.Color = new Color(0.4f, 0.7f, 0.3f);
        colorPicker.CustomMinimumSize = new Vector2(80, 28);
        colorHBox.AddChild(colorPicker);
        vbox.AddChild(colorHBox);

        // 移动方式消耗配置
        var costLabel = new Label();
        costLabel.Text = "=== 移动方式消耗配置 ===";
        costLabel.AddThemeFontSizeOverride("font_size", 14);
        costLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        vbox.AddChild(costLabel);

        var hintLabel = new Label();
        hintLabel.Text = "未配置 = 999 (不可移动)";
        hintLabel.AddThemeFontSizeOverride("font_size", 10);
        hintLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        vbox.AddChild(hintLabel);

        var costScroll = new ScrollContainer();
        costScroll.CustomMinimumSize = new Vector2(0, 200);

        var costContainer = new VBoxContainer();
        costContainer.AddThemeConstantOverride("separation", 4);

        // 内置移动方式
        foreach (var moveType in ExtraSave.BuiltInMoveTypes)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var mLabel = new Label();
            mLabel.Text = moveType;
            mLabel.CustomMinimumSize = new Vector2(120, 24);
            mLabel.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(mLabel);

            var spinBox = new SpinBox();
            spinBox.Name = $"Cost_{moveType}";
            spinBox.MinValue = 0;
            spinBox.MaxValue = 999;
            spinBox.Value = 999;
            spinBox.CustomMinimumSize = new Vector2(80, 24);
            spinBox.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(spinBox);

            costContainer.AddChild(row);
        }

        // 自定义移动方式
        if (extraSave?.CustomMoveTypes != null)
        {
            foreach (var kvp in extraSave.CustomMoveTypes)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);

                var mLabel = new Label();
                mLabel.Text = $"{kvp.Value.displayName}({kvp.Key})";
                mLabel.CustomMinimumSize = new Vector2(120, 24);
                mLabel.AddThemeFontSizeOverride("font_size", 11);
                mLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
                row.AddChild(mLabel);

                var spinBox = new SpinBox();
                spinBox.Name = $"Cost_{kvp.Key}";
                spinBox.MinValue = 0;
                spinBox.MaxValue = 999;
                spinBox.Value = 999;
                spinBox.CustomMinimumSize = new Vector2(80, 24);
                spinBox.AddThemeFontSizeOverride("font_size", 11);
                row.AddChild(spinBox);

                costContainer.AddChild(row);
            }
        }

        costScroll.AddChild(costContainer);
        vbox.AddChild(costScroll);

        // 按钮
        var btnHBox = new HBoxContainer();
        btnHBox.AddThemeConstantOverride("separation", 10);

        var confirmBtn = new Button();
        confirmBtn.Text = "✅ 确认创建";
        confirmBtn.CustomMinimumSize = new Vector2(120, 36);
        confirmBtn.AddThemeFontSizeOverride("font_size", 13);
        var confirmStyle = new StyleBoxFlat();
        confirmStyle.BgColor = new Color(0.2f, 0.5f, 0.3f, 0.9f);
        confirmStyle.SetCornerRadiusAll(8);
        confirmBtn.AddThemeStyleboxOverride("normal", confirmStyle);
        confirmBtn.Pressed += () => {
            string id = idEdit.Text.Trim();
            string displayName = nameEdit.Text.Trim();
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(displayName))
            {
                ShowToast("ID和名称不能为空");
                return;
            }

            var costs = new Dictionary<string, int>();
            foreach (var child in costContainer.GetChildren())
            {
                if (child is HBoxContainer row)
                {
                    var spinBox = row.GetChildren().OfType<SpinBox>().FirstOrDefault();
                    if (spinBox != null)
                    {
                        string moveTypeId = spinBox.Name.ToString().Replace("Cost_", "");
                        costs[moveTypeId] = (int)spinBox.Value;
                    }
                }
            }

            extraSave?.AddCustomTerrainType(id, displayName, costs, (float)defSpin.Value, colorPicker.Color);
            dialog.QueueFree();
            RefreshCustomTerrainTypeList();
            ShowToast("自定义地形已创建！");
        };
        btnHBox.AddChild(confirmBtn);

        var cancelBtn = new Button();
        cancelBtn.Text = "❌ 取消";
        cancelBtn.CustomMinimumSize = new Vector2(120, 36);
        cancelBtn.AddThemeFontSizeOverride("font_size", 13);
        cancelBtn.Pressed += () => dialog.QueueFree();
        btnHBox.AddChild(cancelBtn);

        vbox.AddChild(btnHBox);
        dialog.AddChild(vbox);
        AddChild(dialog);
        MakeMenuDraggable(dialog); // ✅ 使对话框可拖拽
    }

    private void RefreshCustomTerrainTypeList()
    {
        var list = terrainTypePanel?.GetNodeOrNull<VBoxContainer>("CustomTerrainTypeList");
        if (list == null) return;

        foreach (var child in list.GetChildren())
            child.QueueFree();

        if (extraSave?.CustomTerrainTypes == null || extraSave.CustomTerrainTypes.Count == 0)
        {
            var emptyLabel = new Label();
            emptyLabel.Text = "暂无自定义地形";
            emptyLabel.AddThemeFontSizeOverride("font_size", 12);
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            list.AddChild(emptyLabel);
            return;
        }

        foreach (var kvp in extraSave.CustomTerrainTypes)
        {
            var row = CreateTerrainTypeRow(kvp.Key, false);
            list.AddChild(row);
        }
    }
        // ==================== 贴图上传Tab ====================

    private VBoxContainer CreateTextureUploadTab()
    {
        var panel = new VBoxContainer();
        panel.AddThemeConstantOverride("separation", 10);
        panel.AddThemeConstantOverride("margin_left", 10);
        panel.AddThemeConstantOverride("margin_right", 10);
        panel.AddThemeConstantOverride("margin_top", 10);
        panel.AddThemeConstantOverride("margin_bottom", 10);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;

        var container = new VBoxContainer();
        container.AddThemeConstantOverride("separation", 10);

        // 权限请求按钮
        var permBtn = new Button();
        permBtn.Text = "📱 请求相册访问权限";
        permBtn.CustomMinimumSize = new Vector2(0, 40);
        permBtn.AddThemeFontSizeOverride("font_size", 14);
        var permStyle = new StyleBoxFlat();
        permStyle.BgColor = new Color(0.3f, 0.4f, 0.6f, 0.9f);
        permStyle.SetCornerRadiusAll(8);
        permBtn.AddThemeStyleboxOverride("normal", permStyle);
        permBtn.Pressed += () => {
            ExtraSave.RequestGalleryPermission();
            ShowToast("权限请求已发送");
        };
        container.AddChild(permBtn);

        var permStatus = new Label();
        permStatus.Text = $"当前权限状态: {(ExtraSave.HasGalleryPermission() ? "✅ 已授权" : "❌ 未授权")}";
        permStatus.AddThemeFontSizeOverride("font_size", 12);
        permStatus.AddThemeColorOverride("font_color", ExtraSave.HasGalleryPermission() ? new Color(0.3f, 1, 0.3f) : new Color(1, 0.3f, 0.3f));
        container.AddChild(permStatus);

        container.AddChild(new HSeparator());

        // 上传说明
        var infoLabel = new Label();
        infoLabel.Text = "📋 上传要求:\n• 尺寸: 32×32 像素\n• 格式: PNG / JPG / GIF\n• 非标准尺寸将被拒绝";
        infoLabel.AddThemeFontSizeOverride("font_size", 12);
        infoLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.6f));
        container.AddChild(infoLabel);

        // 分类上传
        var categories = new[] { "单位", "兵器", "设施", "地形", "移动方式" };
        foreach (var category in categories)
        {
            var catBtn = new Button();
            catBtn.Text = $"🎨 上传{category}贴图";
            catBtn.CustomMinimumSize = new Vector2(0, 40);
            catBtn.AddThemeFontSizeOverride("font_size", 13);
            catBtn.Pressed += () => ShowTextureUploadDialog(category);
            container.AddChild(catBtn);
        }

        // 已上传贴图列表
        container.AddChild(new HSeparator());

        var uploadedLabel = new Label();
        uploadedLabel.Text = "=== 已上传贴图 ===";
        uploadedLabel.AddThemeFontSizeOverride("font_size", 14);
        uploadedLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        container.AddChild(uploadedLabel);

        var uploadedList = new VBoxContainer();
        uploadedList.Name = "UploadedTextureList";
        uploadedList.AddThemeConstantOverride("separation", 4);
        container.AddChild(uploadedList);

        // ✅ 新增：应用到现有对象区域
        container.AddChild(new HSeparator());

        var applyLabel = new Label();
        applyLabel.Text = "=== 应用到现有对象 ===";
        applyLabel.AddThemeFontSizeOverride("font_size", 14);
        applyLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        container.AddChild(applyLabel);

        var applyHint = new Label();
        applyHint.Text = "选择已上传贴图，然后点击地图上的对象进行替换";
        applyHint.AddThemeFontSizeOverride("font_size", 11);
        applyHint.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        container.AddChild(applyHint);

        // 选择贴图
        var textureSelectHBox = new HBoxContainer();
        textureSelectHBox.AddThemeConstantOverride("separation", 8);

        var textureSelectLabel = new Label();
        textureSelectLabel.Text = "选择贴图:";
        textureSelectLabel.CustomMinimumSize = new Vector2(80, 28);
        textureSelectHBox.AddChild(textureSelectLabel);

        var textureOptionBtn = new OptionButton();
        textureOptionBtn.Name = "ApplyTextureSelect";
        textureOptionBtn.CustomMinimumSize = new Vector2(180, 28);
        textureOptionBtn.AddThemeFontSizeOverride("font_size", 11);
        textureSelectHBox.AddChild(textureOptionBtn);

        // 选择目标类型
        var targetTypeOption = new OptionButton();
        targetTypeOption.Name = "TargetTypeSelect";
        targetTypeOption.AddItem("单位", 0);
        targetTypeOption.AddItem("兵器", 1);
        targetTypeOption.AddItem("设施", 2);
        targetTypeOption.AddItem("地形", 3);
        targetTypeOption.AddItem("移动方式", 4);
        targetTypeOption.CustomMinimumSize = new Vector2(100, 28);
        targetTypeOption.AddThemeFontSizeOverride("font_size", 11);
        textureSelectHBox.AddChild(targetTypeOption);

        container.AddChild(textureSelectHBox);

        // 应用按钮
        var applyTextureBtn = new Button();
        applyTextureBtn.Text = "🖱️ 进入贴图替换模式";
        applyTextureBtn.CustomMinimumSize = new Vector2(300, 40);
        applyTextureBtn.AddThemeFontSizeOverride("font_size", 13);
        var applyBtnStyle = new StyleBoxFlat();
        applyBtnStyle.BgColor = new Color(0.3f, 0.5f, 0.6f, 0.9f);
        applyBtnStyle.SetCornerRadiusAll(8);
        applyTextureBtn.AddThemeStyleboxOverride("normal", applyBtnStyle);
        applyTextureBtn.Pressed += StartTextureApplyMode;
        container.AddChild(applyTextureBtn);

        scroll.AddChild(container);
        panel.AddChild(scroll);

        return panel;
    }

    private void ShowTextureUploadDialog(string category)
    {
        if (!ExtraSave.HasGalleryPermission())
        {
            ShowToast("请先请求相册访问权限");
            return;
        }

        var dialog = new FileDialog();
        dialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        dialog.Access = FileDialog.AccessEnum.Filesystem;
        dialog.Filters = new string[] { "*.png ; PNG Images", "*.jpg ; JPG Images", "*.jpeg ; JPEG Images", "*.gif ; GIF Images" };
        dialog.Title = $"选择{category}贴图 (32×32)";
        dialog.Size = new Vector2I(800, 600);

        dialog.FileSelected += (path) => {
            // 验证尺寸
            var image = new Image();
            Error err = image.Load(path);
            if (err != Error.Ok)
            {
                ShowToast("❌ 图片加载失败");
                dialog.QueueFree();
                return;
            }

            int w = image.GetWidth();
            int h = image.GetHeight();
            if (w != h)
            {
                ShowToast($"❌ 尺寸 {w}×{h} 不是正方形！\n请裁剪为正方形后重新上传");
                dialog.QueueFree();
                return;
            }
            if (w != 32 || h != 32)
            {
                ShowToast($"⚠️ 尺寸 {w}×{h} 非标准，将自动resize到 32×32");
                dialog.QueueFree();
                return;
            }

            // 保存
            string savedPath = extraSave?.ProcessUploadedTexture(path, category, $"custom_{DateTime.Now:yyyyMMddHHmmss}");
            if (!string.IsNullOrEmpty(savedPath) && !savedPath.StartsWith("ERROR"))
            {
                ShowToast($"✅ 贴图上传成功！\n{Path.GetFileName(savedPath)}");
                RefreshUploadedTextureList();
            }
            else
            {
                string errorMsg = savedPath switch
                {
                    "ERROR_NOT_SQUARE" => "❌ 贴图不是正方形，请裁剪后重新上传",
                    "ERROR_TOO_SMALL" => "❌ 贴图尺寸过小，无法resize",
                    "ERROR_LOAD_FAILED" => "❌ 贴图加载失败",
                    _ => "❌ 贴图保存失败"
                };
                ShowToast(errorMsg);
            }
            dialog.QueueFree();
        };

        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void ShowTextureApplyDialog(string category, string targetId)
    {
        // 为现有模板应用贴图
        var dialog = new Panel();
        dialog.CustomMinimumSize = new Vector2(350, 300);
        dialog.SetAnchorsPreset(LayoutPreset.Center);
        dialog.ZIndex = 1700;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.1f, 0.12f, 0.18f, 0.98f);
        style.SetCornerRadiusAll(12);
        dialog.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("margin_left", 15);
        vbox.AddThemeConstantOverride("margin_right", 15);
        vbox.AddThemeConstantOverride("margin_top", 15);
        vbox.AddThemeConstantOverride("margin_bottom", 15);
        vbox.AddThemeConstantOverride("separation", 10);

        var title = new Label();
        title.Text = $"🎨 为 {targetId} 应用贴图";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", Colors.Yellow);
        vbox.AddChild(title);

        var hint = new Label();
        hint.Text = "选择已上传的贴图文件，或上传新贴图";
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        vbox.AddChild(hint);

        // 已上传贴图列表
        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(0, 150);

        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 4);

        string textureDir = Path.Combine(ExtraSave.CUSTOM_TEXTURE_DIR, category);
        if (DirAccess.DirExistsAbsolute(textureDir))
        {
            var dir = DirAccess.Open(textureDir);
            if (dir != null)
            {
                dir.ListDirBegin();
                string fileName;
                while ((fileName = dir.GetNext()) != "")
                {
                    if (dir.CurrentIsDir()) continue;
                    var row = new HBoxContainer();
                    row.AddThemeConstantOverride("separation", 8);

                    var fileLabel = new Label();
                    fileLabel.Text = fileName;
                    fileLabel.CustomMinimumSize = new Vector2(180, 24);
                    fileLabel.AddThemeFontSizeOverride("font_size", 11);
                    row.AddChild(fileLabel);

                    var applyBtn = new Button();
                    applyBtn.Text = "应用";
                    applyBtn.CustomMinimumSize = new Vector2(60, 24);
                    applyBtn.AddThemeFontSizeOverride("font_size", 10);
                    string fullPath = Path.Combine(textureDir, fileName);
                    applyBtn.Pressed += () => {
                        ApplyTextureToTarget(category, targetId, fullPath);
                        dialog.QueueFree();
                    };
                    row.AddChild(applyBtn);

                    list.AddChild(row);
                }
                dir.ListDirEnd();
            }
        }

        if (list.GetChildCount() == 0)
        {
            var empty = new Label();
            empty.Text = "暂无上传的贴图";
            empty.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            list.AddChild(empty);
        }

        scroll.AddChild(list);
        vbox.AddChild(scroll);

        var uploadBtn = new Button();
        uploadBtn.Text = "📤 上传新贴图";
        uploadBtn.CustomMinimumSize = new Vector2(200, 36);
        uploadBtn.Pressed += () => {
            dialog.QueueFree();
            ShowTextureUploadDialog(category);
        };
        vbox.AddChild(uploadBtn);

        var cancelBtn = new Button();
        cancelBtn.Text = "❌ 取消";
        cancelBtn.CustomMinimumSize = new Vector2(200, 36);
        cancelBtn.Pressed += () => dialog.QueueFree();
        vbox.AddChild(cancelBtn);

        dialog.AddChild(vbox);
        AddChild(dialog);
        MakeMenuDraggable(dialog); // ✅ 使对话框可拖拽
    }

    private void ApplyTextureToTarget(string category, string targetId, string texturePath)
    {
        bool success = false;
        try
        {
            switch (category)
            {
                case "单位":
                    if (int.TryParse(targetId, out int uSlot))
                        success = extraSave?.ApplyCustomTextureToSavedUnit(uSlot, texturePath) ?? false;
                    break;
                case "兵器":
                    if (int.TryParse(targetId, out int wSlot))
                        success = extraSave?.ApplyCustomTextureToSavedWeapon(wSlot, texturePath) ?? false;
                    break;
                case "设施":
                    if (int.TryParse(targetId, out int fSlot))
                        success = extraSave?.ApplyCustomTextureToSavedFacility(fSlot, texturePath) ?? false;
                    break;
                case "地形":
                    if (int.TryParse(targetId, out int gSlot))
                        success = extraSave?.ApplyCustomTextureToSavedGrid(gSlot, texturePath) ?? false;
                    else
                        success = extraSave?.ApplyCustomTextureToTerrainType(targetId, texturePath) ?? false;
                    break;
                case "移动方式":
                    success = extraSave?.ApplyCustomTextureToMoveType(targetId, texturePath) ?? false;
                    break;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[CustomConfigUI] 贴图应用异常: {e.Message}");
        }

        ShowToast(success ? "✅ 贴图应用成功！" : "❌ 贴图应用失败");

        // ✅ 关键修复：成功后刷新运行时实例贴图
        if (success)
        {
            RefreshRuntimeTexture(category, targetId, texturePath);
        }
    }

    /// <summary>刷新地图上已放置实例的贴图</summary>
    private void RefreshRuntimeTexture(string category, string targetId, string texturePath)
    {
        if (gameManager == null) return;
        try
        {
            var image = new Image();
            if (image.Load(texturePath) != Error.Ok) return;
            var texture = ImageTexture.CreateFromImage(image);

            switch (category)
            {
                case "单位":
                    if (int.TryParse(targetId, out int uSlot))
                    {
                        var unitData = extraSave?.GetSavedUnit(uSlot);
                        if (unitData != null)
                        {
                            foreach (var unit in gameManager.unitManager?.AllUnits ?? new System.Collections.Generic.List<Infantry>())
                            {
                                if (unit?.Name == unitData.displayName || unit?.Name == unitData.slotName)
                                {
                                    if (unit.sprite != null) unit.sprite.Texture = texture;
                                }
                            }
                        }
                    }
                    break;
                case "兵器":
                    if (int.TryParse(targetId, out int wSlot))
                    {
                        var weaponData = extraSave?.GetSavedWeapon(wSlot);
                        if (weaponData != null)
                        {
                            foreach (var weapon in gameManager.weaponManager?.AllWeapons ?? new System.Collections.Generic.List<Weapon>())
                            {
                                if (weapon?.Name == weaponData.displayName || weapon?.Name == weaponData.slotName)
                                {
                                    // ✅ 修复：AnimatedSprite2D 没有 Texture 属性
                                    // 方案1：如果兵器有 sprite (Sprite2D) 字段，直接替换
                                    // 方案2：创建单帧 SpriteFrames
                                    if (weapon.GetNodeOrNull<Sprite2D>("Sprite2D") != null)
                                    {
                                        weapon.GetNode<Sprite2D>("Sprite2D").Texture = texture;
                                    }
                                    else if (weapon.animSprite != null)
                                    {
                                        // 创建单帧动画
                                        var frames = new SpriteFrames();
                                        frames.AddAnimation("default");
                                        frames.AddFrame("default", texture);
                                        weapon.animSprite.SpriteFrames = frames;
                                        weapon.animSprite.Play("default");
                                    }
                                }
                            }
                        }
                    }
                    break;
                case "地形":
                    if (int.TryParse(targetId, out int gSlot))
                    {
                        var gridData = extraSave?.GetSavedGridTerrain(gSlot);
                        if (gridData != null && terrainEditor?.gridManager?.grids != null)
                        {
                            foreach (var grid in terrainEditor.gridManager.grids)
                            {
                                if (grid?.gridType.ToString() == gridData.gridType.ToString())
                                {
                                    var sprite = grid.GetNodeOrNull<Sprite2D>("Sprite2D");
                                    if (sprite != null) sprite.Texture = texture;
                                }
                            }
                        }
                    }
                    break;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[CustomConfigUI] 刷新运行时贴图失败: {e.Message}");
        }
    }

    private void RefreshUploadedTextureList()
    {
        var list = textureUploadPanel?.GetNodeOrNull<VBoxContainer>("UploadedTextureList");
        if (list == null) return;

        foreach (var child in list.GetChildren())
            child.QueueFree();

        string textureDir = ExtraSave.CUSTOM_TEXTURE_DIR;
        if (!DirAccess.DirExistsAbsolute(textureDir))
        {
            var emptyLabel = new Label();
            emptyLabel.Text = "暂无上传的贴图";
            emptyLabel.AddThemeFontSizeOverride("font_size", 12);
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            list.AddChild(emptyLabel);
            return;
        }

        var dir = DirAccess.Open(textureDir);
        if (dir == null) return;

        dir.ListDirBegin();
        string folderName;
        while ((folderName = dir.GetNext()) != "")
        {
            if (dir.CurrentIsDir() && folderName != "." && folderName != "..")
            {
                var subDir = DirAccess.Open(Path.Combine(textureDir, folderName));
                if (subDir == null) continue;

                subDir.ListDirBegin();
                string fileName;
                while ((fileName = subDir.GetNext()) != "")
                {
                    if (subDir.CurrentIsDir()) continue;

                    var row = new HBoxContainer();
                    row.AddThemeConstantOverride("separation", 8);

                    var fileLabel = new Label();
                    fileLabel.Text = $"[{folderName}] {fileName}";
                    fileLabel.CustomMinimumSize = new Vector2(250, 24);
                    fileLabel.AddThemeFontSizeOverride("font_size", 11);
                    row.AddChild(fileLabel);

                    var previewBtn = new Button();
                    previewBtn.Text = "预览";
                    previewBtn.CustomMinimumSize = new Vector2(60, 24);
                    previewBtn.AddThemeFontSizeOverride("font_size", 10);
                    string fullPath = Path.Combine(textureDir, folderName, fileName);
                    previewBtn.Pressed += () => ShowTexturePreview(fullPath);
                    row.AddChild(previewBtn);

                    var delBtn = new Button();
                    delBtn.Text = "删除";
                    delBtn.CustomMinimumSize = new Vector2(60, 24);
                    delBtn.AddThemeFontSizeOverride("font_size", 10);
                    delBtn.AddThemeColorOverride("font_color", new Color(1, 0.4f, 0.4f));
                    delBtn.Pressed += () => {
                        try { DirAccess.RemoveAbsolute(fullPath); } catch { }
                        RefreshUploadedTextureList();
                    };
                    row.AddChild(delBtn);

                    list.AddChild(row);
                }
                subDir.ListDirEnd();
            }
        }
        dir.ListDirEnd();
    }

    private void ShowTexturePreview(string path)
    {
        var preview = new Panel();
        preview.CustomMinimumSize = new Vector2(200, 200);
        preview.SetAnchorsPreset(LayoutPreset.Center);
        preview.ZIndex = 1700;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.98f);
        style.SetCornerRadiusAll(8);
        preview.AddThemeStyleboxOverride("panel", style);

        var texRect = new TextureRect();
        texRect.SetAnchorsPreset(LayoutPreset.FullRect);
        texRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        texRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;

        var image = new Image();
        if (image.Load(path) == Error.Ok)
        {
            var tex = ImageTexture.CreateFromImage(image);
            texRect.Texture = tex;
        }

        preview.AddChild(texRect);

        var closeBtn = new Button();
        closeBtn.Text = "✕";
        closeBtn.Position = new Vector2(170, 5);
        closeBtn.Pressed += () => preview.QueueFree();
        preview.AddChild(closeBtn);

        AddChild(preview);
        MakeMenuDraggable(preview); // ✅ 使预览窗口可拖拽
    }
        // ==================== 保存/加载槽位Tab ====================

    private VBoxContainer CreateSaveSlotTab()
    {
        var panel = new VBoxContainer();
        panel.AddThemeConstantOverride("separation", 10);
        panel.AddThemeConstantOverride("margin_left", 10);
        panel.AddThemeConstantOverride("margin_right", 10);
        panel.AddThemeConstantOverride("margin_top", 10);
        panel.AddThemeConstantOverride("margin_bottom", 10);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;

        var container = new VBoxContainer();
        container.AddThemeConstantOverride("separation", 10);

        // ✅ 增强：null 安全检查（自动尝试加载）
        if (extraSave == null)
        {
            extraSave = GetTree()?.GetFirstNodeInGroup("extra_save") as ExtraSave;
        }
        if (extraSave == null || extraSave.Data == null)
        {
            var errorLabel = new Label();
            errorLabel.Text = "⚠️ ExtraSave 未初始化\n请检查场景中的 ExtraSave 节点";
            errorLabel.AddThemeFontSizeOverride("font_size", 14);
            errorLabel.AddThemeColorOverride("font_color", Colors.Red);
            container.AddChild(errorLabel);
            scroll.AddChild(container);
            panel.AddChild(scroll);
            return panel;
        }

        var data = extraSave.Data;

        // 单位槽位
        container.AddChild(CreateSlotSection("🚶 单位保存槽", ExtraSave.MAX_SLOTS, (i) => extraSave.GetSavedUnit(i), data.favoriteUnitSlots ?? new List<string>(), "单位"));

        // 兵器槽位
        container.AddChild(CreateSlotSection("⚔️ 兵器保存槽", ExtraSave.MAX_SLOTS, (i) => extraSave.GetSavedWeapon(i), data.favoriteWeaponSlots ?? new List<string>(), "兵器"));

        // 设施槽位
        container.AddChild(CreateSlotSection("🏙️ 设施保存槽", ExtraSave.MAX_SLOTS, (i) => extraSave.GetSavedFacility(i), data.favoriteFacilitySlots ?? new List<string>(), "设施"));

        // 地形槽位
        container.AddChild(CreateSlotSection("🗺️ 地形保存槽", ExtraSave.MAX_SLOTS, (i) => extraSave.GetSavedGridTerrain(i), data.favoriteGridSlots ?? new List<string>(), "地形"));

        // 地图槽位
        container.AddChild(CreateSlotSection("🌍 地图保存槽", ExtraSave.MAX_MAP_SLOTS, (i) => extraSave.GetSavedMap(i), null, "地图"));

        scroll.AddChild(container);
        panel.AddChild(scroll);

        return panel;
    }

    private VBoxContainer CreateSlotSection<T>(string title, int maxSlots, Func<int, T> getSlot, List<string> favorites, string category) where T : class
    {
        var section = new VBoxContainer();
        section.AddThemeConstantOverride("separation", 6);

        var titleLabel = new Label();
        titleLabel.Text = title;
        titleLabel.AddThemeFontSizeOverride("font_size", 14);
        titleLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        section.AddChild(titleLabel);

        var grid = new GridContainer();
        grid.Columns = 5;
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);

        // ✅ 安全处理 favorites
        favorites = favorites ?? new List<string>();

        for (int i = 0; i < maxSlots; i++)
        {
            var slotIndex = i;
            var slotData = getSlot?.Invoke(i);

            var slotBtn = new Button();
            slotBtn.CustomMinimumSize = new Vector2(80, 60);
            slotBtn.AddThemeFontSizeOverride("font_size", 10);

            string displayName = $"{category}{i + 1}";
            bool isEmpty = true;
            bool isFav = false;
            string slotName = $"{category}{i + 1}";

            if (slotData is SavedUnitData u)
            {
                displayName = string.IsNullOrEmpty(u.displayName) ? displayName : u.displayName;
                isEmpty = string.IsNullOrEmpty(u.scenePath) && u.fieldValues.Count == 0;
                isFav = u.isFavorite;
                slotName = u.slotName;
            }
            else if (slotData is SavedWeaponData w)
            {
                displayName = string.IsNullOrEmpty(w.displayName) ? displayName : w.displayName;
                isEmpty = string.IsNullOrEmpty(w.scenePath) && w.fieldValues.Count == 0;
                isFav = w.isFavorite;
                slotName = w.slotName;
            }
            else if (slotData is SavedFacilityData f)
            {
                displayName = string.IsNullOrEmpty(f.displayName) ? displayName : f.displayName;
                isEmpty = string.IsNullOrEmpty(f.scenePath) && f.fieldValues.Count == 0;
                isFav = f.isFavorite;
                slotName = f.slotName;
            }
            else if (slotData is SavedGridTerrainData g)
            {
                displayName = string.IsNullOrEmpty(g.displayName) ? displayName : g.displayName;
                isEmpty = g.savedTimestamp == 0;
                isFav = g.isFavorite;
                slotName = g.slotName;
            }
            else if (slotData is SavedMapData m)
            {
                displayName = string.IsNullOrEmpty(m.displayName) ? displayName : m.displayName;
                isEmpty = m.width == 0;
                slotName = m.slotName;
            }

            slotBtn.Text = isFav ? $"★{displayName}" : displayName;

            var style = new StyleBoxFlat();
            if (isEmpty)
            {
                style.BgColor = new Color(0.2f, 0.2f, 0.25f, 0.8f);
                slotBtn.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            }
            else if (isFav)
            {
                style.BgColor = new Color(0.8f, 0.6f, 0.2f, 0.9f);
                slotBtn.AddThemeColorOverride("font_color", Colors.Yellow);
            }
            else
            {
                style.BgColor = new Color(0.3f, 0.4f, 0.6f, 0.9f);
                slotBtn.AddThemeColorOverride("font_color", Colors.White);
            }
            style.SetCornerRadiusAll(6);
            slotBtn.AddThemeStyleboxOverride("normal", style);

            slotBtn.Pressed += () => ShowSlotContextMenu(slotIndex, category, slotData, isEmpty, isFav, slotName);
            grid.AddChild(slotBtn);
        }

        section.AddChild(grid);
        return section;
    }

    private void ShowSlotContextMenu<T>(int slotIndex, string category, T slotData, bool isEmpty, bool isFav, string slotName) where T : class
    {
        var menu = new PopupMenu();
        menu.AddItem($"保存当前{category}", 0);
        menu.AddItem($"加载{category}", 1);
        menu.AddItem(isFav ? "取消收藏" : "收藏", 2);
        menu.AddItem("改名", 3);
        if (!isEmpty)
        {
            menu.AddItem("清除", 4);
        }

        menu.IdPressed += (id) => {
            switch (id)
            {
                case 0: // 保存
                    ShowSaveDialog(slotIndex, category);
                    break;
                case 1: // 加载
                    if (!isEmpty)
                        ShowLoadDialog(slotIndex, category);
                    else
                        ShowToast("槽位为空，无法加载");
                    break;
                case 2: // 收藏
                    ToggleFavorite(slotIndex, category);
                    break;
                case 3: // 改名
                    ShowRenameDialog(slotIndex, category);
                    break;
                case 4: // 清除
                    ClearSlot(slotIndex, category);
                    break;
            }
            menu.QueueFree();
        };

        AddChild(menu);
        menu.Popup(new Rect2I((int)GetGlobalMousePosition().X, (int)GetGlobalMousePosition().Y, 150, 150));
    }

    private void ShowSaveDialog(int slotIndex, string category)
    {
        var dialog = new Panel();
        dialog.CustomMinimumSize = new Vector2(350, 200);
        dialog.SetAnchorsPreset(LayoutPreset.Center);
        dialog.ZIndex = 1600;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.1f, 0.12f, 0.18f, 0.98f);
        style.SetCornerRadiusAll(12);
        dialog.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("margin_left", 15);
        vbox.AddThemeConstantOverride("margin_right", 15);
        vbox.AddThemeConstantOverride("margin_top", 15);
        vbox.AddThemeConstantOverride("margin_bottom", 15);
        vbox.AddThemeConstantOverride("separation", 10);

        var title = new Label();
        title.Text = $"💾 保存{category}到槽位{slotIndex + 1}";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", new Color(0.4f, 0.9f, 1f));
        vbox.AddChild(title);

        var nameHBox = new HBoxContainer();
        nameHBox.AddThemeConstantOverride("separation", 8);
        var nameLabel = new Label();
        nameLabel.Text = "自定义名称:";
        nameHBox.AddChild(nameLabel);
        var nameEdit = new LineEdit();
        nameEdit.CustomMinimumSize = new Vector2(200, 28);
        nameEdit.PlaceholderText = "留空使用默认名称";
        nameHBox.AddChild(nameEdit);
        vbox.AddChild(nameHBox);

        var btnHBox = new HBoxContainer();
        btnHBox.AddThemeConstantOverride("separation", 10);

        var confirmBtn = new Button();
        confirmBtn.Text = "✅ 确认保存";
        confirmBtn.CustomMinimumSize = new Vector2(120, 36);
        var confirmStyle = new StyleBoxFlat();
        confirmStyle.BgColor = new Color(0.2f, 0.5f, 0.3f, 0.9f);
        confirmStyle.SetCornerRadiusAll(8);
        confirmBtn.AddThemeStyleboxOverride("normal", confirmStyle);
        confirmBtn.Pressed += () => {
            SaveToSlot(slotIndex, category, nameEdit.Text);
            dialog.QueueFree();
        };
        btnHBox.AddChild(confirmBtn);

        var cancelBtn = new Button();
        cancelBtn.Text = "❌ 取消";
        cancelBtn.CustomMinimumSize = new Vector2(120, 36);
        cancelBtn.Pressed += () => dialog.QueueFree();
        btnHBox.AddChild(cancelBtn);

        vbox.AddChild(btnHBox);
        dialog.AddChild(vbox);
        AddChild(dialog);
        MakeMenuDraggable(dialog); // ✅ 使对话框可拖拽
    }

    private void SaveToSlot(int slotIndex, string category, string customName)
    {
        var te = terrainEditor;
        // ✅ 处理自定义名称：截断、清理
        string finalName = string.IsNullOrWhiteSpace(customName) ? null : customName.Trim();
        if (finalName != null && finalName.Length > 20)
        {
            finalName = finalName.Substring(0, 20);
            ShowToast("⚠️ 名称过长，已截断至20字符");
        }

        switch (category)
        {
            case "单位":
                if (te?.selectedGrid != null && te.selectedGrid.infantries.Count > 0)
                {
                    var unit = te.selectedGrid.infantries[0];
                    if (!string.IsNullOrEmpty(finalName)) unit.Name = finalName; // ✅ 应用名称到实例
                    extraSave?.SaveUnit(unit, slotIndex, finalName);
                    ShowToast($"✅ 单位已保存到槽位{slotIndex + 1}" + (finalName != null ? $" 名称:{finalName}" : ""));
                }
                else
                {
                    ShowToast("❌ 请先选择一个单位");
                }
                break;

            case "兵器":
                if (te?.selectedGrid != null && te.selectedGrid.weapon != null)
                {
                    var weapon = te.selectedGrid.weapon;
                    if (!string.IsNullOrEmpty(finalName)) weapon.Name = finalName;
                    extraSave?.SaveWeapon(weapon, slotIndex, finalName);
                    ShowToast($"✅ 兵器已保存到槽位{slotIndex + 1}" + (finalName != null ? $" 名称:{finalName}" : ""));
                }
                else
                {
                    ShowToast("❌ 请先选择一个兵器");
                }
                break;

            case "设施":
                if (te?.selectedGrid != null && te.selectedGrid.city != null)
                {
                    var facility = te.selectedGrid.city;
                    if (!string.IsNullOrEmpty(finalName)) facility.Name = finalName;
                    extraSave?.SaveFacility(facility, slotIndex, finalName);
                    ShowToast($"✅ 设施已保存到槽位{slotIndex + 1}" + (finalName != null ? $" 名称:{finalName}" : ""));
                }
                else
                {
                    ShowToast("❌ 请先选择一个设施");
                }
                break;

            case "地形":
                if (te?.selectedGrid != null)
                {
                    extraSave?.SaveGridTerrain(te.selectedGrid, slotIndex, finalName);
                    ShowToast($"✅ 地形已保存到槽位{slotIndex + 1}" + (finalName != null ? $" 名称:{finalName}" : ""));
                }
                else
                {
                    ShowToast("❌ 请先选择一个格子");
                }
                break;

            case "地图":
                extraSave?.SaveCurrentMap(slotIndex, finalName, gameManager, te?.gridManager);
                ShowToast($"✅ 地图已保存到槽位{slotIndex + 1}" + (finalName != null ? $" 名称:{finalName}" : ""));
                break;
        }

        RefreshSaveSlotTab();
    }

    private void ShowLoadDialog(int slotIndex, string category)
    {
        ShowToast($"加载{category}槽位{slotIndex + 1}...");

        var te = terrainEditor;
        if (te == null)
        {
            ShowToast("❌ TerrainEditor 未连接");
            return;
        }

        switch (category)
        {
            case "单位":
                var unitData = extraSave?.GetSavedUnit(slotIndex);
                if (unitData != null && !string.IsNullOrEmpty(unitData.scenePath))
                {
                    te.SetSpawnMode(SpawnMode.Unit, unitData.scenePath, unitData.team);
                    ShowToast($"✅ 单位 \"{unitData.displayName}\" 已准备，点击地图格子放置");
                }
                else
                {
                    ShowToast("❌ 该槽位没有保存的单位");
                }
                break;

            case "兵器":
                var weaponData = extraSave?.GetSavedWeapon(slotIndex);
                if (weaponData != null && !string.IsNullOrEmpty(weaponData.scenePath))
                {
                    te.SetSpawnMode(SpawnMode.Weapon, weaponData.scenePath, weaponData.team);
                    ShowToast($"✅ 兵器 \"{weaponData.displayName}\" 已准备，点击地图格子放置");
                }
                else
                {
                    ShowToast("❌ 该槽位没有保存的兵器");
                }
                break;

            case "设施":
                var facilityData = extraSave?.GetSavedFacility(slotIndex);
                if (facilityData != null && !string.IsNullOrEmpty(facilityData.scenePath))
                {
                    te.SetSpawnMode(SpawnMode.Facility, facilityData.scenePath, facilityData.team);
                    ShowToast($"✅ 设施 \"{facilityData.displayName}\" 已准备，点击地图格子放置");
                }
                else
                {
                    ShowToast("❌ 该槽位没有保存的设施");
                }
                break;

            case "地形":
                var gridData = extraSave?.GetSavedGridTerrain(slotIndex);
                if (gridData != null && gridData.savedTimestamp > 0)
                {
                    te.SetApplyTerrainMode(gridData);
                    ShowToast($"✅ 地形 \"{gridData.displayName}\" 已准备，点击地图格子应用");
                }
                else
                {
                    ShowToast("❌ 该槽位没有保存的地形");
                }
                break;

            case "地图":
                var mapData = extraSave?.GetSavedMap(slotIndex);
                if (mapData != null && mapData.width > 0)
                {
                    bool success = extraSave?.LoadMapToGame(slotIndex, gameManager, te.gridManager, te) ?? false;
                    ShowToast(success ? $"✅ 地图 \"{mapData.displayName}\" 加载成功！" : "❌ 地图加载失败");
                }
                else
                {
                    ShowToast("❌ 该槽位没有保存的地图");
                }
                break;
        }
    }

    private void ToggleFavorite(int slotIndex, string category)
    {
        bool success = false;
        try
        {
            switch (category)
            {
                case "单位": success = extraSave?.ToggleUnitFavorite(slotIndex) ?? false; break;
                case "兵器": success = extraSave?.ToggleWeaponFavorite(slotIndex) ?? false; break;
                case "设施": success = extraSave?.ToggleFacilityFavorite(slotIndex) ?? false; break;
                case "地形": success = extraSave?.ToggleGridFavorite(slotIndex) ?? false; break;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[CustomConfigUI] 收藏切换异常: {e.Message}");
        }

        if (success)
            ShowToast($"✅ {category}槽位{slotIndex + 1} 收藏状态已切换");
        else
            ShowToast($"❌ 收藏失败（可能已达上限{ExtraSave.MAX_FAVORITES}个）");

        RefreshSaveSlotTab();
    }

    private void ShowRenameDialog(int slotIndex, string category)
    {
        var dialog = new Panel();
        dialog.CustomMinimumSize = new Vector2(300, 150);
        dialog.SetAnchorsPreset(LayoutPreset.Center);
        dialog.ZIndex = 1600;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.1f, 0.12f, 0.18f, 0.98f);
        style.SetCornerRadiusAll(12);
        dialog.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("margin_left", 15);
        vbox.AddThemeConstantOverride("margin_right", 15);
        vbox.AddThemeConstantOverride("margin_top", 15);
        vbox.AddThemeConstantOverride("margin_bottom", 15);
        vbox.AddThemeConstantOverride("separation", 10);

        var title = new Label();
        title.Text = $"✏️ 重命名{category}{slotIndex + 1}";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(title);

        var nameEdit = new LineEdit();
        nameEdit.CustomMinimumSize = new Vector2(250, 28);
        nameEdit.PlaceholderText = "输入新名称";
        vbox.AddChild(nameEdit);

        var btnHBox = new HBoxContainer();
        btnHBox.AddThemeConstantOverride("separation", 10);

        var confirmBtn = new Button();
        confirmBtn.Text = "确认";
        confirmBtn.Pressed += () => {
            bool success = false;
            switch (category)
            {
                case "单位": success = extraSave?.RenameUnit(slotIndex, nameEdit.Text) ?? false; break;
                case "兵器": success = extraSave?.RenameWeapon(slotIndex, nameEdit.Text) ?? false; break;
                case "设施": success = extraSave?.RenameFacility(slotIndex, nameEdit.Text) ?? false; break;
                case "地形": success = extraSave?.RenameGridTerrain(slotIndex, nameEdit.Text) ?? false; break;
                case "地图": success = extraSave?.RenameMap(slotIndex, nameEdit.Text) ?? false; break;
            }

            if (success)
            {
                ShowToast("✅ 重命名成功！");
                RefreshSaveSlotTab();
            }
            else
            {
                ShowToast("❌ 重命名失败");
            }
            dialog.QueueFree();
        };
        btnHBox.AddChild(confirmBtn);

        var cancelBtn = new Button();
        cancelBtn.Text = "取消";
        cancelBtn.Pressed += () => dialog.QueueFree();
        btnHBox.AddChild(cancelBtn);

        vbox.AddChild(btnHBox);
        dialog.AddChild(vbox);
        AddChild(dialog);
        MakeMenuDraggable(dialog); // ✅ 使对话框可拖拽
    }

    private void ClearSlot(int slotIndex, string category)
    {
        // 清除槽位数据
        switch (category)
        {
            case "单位":
                extraSave?.ClearUnitSlot(slotIndex);
                break;
            case "兵器":
                extraSave?.ClearWeaponSlot(slotIndex);
                break;
            case "设施":
                extraSave?.ClearFacilitySlot(slotIndex);
                break;
            case "地形":
                extraSave?.ClearGridSlot(slotIndex);
                break;
            case "地图":
                extraSave?.ClearMapSlot(slotIndex);
                break;
        }

        ShowToast($"✅ {category}槽位{slotIndex + 1} 已清除");
        RefreshSaveSlotTab();
    }

    private void RefreshSaveSlotTab()
    {
        // 重新创建保存槽位面板
        var oldPanel = saveSlotPanel;
        saveSlotPanel = CreateSaveSlotTab();
        tabContainer.RemoveChild(oldPanel);
        oldPanel.QueueFree();
        tabContainer.AddChild(saveSlotPanel);
        tabContainer.SetTabTitle(3, "💾 保存/加载");
    }

    // ==================== 通用方法 ====================

    public new void Show()
    {
        if (mainPanel == null) return;
        mainPanel.Show();
        // ✅ 确保 ExtraSave 已初始化
        if (extraSave == null)
        {
            extraSave = GetTree()?.GetFirstNodeInGroup("extra_save") as ExtraSave;
        }
        RefreshCustomMoveTypeList();
        RefreshCustomTerrainTypeList();
        RefreshUploadedTextureList();
        RefreshApplyTextureList();
        RefreshSaveSlotTab(); // 保存槽位界面
        GD.Print("[CustomConfigUI] 配置面板已打开，所有列表已刷新");
    }

    public new void Hide()
    {
        mainPanel?.Hide();
    }



    public override void _Process(double delta)
    {
        if (isDraggingMenu && mainPanel != null)
        {
            Vector2 newPosition = GetGlobalMousePosition() + dragOffset;
            var viewportSize = GetViewport().GetVisibleRect().Size;
            newPosition.X = Mathf.Clamp(newPosition.X, 0, viewportSize.X - mainPanel.Size.X);
            newPosition.Y = Mathf.Clamp(newPosition.Y, 0, viewportSize.Y - mainPanel.Size.Y);
            mainPanel.GlobalPosition = newPosition;
        }
    }

    // ✅ 拖拽增强：使任意对话框可拖拽并管理ZIndex
    private void MakeMenuDraggable(Control menuPanel, Control dragHandle = null)
    {
        Control handle = dragHandle ?? menuPanel;
        handle.MouseFilter = MouseFilterEnum.Stop;

        handle.GuiInput += (InputEvent @event) => {
            if (@event is InputEventMouseButton mouseEvent)
            {
                if (mouseEvent.ButtonIndex == MouseButton.Left)
                {
                    if (mouseEvent.Pressed)
                    {
                        isDraggingMenu = true;
                        dragOffset = menuPanel.GlobalPosition - handle.GetGlobalMousePosition();
                        menuPanel.ZIndex = 2000; // 拖拽时置顶
                    }
                    else
                    {
                        isDraggingMenu = false;
                        menuPanel.ZIndex = menuPanel.Name == "CustomConfigPanel" ? 1500 : 1600;
                    }
                }
            }
        };
    }

    // ✅ 贴图应用模式
    private void StartTextureApplyMode()
    {
        var textureOption = textureUploadPanel?.GetNodeOrNull<OptionButton>("ApplyTextureSelect");
        var targetTypeOption = textureUploadPanel?.GetNodeOrNull<OptionButton>("TargetTypeSelect");

        if (textureOption == null || targetTypeOption == null) 
        {
            ShowToast("❌ UI组件未初始化");
            return;
        }
        if (textureOption.Selected < 0 || textureOption.Selected >= textureOption.ItemCount) 
        {
            ShowToast("❌ 请先选择贴图");
            return;
        }

        // ✅ 增强：验证贴图路径
        var metadata = textureOption.GetItemMetadata(textureOption.Selected);
        if (metadata.VariantType == Variant.Type.Nil)
        {
            ShowToast("❌ 贴图路径无效");
            return;
        }

        pendingApplyTexturePath = metadata.AsString();
        pendingApplyTargetType = targetTypeOption.GetItemText(targetTypeOption.Selected);

        if (string.IsNullOrEmpty(pendingApplyTexturePath))
        {
            ShowToast("❌ 贴图路径为空");
            return;
        }
        if (!Godot.FileAccess.FileExists(pendingApplyTexturePath))
        {
            ShowToast("❌ 贴图文件不存在");
            return;
        }

        if (terrainEditor != null)
        {
            terrainEditor.SetTextureApplyMode(pendingApplyTexturePath, pendingApplyTargetType);
            ShowToast($"🖱️ 点击地图上的{pendingApplyTargetType}替换贴图，ESC取消");
            Hide();
        }
        else
        {
            ShowToast("❌ TerrainEditor未连接");
        }
    }

    private void RefreshApplyTextureList()
    {
        var textureOption = textureUploadPanel?.GetNodeOrNull<OptionButton>("ApplyTextureSelect");
        if (textureOption == null) return;

        textureOption.Clear();

        string textureDir = ExtraSave.CUSTOM_TEXTURE_DIR;
        if (!DirAccess.DirExistsAbsolute(textureDir)) return;

        var dir = DirAccess.Open(textureDir);
        if (dir == null) return;

        dir.ListDirBegin();
        string folderName;
        while ((folderName = dir.GetNext()) != "")
        {
            if (dir.CurrentIsDir() && folderName != "." && folderName != "..")
            {
                var subDir = DirAccess.Open(Path.Combine(textureDir, folderName));
                if (subDir == null) continue;

                subDir.ListDirBegin();
                string fileName;
                while ((fileName = subDir.GetNext()) != "")
                {
                    if (subDir.CurrentIsDir()) continue;
                    string fullPath = Path.Combine(textureDir, folderName, fileName);
                    int idx = textureOption.ItemCount;
                    textureOption.AddItem($"[{folderName}] {fileName}", idx);
                    textureOption.SetItemMetadata(idx, fullPath);
                }
                subDir.ListDirEnd();
            }
        }
        dir.ListDirEnd();
    }

    private void ShowToast(string message)
    {
        var toast = new Label();
        toast.Text = message;
        toast.HorizontalAlignment = HorizontalAlignment.Center;
        toast.AddThemeFontSizeOverride("font_size", 14);
        toast.AddThemeColorOverride("font_color", Colors.Yellow);
        toast.SetAnchorsPreset(LayoutPreset.CenterTop);
        toast.Position = new Vector2(0, 100);
        toast.ZIndex = 2000;

        var bg = new ColorRect();
        bg.Color = new Color(0, 0, 0, 0.8f);
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        toast.AddChild(bg);

        AddChild(toast);

        var timer = new Timer();
        timer.WaitTime = 2.0f;
        timer.OneShot = true;
        timer.Timeout += () => { toast.QueueFree(); timer.QueueFree(); };
        AddChild(timer);
        timer.Start();
    }

    // 占位方法
    private void ShowEditMoveTypeDialog(string id) { }
    private void ShowBuiltInMoveTypeCosts(string id) { }
    private void ShowEditTerrainTypeDialog(string id) { }
}