using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

// CustomConfigUI · SaveSlotTab（由 CustomConfigUI.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class CustomConfigUI
{
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
            TranslateGroup.SetText(errorLabel, "⚠️ ExtraSave 未初始化\n请检查场景中的 ExtraSave 节点");
            errorLabel.AddThemeFontSizeOverride("font_size", 14);
            errorLabel.AddThemeColorOverride("font_color", Colors.Red);
            container.AddChild(errorLabel);
            scroll.AddChild(container);
            panel.AddChild(scroll);
            return panel;
        }

        var data = extraSave.Data;

        // 单位槽位
        container.AddChild(CreateSlotSection(TranslateGroup.T("🚶 单位保存槽"), ExtraSave.MAX_SLOTS, (i) => extraSave.GetSavedUnit(i), data.favoriteUnitSlots ?? new List<string>(), TranslateGroup.T("单位")));

        // 兵器槽位
        container.AddChild(CreateSlotSection(TranslateGroup.T("⚔️ 兵器保存槽"), ExtraSave.MAX_SLOTS, (i) => extraSave.GetSavedWeapon(i), data.favoriteWeaponSlots ?? new List<string>(), TranslateGroup.T("兵器")));

        // 设施槽位
        container.AddChild(CreateSlotSection(TranslateGroup.T("🏙️ 设施保存槽"), ExtraSave.MAX_SLOTS, (i) => extraSave.GetSavedFacility(i), data.favoriteFacilitySlots ?? new List<string>(), TranslateGroup.T("设施")));

        // 地形槽位
        container.AddChild(CreateSlotSection(TranslateGroup.T("🗺️ 地形保存槽"), ExtraSave.MAX_SLOTS, (i) => extraSave.GetSavedGridTerrain(i), data.favoriteGridSlots ?? new List<string>(), TranslateGroup.T("地形")));

        // 地图槽位
        container.AddChild(CreateSlotSection(TranslateGroup.T("🌍 地图保存槽"), ExtraSave.MAX_MAP_SLOTS, (i) => extraSave.GetSavedMap(i), null, TranslateGroup.T("地图")));

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
        // 关闭已存在的操作面板
        var existing = GetNodeOrNull("SlotActionPanel");
        existing?.QueueFree();

        // 先声明 panel 变量，供 overlay lambda 捕获
        Panel panel = null;

        // 半透明遮罩（点击关闭）
        var overlay = new Panel();
        overlay.Name = "SlotActionOverlay";
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.ZIndex = 1599;
        overlay.MouseFilter = MouseFilterEnum.Stop;
        var overlayStyle = new StyleBoxFlat();
        overlayStyle.BgColor = new Color(0, 0, 0, 0.4f);
        overlay.AddThemeStyleboxOverride("panel", overlayStyle);
        overlay.GuiInput += (e) => {
            if (e is InputEventMouseButton btn && btn.Pressed && btn.ButtonIndex == MouseButton.Left)
            {
                overlay.QueueFree();
                panel?.QueueFree();
            }
        };
        AddChild(overlay);

        // 操作面板
        panel = new Panel();
        panel.Name = "SlotActionPanel";
        panel.CustomMinimumSize = new Vector2(280, 0);
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.ZIndex = 1600;
        panel.MouseFilter = MouseFilterEnum.Stop;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.12f, 0.14f, 0.2f, 0.98f);
        panelStyle.SetCornerRadiusAll(16);
        panelStyle.SetBorderWidthAll(2);
        panelStyle.BorderColor = new Color(0.5f, 0.7f, 0.9f);
        panel.AddThemeStyleboxOverride("panel", panelStyle);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("margin_left", 16);
        vbox.AddThemeConstantOverride("margin_right", 16);
        vbox.AddThemeConstantOverride("margin_top", 16);
        vbox.AddThemeConstantOverride("margin_bottom", 16);
        vbox.AddThemeConstantOverride("separation", 10);

        // 标题
        var title = new Label();
        title.Text = $"{category} {TranslateGroup.T("槽位")} {slotIndex + 1}";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", new Color(0.5f, 0.9f, 1f));
        vbox.AddChild(title);

        // 分隔线
        var sep = new HSeparator();
        vbox.AddChild(sep);

        // 保存按钮
        var saveBtn = new Button();
        saveBtn.Text = isEmpty ? $"💾 {TranslateGroup.T("保存")}{category}" : $"💾 {TranslateGroup.T("覆盖保存")}";
        saveBtn.CustomMinimumSize = new Vector2(0, 48);
        saveBtn.AddThemeFontSizeOverride("font_size", 14);
        var saveStyle = new StyleBoxFlat();
        saveStyle.BgColor = new Color(0.2f, 0.5f, 0.3f, 0.9f);
        saveStyle.SetCornerRadiusAll(10);
        saveBtn.AddThemeStyleboxOverride("normal", saveStyle);
        saveBtn.Pressed += () => {
            if (category == TranslateGroup.T("地图"))
            {
                if (editorSaveSlots != null)
                {
                    editorSaveSlots.SaveMapToSlot(slotIndex);
                    ShowToast($"✅ {TranslateGroup.T("地图已保存到槽位")}{slotIndex + 1}");
                }
            }
            else
            {
                if (editorSaveSlots != null)
                {
                    if (isEmpty)
                    {
                        editorSaveSlots.SaveToSlot(slotIndex, GetEditorSlotType(category));
                        ShowToast($"💾 {TranslateGroup.T("请点击地图上的")}{category}{TranslateGroup.T("来保存")}");
                    }
                    else
                    {
                        ShowToast(TranslateGroup.T("槽位已有数据，请先清除"));
                    }
                }
            }
            overlay.QueueFree();
            panel.QueueFree();
        };
        vbox.AddChild(saveBtn);

        // 加载按钮（空槽禁用）
        var loadBtn = new Button();
        loadBtn.Text = $"📂 {TranslateGroup.T("加载")}{category}";
        loadBtn.CustomMinimumSize = new Vector2(0, 48);
        loadBtn.AddThemeFontSizeOverride("font_size", 14);
        var loadStyle = new StyleBoxFlat();
        loadStyle.BgColor = isEmpty ? new Color(0.3f, 0.3f, 0.35f, 0.5f) : new Color(0.3f, 0.5f, 0.7f, 0.9f);
        loadStyle.SetCornerRadiusAll(10);
        loadBtn.AddThemeStyleboxOverride("normal", loadStyle);
        loadBtn.Disabled = isEmpty;
        loadBtn.Pressed += () => {
            if (category == TranslateGroup.T("地图"))
            {
                if (editorSaveSlots != null && !isEmpty)
                {
                    editorSaveSlots.LoadMapFromSlot(slotIndex);
                    ShowToast(TranslateGroup.T("✅ 地图加载完成"));
                }
            }
            else
            {
                if (editorSaveSlots != null && !isEmpty)
                {
                    editorSaveSlots.LoadFromSlot(slotIndex, GetEditorSlotType(category));
                    ShowToast($"📂 {TranslateGroup.T("请点击地图上的格子来加载")}{category}");
                }
            }
            overlay.QueueFree();
            panel.QueueFree();
        };
        vbox.AddChild(loadBtn);

        // 收藏按钮
        var favBtn = new Button();
        favBtn.Text = isFav ? TranslateGroup.T("★ 取消收藏") : TranslateGroup.T("☆ 收藏");
        favBtn.CustomMinimumSize = new Vector2(0, 44);
        favBtn.AddThemeFontSizeOverride("font_size", 13);
        var favStyle = new StyleBoxFlat();
        favStyle.BgColor = new Color(0.8f, 0.6f, 0.2f, 0.8f);
        favStyle.SetCornerRadiusAll(10);
        favBtn.AddThemeStyleboxOverride("normal", favStyle);
        favBtn.Pressed += () => {
            ToggleFavorite(slotIndex, category);
            overlay.QueueFree();
            panel.QueueFree();
        };
        vbox.AddChild(favBtn);

        // 改名按钮
        var renameBtn = new Button();
        TranslateGroup.SetText(renameBtn, "✏️ 改名");
        renameBtn.CustomMinimumSize = new Vector2(0, 44);
        renameBtn.AddThemeFontSizeOverride("font_size", 13);
        var renameStyle = new StyleBoxFlat();
        renameStyle.BgColor = new Color(0.4f, 0.4f, 0.5f, 0.8f);
        renameStyle.SetCornerRadiusAll(10);
        renameBtn.AddThemeStyleboxOverride("normal", renameStyle);
        renameBtn.Pressed += () => {
            ShowRenameDialog(slotIndex, category);
            overlay.QueueFree();
            panel.QueueFree();
        };
        vbox.AddChild(renameBtn);

        // 清除按钮（空槽禁用）
        if (!isEmpty)
        {
            var clearBtn = new Button();
            TranslateGroup.SetText(clearBtn, "🗑️ 清除");
            clearBtn.CustomMinimumSize = new Vector2(0, 44);
            clearBtn.AddThemeFontSizeOverride("font_size", 13);
            clearBtn.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
            var clearStyle = new StyleBoxFlat();
            clearStyle.BgColor = new Color(0.5f, 0.2f, 0.2f, 0.8f);
            clearStyle.SetCornerRadiusAll(10);
            clearBtn.AddThemeStyleboxOverride("normal", clearStyle);
            clearBtn.Pressed += () => {
                ClearSlot(slotIndex, category);
                overlay.QueueFree();
                panel.QueueFree();
            };
            vbox.AddChild(clearBtn);
        }

        // 关闭按钮
        var closeBtn = new Button();
        TranslateGroup.SetText(closeBtn, "❌ 关闭");
        closeBtn.CustomMinimumSize = new Vector2(0, 40);
        closeBtn.AddThemeFontSizeOverride("font_size", 12);
        var closeStyle = new StyleBoxFlat();
        closeStyle.BgColor = new Color(0.25f, 0.25f, 0.3f, 0.7f);
        closeStyle.SetCornerRadiusAll(8);
        closeBtn.AddThemeStyleboxOverride("normal", closeStyle);
        closeBtn.Pressed += () => {
            overlay.QueueFree();
            panel.QueueFree();
        };
        vbox.AddChild(closeBtn);

        panel.AddChild(vbox);
        AddChild(panel);
        MakeMenuDraggable(panel);
    }

