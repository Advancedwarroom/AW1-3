using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

// CustomConfigUI · TerrainTypeTab（由 CustomConfigUI.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class CustomConfigUI
{
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
        TranslateGroup.SetText(newBtn, "➕ 新建自定义地形");
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
        TranslateGroup.SetText(listLabel, "=== 现有地形种类 ===");
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
        TranslateGroup.SetText(customLabel, "=== 自定义地形 ===");
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
        infoLabel.Text = isBuiltIn ? TranslateGroup.T("[内置]") : TranslateGroup.T("[自定义]");
        infoLabel.AddThemeFontSizeOverride("font_size", 11);
        infoLabel.AddThemeColorOverride("font_color", isBuiltIn ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.4f, 1f, 0.4f));
        hbox.AddChild(infoLabel);

        if (!isBuiltIn)
        {
            var editBtn = new Button();
            TranslateGroup.SetText(editBtn, "编辑");
            editBtn.CustomMinimumSize = new Vector2(60, 28);
            editBtn.AddThemeFontSizeOverride("font_size", 11);
            editBtn.Pressed += () => ShowEditTerrainTypeDialog(terrainId);
            hbox.AddChild(editBtn);

            var delBtn = new Button();
            TranslateGroup.SetText(delBtn, "删除");
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
            texBtn.Pressed += () => ShowTextureApplyDialog(TranslateGroup.T("地形"), terrainId);
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
        TranslateGroup.SetText(title, "➕ 新建自定义地形");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        vbox.AddChild(title);

        // ID
        var idHBox = new HBoxContainer();
        idHBox.AddThemeConstantOverride("separation", 8);
        var idLabel = new Label();
        TranslateGroup.SetText(idLabel, "ID (英文):");
        idLabel.CustomMinimumSize = new Vector2(80, 28);
        idHBox.AddChild(idLabel);
        var idEdit = new LineEdit();
        idEdit.CustomMinimumSize = new Vector2(200, 28);
        idEdit.PlaceholderText = TranslateGroup.T("如: MyForest");
        idHBox.AddChild(idEdit);
        vbox.AddChild(idHBox);

        // 显示名称
        var nameHBox = new HBoxContainer();
        nameHBox.AddThemeConstantOverride("separation", 8);
        var nameLabel = new Label();
        TranslateGroup.SetText(nameLabel, $"显示{TranslateGroup.T("名称")}:");
        nameLabel.CustomMinimumSize = new Vector2(80, 28);
        nameHBox.AddChild(nameLabel);
        var nameEdit = new LineEdit();
        nameEdit.CustomMinimumSize = new Vector2(200, 28);
        nameEdit.PlaceholderText = TranslateGroup.T("如: 我的森林");
        nameHBox.AddChild(nameEdit);
        vbox.AddChild(nameHBox);

        // 防御加成
        var defHBox = new HBoxContainer();
        defHBox.AddThemeConstantOverride("separation", 8);
        var defLabel = new Label();
        TranslateGroup.SetText(defLabel, "防御加成:");
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
        TranslateGroup.SetText(colorLabel, "地形颜色:");
        colorLabel.CustomMinimumSize = new Vector2(80, 28);
        colorHBox.AddChild(colorLabel);
        var colorPicker = new ColorPickerButton();
        colorPicker.Color = new Color(0.4f, 0.7f, 0.3f);
        colorPicker.CustomMinimumSize = new Vector2(80, 28);
        colorHBox.AddChild(colorPicker);
        vbox.AddChild(colorHBox);

        // 移动方式消耗配置
        var costLabel = new Label();
        TranslateGroup.SetText(costLabel, "=== 移动方式消耗配置 ===");
        costLabel.AddThemeFontSizeOverride("font_size", 14);
        costLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        vbox.AddChild(costLabel);

        var hintLabel = new Label();
        TranslateGroup.SetText(hintLabel, "未配置 = 999 (不可移动)");
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
        TranslateGroup.SetText(confirmBtn, "✅ 确认创建");
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
                ShowToast(TranslateGroup.T("ID和名称不能为空"));
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
            ShowToast(TranslateGroup.T("自定义地形已创建！"));
        };
        btnHBox.AddChild(confirmBtn);

        var cancelBtn = new Button();
        TranslateGroup.SetText(cancelBtn, "❌ 取消");
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
            TranslateGroup.SetText(emptyLabel, "暂无自定义地形");
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
    private void ShowEditTerrainTypeDialog(string id) { }
}
