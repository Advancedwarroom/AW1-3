using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

// CustomConfigUI · MoveTypeTab（由 CustomConfigUI.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class CustomConfigUI
{

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
        TranslateGroup.SetText(newBtn, "➕ 新建自定义移动方式");
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
        TranslateGroup.SetText(listLabel, "=== 现有移动方式 ===");
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
        TranslateGroup.SetText(customLabel, "=== 自定义移动方式 ===");
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
            editBtn.Pressed += () => ShowEditMoveTypeDialog(moveTypeId);
            hbox.AddChild(editBtn);

            var delBtn = new Button();
            TranslateGroup.SetText(delBtn, "删除");
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
            texBtn.Pressed += () => ShowTextureApplyDialog(TranslateGroup.T("移动方式"), moveTypeId);
            hbox.AddChild(texBtn);
        }
        else
        {
            var viewBtn = new Button();
            TranslateGroup.SetText(viewBtn, "查看消耗");
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
        TranslateGroup.SetText(title, "➕ 新建自定义移动方式");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        vbox.AddChild(title);

        // ID输入
        var idHBox = new HBoxContainer();
        idHBox.AddThemeConstantOverride("separation", 8);
        var idLabel = new Label();
        TranslateGroup.SetText(idLabel, "ID (英文):");
        idLabel.CustomMinimumSize = new Vector2(80, 28);
        idHBox.AddChild(idLabel);
        var idEdit = new LineEdit();
        idEdit.Name = "MoveTypeIdEdit";
        idEdit.CustomMinimumSize = new Vector2(200, 28);
        idEdit.PlaceholderText = TranslateGroup.T("如: MyHover");
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
        nameEdit.Name = "MoveTypeNameEdit";
        nameEdit.CustomMinimumSize = new Vector2(200, 28);
        nameEdit.PlaceholderText = TranslateGroup.T("如: 我的气垫");
        nameHBox.AddChild(nameEdit);
        vbox.AddChild(nameHBox);

        // 地形消耗配置
        var costLabel = new Label();
        TranslateGroup.SetText(costLabel, "=== 地形移动消耗配置 ===");
        costLabel.AddThemeFontSizeOverride("font_size", 14);
        costLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        vbox.AddChild(costLabel);

        var hintLabel = new Label();
        TranslateGroup.SetText(hintLabel, "未配置 = 999 (不可移动)");
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
                        string terrainId = spinBox.Name.ToString().Replace("Cost_", "");
                        costs[terrainId] = (int)spinBox.Value;
                    }
                }
            }

            extraSave?.AddCustomMoveType(id, displayName, costs);
            dialog.QueueFree();
            RefreshCustomMoveTypeList();
            ShowToast(TranslateGroup.T("自定义移动方式已创建！"));
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

    private void RefreshCustomMoveTypeList()
    {
        var list = moveTypePanel?.GetNodeOrNull<VBoxContainer>("CustomMoveTypeList");
        if (list == null) return;

        foreach (var child in list.GetChildren())
            child.QueueFree();

        if (extraSave?.CustomMoveTypes == null || extraSave.CustomMoveTypes.Count == 0)
        {
            var emptyLabel = new Label();
            TranslateGroup.SetText(emptyLabel, "暂无自定义移动方式");
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

    // 占位方法
    private void ShowEditMoveTypeDialog(string moveTypeId)
    {
        if (extraSave?.CustomMoveTypes == null || !extraSave.CustomMoveTypes.ContainsKey(moveTypeId))
            return;

        var customMove = extraSave.CustomMoveTypes[moveTypeId];

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
        title.Text = $"✏️ {TranslateGroup.T("编辑")} {customMove.displayName}";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        vbox.AddChild(title);

        var nameHBox = new HBoxContainer();
        nameHBox.AddThemeConstantOverride("separation", 8);
        var nameLabel = new Label();
        TranslateGroup.SetText(nameLabel, "显示名称:");
        nameLabel.CustomMinimumSize = new Vector2(80, 28);
        nameHBox.AddChild(nameLabel);
        var nameEdit = new LineEdit();
        nameEdit.Text = customMove.displayName;
        nameEdit.CustomMinimumSize = new Vector2(200, 28);
        nameHBox.AddChild(nameEdit);
        vbox.AddChild(nameHBox);

        var costLabel = new Label();
        TranslateGroup.SetText(costLabel, "=== 地形移动消耗配置 ===");
        costLabel.AddThemeFontSizeOverride("font_size", 14);
        costLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        vbox.AddChild(costLabel);

        var hintLabel = new Label();
        TranslateGroup.SetText(hintLabel, "未配置 = 999 (不可移动)");
        hintLabel.AddThemeFontSizeOverride("font_size", 10);
        hintLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        vbox.AddChild(hintLabel);

        var costScroll = new ScrollContainer();
        costScroll.CustomMinimumSize = new Vector2(0, 250);

        var costContainer = new VBoxContainer();
        costContainer.Name = "EditCostContainer";
        costContainer.AddThemeConstantOverride("separation", 4);

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
            int cost = customMove.moveCosts.GetValueOrDefault(terrain, 999);
            spinBox.Value = cost;
            spinBox.CustomMinimumSize = new Vector2(80, 24);
            spinBox.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(spinBox);

            costContainer.AddChild(row);
        }

        costScroll.AddChild(costContainer);
        vbox.AddChild(costScroll);

        var btnHBox = new HBoxContainer();
        btnHBox.AddThemeConstantOverride("separation", 10);

        var confirmBtn = new Button();
        TranslateGroup.SetText(confirmBtn, "✅ 确认修改");
        confirmBtn.CustomMinimumSize = new Vector2(120, 36);
        confirmBtn.Pressed += () => {
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
            extraSave?.AddCustomMoveType(moveTypeId, nameEdit.Text.Trim(), costs);
            dialog.QueueFree();
            RefreshCustomMoveTypeList();
            ShowToast(TranslateGroup.T("自定义移动方式已更新！"));
        };
        btnHBox.AddChild(confirmBtn);

        var cancelBtn = new Button();
        TranslateGroup.SetText(cancelBtn, "❌ 取消");
        cancelBtn.CustomMinimumSize = new Vector2(120, 36);
        cancelBtn.Pressed += () => dialog.QueueFree();
        btnHBox.AddChild(cancelBtn);

        vbox.AddChild(btnHBox);
        dialog.AddChild(vbox);
        AddChild(dialog);
        MakeMenuDraggable(dialog);
    }
    private void ShowBuiltInMoveTypeCosts(string moveTypeId)
    {
        GD.Print($"[CustomConfigUI] 打开内置移动方式消耗: {moveTypeId}");
        try
        {
            var dialog = new Panel();
            dialog.Name = $"BuiltInMoveCosts_{moveTypeId}";
            // ✅ 自适应屏幕大小
            var viewportSize = GetViewport().GetVisibleRect().Size;
            float dlgWidth = Mathf.Min(420, viewportSize.X * 0.9f);
            float dlgHeight = Mathf.Min(550, viewportSize.Y * 0.8f);
            dialog.CustomMinimumSize = new Vector2(dlgWidth, dlgHeight);
            dialog.Size = new Vector2(dlgWidth, dlgHeight);
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
            title.Text = $"📊 {moveTypeId} - {TranslateGroup.T("移动消耗")}";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 18);
            title.AddThemeColorOverride("font_color", Colors.Yellow);
            vbox.AddChild(title);

            var hint = new Label();
            TranslateGroup.SetText(hint, "内置移动方式的消耗由游戏逻辑决定，可基于此创建自定义版本");
            hint.AddThemeFontSizeOverride("font_size", 11);
            hint.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            hint.AutowrapMode = TextServer.AutowrapMode.Word;
            vbox.AddChild(hint);

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.CustomMinimumSize = new Vector2(0, dlgHeight * 0.55f);

            var costContainer = new VBoxContainer();
            costContainer.AddThemeConstantOverride("separation", 6);

            foreach (var terrain in ExtraSave.BuiltInTerrainTypes)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);

                var tLabel = new Label();
                tLabel.Text = terrain;
                tLabel.CustomMinimumSize = new Vector2(150, 24);
                tLabel.AddThemeFontSizeOverride("font_size", 11);
                row.AddChild(tLabel);

                var costLabel = new Label();
                int cost = extraSave?.GetMoveCost(moveTypeId, terrain) ?? -1;
                if (cost < 0)
                    costLabel.Text = TranslateGroup.T("内置逻辑");
                else
                    costLabel.Text = cost == 999 ? "∞ (不可移动)" : cost.ToString();
                costLabel.AddThemeFontSizeOverride("font_size", 11);
                costLabel.AddThemeColorOverride("font_color", cost < 0 ? Colors.Gray : (cost == 999 ? Colors.Red : Colors.Green));
                row.AddChild(costLabel);

                costContainer.AddChild(row);
            }

            scroll.AddChild(costContainer);
            vbox.AddChild(scroll);

            var createBtn = new Button();
            TranslateGroup.SetText(createBtn, "➕ 基于此创建自定义移动方式");
            createBtn.CustomMinimumSize = new Vector2(0, 44);
            createBtn.AddThemeFontSizeOverride("font_size", 13);
            var createStyle = new StyleBoxFlat();
            createStyle.BgColor = new Color(0.2f, 0.5f, 0.3f, 0.9f);
            createStyle.SetCornerRadiusAll(8);
            createBtn.AddThemeStyleboxOverride("normal", createStyle);
            createBtn.Pressed += () => {
                dialog.QueueFree();
                ShowNewMoveTypeDialogBasedOn(moveTypeId);
            };
            vbox.AddChild(createBtn);

            var closeBtn = new Button();
            TranslateGroup.SetText(closeBtn, "❌ 关闭");
            closeBtn.CustomMinimumSize = new Vector2(0, 40);
            closeBtn.AddThemeFontSizeOverride("font_size", 12);
            closeBtn.Pressed += () => dialog.QueueFree();
            vbox.AddChild(closeBtn);

            dialog.AddChild(vbox);
            AddChild(dialog);
            MakeMenuDraggable(dialog);
            GD.Print($"[CustomConfigUI] 内置移动方式消耗对话框已显示: {moveTypeId}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[CustomConfigUI] ShowBuiltInMoveTypeCosts 异常: {e.Message}\n{e.StackTrace}");
            ShowToast(TranslateGroup.T("❌ 打开消耗面板失败"));
        }
    }

    private void ShowNewMoveTypeDialogBasedOn(string baseMoveTypeId)
    {
        ShowNewMoveTypeDialog();
        CallDeferred(nameof(SetNewMoveTypeIdHint), baseMoveTypeId);
    }

    private void SetNewMoveTypeIdHint(string baseId)
    {
        GD.Print($"基于 {baseId} 创建自定义移动方式");
    }
}