private string GetEditorSlotType(string category)
{
    if (category == TranslateGroup.T("单位")) return "unit";
    if (category == TranslateGroup.T("兵器")) return "weapon";
    if (category == TranslateGroup.T("设施")) return "facility";
    if (category == TranslateGroup.T("地形")) return "grid";
    if (category == TranslateGroup.T("地图")) return "map";
    return category.ToLower();
}
    private void OnSlotDataChanged(string type)
    {
        RefreshSaveSlotTab();
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
        title.Text = $"💾 {TranslateGroup.T("保存")}{category}{TranslateGroup.T("到槽位")}{slotIndex + 1}";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", new Color(0.4f, 0.9f, 1f));
        vbox.AddChild(title);

        var nameHBox = new HBoxContainer();
        nameHBox.AddThemeConstantOverride("separation", 8);
        var nameLabel = new Label();
        TranslateGroup.SetText(nameLabel, $"自定义{TranslateGroup.T("名称")}:");
        nameHBox.AddChild(nameLabel);
        var nameEdit = new LineEdit();
        nameEdit.CustomMinimumSize = new Vector2(200, 28);
        nameEdit.PlaceholderText = TranslateGroup.T("留空使用默认名称");
        nameHBox.AddChild(nameEdit);
        vbox.AddChild(nameHBox);

        var btnHBox = new HBoxContainer();
        btnHBox.AddThemeConstantOverride("separation", 10);

        var confirmBtn = new Button();
        TranslateGroup.SetText(confirmBtn, "✅ 确认保存");
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
        TranslateGroup.SetText(cancelBtn, "❌ 取消");
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
            ShowToast(TranslateGroup.T("⚠️ 名称过长，已截断至20字符"));
        }

        if (category == TranslateGroup.T("单位"))
        {
            if (te?.selectedGrid != null && te.selectedGrid.infantries.Count > 0)
                            {
                                var unit = te.selectedGrid.infantries[0];
                                if (!string.IsNullOrEmpty(finalName)) unit.Name = finalName; // ✅ 应用名称到实例
                                extraSave?.SaveUnit(unit, slotIndex, finalName);
                                ShowToast($"✅ {TranslateGroup.T("单位已保存到槽位")}{slotIndex + 1}" + (finalName != null ? $" {TranslateGroup.T("名称")}:{finalName}" : ""));
                            }
                            else
                            {
                                ShowToast(TranslateGroup.T("❌ 请先选择一个单位"));
                            }
        }
        else if (category == TranslateGroup.T("兵器"))
        {
            if (te?.selectedGrid != null && te.selectedGrid.weapon != null)
                            {
                                var weapon = te.selectedGrid.weapon;
                                if (!string.IsNullOrEmpty(finalName)) weapon.Name = finalName;
                                extraSave?.SaveWeapon(weapon, slotIndex, finalName);
                                ShowToast($"✅ {TranslateGroup.T("兵器已保存到槽位")}{slotIndex + 1}" + (finalName != null ? $" {TranslateGroup.T("名称")}:{finalName}" : ""));
                            }
                            else
                            {
                                ShowToast(TranslateGroup.T("❌ 请先选择一个兵器"));
                            }
        }
        else if (category == TranslateGroup.T("设施"))
        {
            if (te?.selectedGrid != null && te.selectedGrid.city != null)
                            {
                                var facility = te.selectedGrid.city;
                                if (!string.IsNullOrEmpty(finalName)) facility.Name = finalName;
                                extraSave?.SaveFacility(facility, slotIndex, finalName);
                                ShowToast($"✅ {TranslateGroup.T("设施已保存到槽位")}{slotIndex + 1}" + (finalName != null ? $" {TranslateGroup.T("名称")}:{finalName}" : ""));
                            }
                            else
                            {
                                ShowToast(TranslateGroup.T("❌ 请先选择一个设施"));
                            }
        }
        else if (category == TranslateGroup.T("地形"))
        {
            if (te?.selectedGrid != null)
                            {
                                extraSave?.SaveGridTerrain(te.selectedGrid, slotIndex, finalName);
                                ShowToast($"✅ {TranslateGroup.T("地形已保存到槽位")}{slotIndex + 1}" + (finalName != null ? $" {TranslateGroup.T("名称")}:{finalName}" : ""));
                            }
                            else
                            {
                                ShowToast(TranslateGroup.T("❌ 请先选择一个格子"));
                            }
        }
        else if (category == TranslateGroup.T("地图"))
        {
            extraSave?.SaveCurrentMap(slotIndex, finalName, gameManager, te?.gridManager);
                            ShowToast($"✅ {TranslateGroup.T("地图已保存到槽位")}{slotIndex + 1}" + (finalName != null ? $" {TranslateGroup.T("名称")}:{finalName}" : ""));
        }

        RefreshSaveSlotTab();
    }

    private void ShowLoadDialog(int slotIndex, string category)
    {
        ShowToast($"{TranslateGroup.T("加载")}{category}{TranslateGroup.T("槽位")}{slotIndex + 1}...");

        var te = terrainEditor;
        if (te == null)
        {
            ShowToast(TranslateGroup.T("❌ TerrainEditor 未连接"));
            return;
        }

        if (category == TranslateGroup.T("单位"))
        {
            var unitData = extraSave?.GetSavedUnit(slotIndex);
                            if (unitData != null && !string.IsNullOrEmpty(unitData.scenePath))
                            {
                                te.SetSpawnMode(SpawnMode.Unit, unitData.scenePath, unitData.team);
                                ShowToast("✅ " + TranslateGroup.T("单位") + " \"" + unitData.displayName + "\" " + TranslateGroup.T("已准备，点击地图格子放置"));
                            }
                            else
                            {
                                ShowToast(TranslateGroup.T("❌ 该槽位没有保存的单位"));
                            }
        }
        else if (category == TranslateGroup.T("兵器"))
        {
            var weaponData = extraSave?.GetSavedWeapon(slotIndex);
                            if (weaponData != null && !string.IsNullOrEmpty(weaponData.scenePath))
                            {
                                te.SetSpawnMode(SpawnMode.Weapon, weaponData.scenePath, weaponData.team);
                                ShowToast("✅ " + TranslateGroup.T("兵器") + " \"" + weaponData.displayName + "\" " + TranslateGroup.T("已准备，点击地图格子放置"));
                            }
                            else
                            {
                                ShowToast(TranslateGroup.T("❌ 该槽位没有保存的兵器"));
                            }
        }
        else if (category == TranslateGroup.T("设施"))
        {
            var facilityData = extraSave?.GetSavedFacility(slotIndex);
                            if (facilityData != null && !string.IsNullOrEmpty(facilityData.scenePath))
                            {
                                te.SetSpawnMode(SpawnMode.Facility, facilityData.scenePath, facilityData.team);
                                ShowToast("✅ " + TranslateGroup.T("设施") + " \"" + facilityData.displayName + "\" " + TranslateGroup.T("已准备，点击地图格子放置"));
                            }
                            else
                            {
                                ShowToast(TranslateGroup.T("❌ 该槽位没有保存的设施"));
                            }
        }
        else if (category == TranslateGroup.T("地形"))
        {
            var gridData = extraSave?.GetSavedGridTerrain(slotIndex);
                            if (gridData != null && gridData.savedTimestamp > 0)
                            {
                                te.SetApplyTerrainMode(gridData);
                                ShowToast("✅ " + TranslateGroup.T("地形") + " \"" + gridData.displayName + "\" " + TranslateGroup.T("已准备，点击地图格子应用"));
                            }
                            else
                            {
                                ShowToast(TranslateGroup.T("❌ 该槽位没有保存的地形"));
                            }
        }
        else if (category == TranslateGroup.T("地图"))
        {
            var mapData = extraSave?.GetSavedMap(slotIndex);
                            if (mapData != null && mapData.width > 0)
                            {
                                bool success = extraSave?.LoadMapToGame(slotIndex, gameManager, te.gridManager, te) ?? false;
                                ShowToast(success ? "✅ " + TranslateGroup.T("地图") + " \"" + mapData.displayName + "\" " + TranslateGroup.T("加载成功！") : TranslateGroup.T("❌ 地图加载失败"));
                            }
                            else
                            {
                                ShowToast(TranslateGroup.T("❌ 该槽位没有保存的地图"));
                            }
        }
    }

    private void ToggleFavorite(int slotIndex, string category)
    {
        bool success = false;
        try
        {
            if (category == TranslateGroup.T("单位"))
            {
                success = extraSave?.ToggleUnitFavorite(slotIndex) ?? false; 
            }
            else if (category == TranslateGroup.T("兵器"))
            {
                success = extraSave?.ToggleWeaponFavorite(slotIndex) ?? false; 
            }
            else if (category == TranslateGroup.T("设施"))
            {
                success = extraSave?.ToggleFacilityFavorite(slotIndex) ?? false; 
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[CustomConfigUI] {TranslateGroup.T("收藏切换异常")}: {e.Message}");
        }

        if (success)
            ShowToast($"✅ {category}{TranslateGroup.T("槽位")}{slotIndex + 1} {TranslateGroup.T("收藏状态已切换")}");
        else
            ShowToast($"❌ {TranslateGroup.T("收藏失败（可能已达上限")}{ExtraSave.MAX_FAVORITES}{TranslateGroup.T("个）")}");

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
        title.Text = $"✏️ {TranslateGroup.T("重命名")}{category}{slotIndex + 1}";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(title);

        var nameEdit = new LineEdit();
        nameEdit.CustomMinimumSize = new Vector2(250, 28);
        nameEdit.PlaceholderText = TranslateGroup.T("输入新名称");
        vbox.AddChild(nameEdit);

        var btnHBox = new HBoxContainer();
        btnHBox.AddThemeConstantOverride("separation", 10);

        var confirmBtn = new Button();
        TranslateGroup.SetText(confirmBtn, "确认");
        confirmBtn.Pressed += () => {
            bool success = false;
            if (category == TranslateGroup.T("单位"))
            {
                success = extraSave?.RenameUnit(slotIndex, nameEdit.Text) ?? false; 
            }
            else if (category == TranslateGroup.T("兵器"))
            {
                success = extraSave?.RenameWeapon(slotIndex, nameEdit.Text) ?? false;
            }
            else if (category == TranslateGroup.T("设施"))
            {
                success = extraSave?.RenameFacility(slotIndex, nameEdit.Text) ?? false;
            }
            else if (category == TranslateGroup.T("地形"))
            {
                success = extraSave?.RenameGridTerrain(slotIndex, nameEdit.Text) ?? false; 
            }

            if (success)
            {
                ShowToast(TranslateGroup.T("✅ 重命名成功！"));
                RefreshSaveSlotTab();
            }
            else
            {
                ShowToast(TranslateGroup.T("❌ 重命名失败"));
            }
            dialog.QueueFree();
        };
        btnHBox.AddChild(confirmBtn);

        var cancelBtn = new Button();
        TranslateGroup.SetText(cancelBtn, "取消");
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
        if (category == TranslateGroup.T("单位"))
        {
            extraSave?.ClearUnitSlot(slotIndex);
        }
        else if (category == TranslateGroup.T("兵器"))
        {
            extraSave?.ClearWeaponSlot(slotIndex);
        }
        else if (category == TranslateGroup.T("设施"))
        {
            extraSave?.ClearFacilitySlot(slotIndex);
        }
        else if (category == TranslateGroup.T("地形"))
        {
            extraSave?.ClearGridSlot(slotIndex);
        }
        else if (category == TranslateGroup.T("地图"))
        {
            extraSave?.ClearMapSlot(slotIndex);
        }

        ShowToast($"✅ {category}{TranslateGroup.T("槽位")}{slotIndex + 1} {TranslateGroup.T("已清除")}");
        RefreshSaveSlotTab();
    }

    private void RefreshSaveSlotTab()
    {
        // 重新创建保存槽位面板，保持原索引位置
        var oldPanel = saveSlotPanel;
        int idx = tabContainer.GetTabIdxFromControl(oldPanel);
        saveSlotPanel = CreateSaveSlotTab();
        tabContainer.RemoveChild(oldPanel);
        oldPanel.QueueFree();
        tabContainer.AddChild(saveSlotPanel);
        if (idx >= 0)
            tabContainer.MoveChild(saveSlotPanel, idx);
        tabContainer.SetTabTitle(idx >= 0 ? idx : 3, TranslateGroup.T("💾 保存/加载"));
    }
}
