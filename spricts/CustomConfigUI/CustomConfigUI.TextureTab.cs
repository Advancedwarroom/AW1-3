using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

// CustomConfigUI · TextureTab（由 CustomConfigUI.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class CustomConfigUI
{
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
        TranslateGroup.SetText(permBtn, "📱 请求相册访问权限");
        permBtn.CustomMinimumSize = new Vector2(0, 40);
        permBtn.AddThemeFontSizeOverride("font_size", 14);
        var permStyle = new StyleBoxFlat();
        permStyle.BgColor = new Color(0.3f, 0.4f, 0.6f, 0.9f);
        permStyle.SetCornerRadiusAll(8);
        permBtn.AddThemeStyleboxOverride("normal", permStyle);
        permBtn.Pressed += () => {
            ExtraSave.RequestGalleryPermission();
            ShowToast(TranslateGroup.T("权限请求已发送"));
        };
        container.AddChild(permBtn);

        var permStatus = new Label();
        permStatus.Text = $"当前权限状态: {(ExtraSave.HasGalleryPermission() ? TranslateGroup.T("✅ 已授权") : TranslateGroup.T("❌ 未授权"))}";
        permStatus.AddThemeFontSizeOverride("font_size", 12);
        permStatus.AddThemeColorOverride("font_color", ExtraSave.HasGalleryPermission() ? new Color(0.3f, 1, 0.3f) : new Color(1, 0.3f, 0.3f));
        container.AddChild(permStatus);

        // ✅ 剪贴板备用方案（手机权限问题时的替代）
        var clipboardBtn = new Button();
        TranslateGroup.SetText(clipboardBtn, "📋 从剪贴板粘贴图片");
        clipboardBtn.CustomMinimumSize = new Vector2(0, 40);
        clipboardBtn.AddThemeFontSizeOverride("font_size", 13);
        var cbStyle = new StyleBoxFlat();
        cbStyle.BgColor = new Color(0.4f, 0.5f, 0.3f, 0.9f);
        cbStyle.SetCornerRadiusAll(8);
        clipboardBtn.AddThemeStyleboxOverride("normal", cbStyle);
        clipboardBtn.Pressed += ShowClipboardCategoryDialog;
        container.AddChild(clipboardBtn);

        container.AddChild(new HSeparator());

        // 上传说明
        var infoLabel = new Label();
        TranslateGroup.SetText(infoLabel, "📋 上传要求:\n• 尺寸: 32×32 像素\n• 格式: PNG / JPG / GIF\n• 非标准尺寸将被拒绝\n• 权限问题请用剪贴板粘贴");
        infoLabel.AddThemeFontSizeOverride("font_size", 12);
        infoLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.6f));
        container.AddChild(infoLabel);

        // 分类上传
        var categories = new[] { TranslateGroup.T("单位"), TranslateGroup.T("兵器"), TranslateGroup.T("设施"), TranslateGroup.T("地形"), TranslateGroup.T("移动方式") };
        foreach (var category in categories)
        {
            var catBtn = new Button();
            catBtn.Text = $"🎨 {TranslateGroup.T("上传")}{category}{TranslateGroup.T("贴图")}";
            catBtn.CustomMinimumSize = new Vector2(0, 40);
            catBtn.AddThemeFontSizeOverride("font_size", 13);
            catBtn.Pressed += () => ShowTextureUploadDialog(category);
            container.AddChild(catBtn);
        }

        // 已上传贴图列表
        container.AddChild(new HSeparator());

        var uploadedLabel = new Label();
        TranslateGroup.SetText(uploadedLabel, "=== 已上传贴图 ===");
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
        TranslateGroup.SetText(applyLabel, "=== 应用到现有对象 ===");
        applyLabel.AddThemeFontSizeOverride("font_size", 14);
        applyLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        container.AddChild(applyLabel);

        var applyHint = new Label();
        TranslateGroup.SetText(applyHint, "选择已上传贴图，然后点击地图上的对象进行替换");
        applyHint.AddThemeFontSizeOverride("font_size", 11);
        applyHint.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        container.AddChild(applyHint);

        // 选择贴图
        var textureSelectHBox = new HBoxContainer();
        textureSelectHBox.AddThemeConstantOverride("separation", 8);

        var textureSelectLabel = new Label();
        TranslateGroup.SetText(textureSelectLabel, "选择贴图:");
        textureSelectLabel.CustomMinimumSize = new Vector2(80, 28);
        textureSelectHBox.AddChild(textureSelectLabel);

        applyTextureSelect = new OptionButton();
        applyTextureSelect.Name = "ApplyTextureSelect";
        applyTextureSelect.CustomMinimumSize = new Vector2(180, 28);
        applyTextureSelect.AddThemeFontSizeOverride("font_size", 11);
        textureSelectHBox.AddChild(applyTextureSelect);

        // 选择目标类型
        targetTypeSelect = new OptionButton();
        targetTypeSelect.Name = "TargetTypeSelect";
        targetTypeSelect.AddItem(TranslateGroup.T("单位"), 0);
        targetTypeSelect.AddItem(TranslateGroup.T("兵器"), 1);
        targetTypeSelect.AddItem(TranslateGroup.T("设施"), 2);
        targetTypeSelect.AddItem(TranslateGroup.T("地形"), 3);
        targetTypeSelect.AddItem(TranslateGroup.T("移动方式"), 4);
        targetTypeSelect.CustomMinimumSize = new Vector2(100, 28);
        targetTypeSelect.AddThemeFontSizeOverride("font_size", 11);
        textureSelectHBox.AddChild(targetTypeSelect);

        container.AddChild(textureSelectHBox);

        // 应用按钮
        var applyTextureBtn = new Button();
        TranslateGroup.SetText(applyTextureBtn, "🖱️ 进入贴图替换模式");
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

    // ✅ 移动端文件选择回调存储
    private string _pendingTextureCategory = "";

    private void ShowTextureUploadDialog(string category)
    {
        // ✅ 移动端：使用系统原生文件对话框，避免 FileDialog 在手机上闪退
        if (OS.GetName() == "Android" || OS.GetName() == "iOS")
        {
            _pendingTextureCategory = category;
            if (!ExtraSave.HasGalleryPermission())
            {
                ExtraSave.RequestGalleryPermission();
                ShowToast(TranslateGroup.T("权限请求已发送，请允许后重试"));
                return;
            }

            var filters = new string[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp" };
            DisplayServer.FileDialogShow(
                $"{TranslateGroup.T("选择")}{category}{TranslateGroup.T("贴图")}",
                OS.GetSystemDir(OS.SystemDir.Pictures),
                "",
                false,
                DisplayServer.FileDialogMode.OpenFile,
                filters,
                new Callable(this, nameof(OnMobileTextureFileSelected))
            );
            return;
        }

        // ✅ 桌面端：使用 Godot FileDialog
        var dialog = new FileDialog();
        dialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        dialog.Access = FileDialog.AccessEnum.Filesystem;
        dialog.Filters = new string[] { "*.png ; PNG Images", "*.jpg ; JPG Images", "*.jpeg ; JPEG Images", "*.gif ; GIF Images", "*.webp ; WebP Images" };
        dialog.Title = $"{TranslateGroup.T("选择")}{category}{TranslateGroup.T("贴图")} ({TranslateGroup.T("建议")}32×32)";
        dialog.Size = new Vector2I(800, 600);

        dialog.FileSelected += (path) => {
            if (string.IsNullOrEmpty(path)) { dialog.QueueFree(); return; }
            ProcessTextureUpload(path, category);
            dialog.QueueFree();
        };

        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void OnMobileTextureFileSelected(bool status, string[] selectedPaths)
    {
        if (!status || selectedPaths == null || selectedPaths.Length == 0) return;
        ProcessTextureUpload(selectedPaths[0], _pendingTextureCategory);
    }

    private void ProcessTextureUpload(string path, string category)
    {
        try
        {
            // 验证尺寸
            var image = new Image();
            Error err = image.Load(path);
            if (err != Error.Ok)
            {
                ShowToast(TranslateGroup.T("❌ 图片加载失败"));
                return;
            }

            int w = image.GetWidth();
            int h = image.GetHeight();
            if (w != h)
            {
                ShowToast($"❌ {TranslateGroup.T("尺寸")} {w}×{h} {TranslateGroup.T("不是正方形！")}\n{TranslateGroup.T("请裁剪为正方形后重新上传")}");
                return;
            }
            if (w != 32 || h != 32)
            {
                image.Resize(32, 32, Image.Interpolation.Lanczos);
                ShowToast($"⚠️ {TranslateGroup.T("尺寸")} {w}×{h} {TranslateGroup.T("非标准，已自动")}resize{TranslateGroup.T("到")} 32×32");
            }

            string tempPath = path;
            if (w != 32 || h != 32)
            {
                tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"resized_{Guid.NewGuid()}.png");
                image.SavePng(tempPath);
            }

            string savedPath = extraSave?.ProcessUploadedTexture(tempPath, category, $"custom_{DateTime.Now:yyyyMMddHHmmss}");
            if (!string.IsNullOrEmpty(savedPath) && !savedPath.StartsWith("ERROR"))
            {
                ShowToast($"✅ {TranslateGroup.T("贴图上传成功！")}\n{Path.GetFileName(savedPath)}");
                RefreshUploadedTextureList();
                RefreshApplyTextureList();
            }
            else
            {
                string errorMsg = savedPath switch
                {
                    "ERROR_NOT_SQUARE" => TranslateGroup.T("❌ 贴图不是正方形，请裁剪后重新上传"),
                    "ERROR_TOO_SMALL" => TranslateGroup.T("❌ 贴图尺寸过小，无法resize"),
                    "ERROR_LOAD_FAILED" => TranslateGroup.T("❌ 贴图加载失败"),
                    _ => TranslateGroup.T("❌ 贴图保存失败")
                };
                ShowToast(errorMsg);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[CustomConfigUI] {TranslateGroup.T("贴图处理异常")}: {e.Message}");
            ShowToast(TranslateGroup.T("❌ 贴图处理失败"));
        }
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
        title.Text = $"🎨 {TranslateGroup.T("为")} {targetId} {TranslateGroup.T("应用贴图")}";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", Colors.Yellow);
        vbox.AddChild(title);

        var hint = new Label();
        TranslateGroup.SetText(hint, "选择已上传的贴图文件，或上传新贴图");
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        vbox.AddChild(hint);

        // 已上传贴图列表
        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(0, 150);

        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 4);

        string textureDir = GodotPathCombine(ExtraSave.CUSTOM_TEXTURE_DIR, category);
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
                    TranslateGroup.SetText(applyBtn, "应用");
                    applyBtn.CustomMinimumSize = new Vector2(60, 24);
                    applyBtn.AddThemeFontSizeOverride("font_size", 10);
                    string fullPath = GodotPathCombine(textureDir, fileName);
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
            TranslateGroup.SetText(empty, "暂无上传的贴图");
            empty.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            list.AddChild(empty);
        }

        scroll.AddChild(list);
        vbox.AddChild(scroll);

        var uploadBtn = new Button();
        TranslateGroup.SetText(uploadBtn, "📤 上传新贴图");
        uploadBtn.CustomMinimumSize = new Vector2(200, 36);
        uploadBtn.Pressed += () => {
            dialog.QueueFree();
            ShowTextureUploadDialog(category);
        };
        vbox.AddChild(uploadBtn);

        var cancelBtn = new Button();
        TranslateGroup.SetText(cancelBtn, "❌ 取消");
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
            if (category == TranslateGroup.T("单位"))
            {
                if (int.TryParse(targetId, out int uSlot))
                    success = extraSave?.ApplyCustomTextureToSavedUnit(uSlot, texturePath) ?? false;
            }
            else if (category == TranslateGroup.T("兵器"))
            {
                if (int.TryParse(targetId, out int wSlot))
                    success = extraSave?.ApplyCustomTextureToSavedWeapon(wSlot, texturePath) ?? false;
            }
            else if (category == TranslateGroup.T("设施"))
            {
                if (int.TryParse(targetId, out int fSlot))
                    success = extraSave?.ApplyCustomTextureToSavedFacility(fSlot, texturePath) ?? false;
            }
            else if (category == TranslateGroup.T("地形"))
            {
                if (int.TryParse(targetId, out int gSlot))
                    success = extraSave?.ApplyCustomTextureToSavedGrid(gSlot, texturePath) ?? false;
                else
                    success = extraSave?.ApplyCustomTextureToTerrainType(targetId, texturePath) ?? false;
            }
            else if (category == TranslateGroup.T("移动方式"))
            {
                success = extraSave?.ApplyCustomTextureToMoveType(targetId, texturePath) ?? false;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[CustomConfigUI] {TranslateGroup.T("贴图应用异常")}: {e.Message}");
        }

        ShowToast(success ? TranslateGroup.T("✅ 贴图应用成功！") : TranslateGroup.T("❌ 贴图应用失败"));

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

            if (category == TranslateGroup.T("单位"))
            {
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
            }
            else if (category == TranslateGroup.T("兵器"))
            {
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
            }
            else if (category == TranslateGroup.T("地形"))
            {
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
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[CustomConfigUI] {TranslateGroup.T("刷新运行时贴图失败")}: {e.Message}");
        }
    }

    private void RefreshUploadedTextureList()
    {
        var list = textureUploadPanel?.GetNodeOrNull<VBoxContainer>("UploadedTextureList");
        if (list == null) return;

        foreach (var child in list.GetChildren())
            child.QueueFree();

        // ✅ 从文件系统读取
        string textureDir = ExtraSave.CUSTOM_TEXTURE_DIR;
        if (DirAccess.DirExistsAbsolute(textureDir))
        {
            var dir = DirAccess.Open(textureDir);
            if (dir != null)
            {
                dir.ListDirBegin();
                string folderName;
                while ((folderName = dir.GetNext()) != "")
                {
                    if (dir.CurrentIsDir() && folderName != "." && folderName != "..")
                    {
                        var subDir = DirAccess.Open(GodotPathCombine(textureDir, folderName));
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
                            TranslateGroup.SetText(previewBtn, "预览");
                            previewBtn.CustomMinimumSize = new Vector2(60, 24);
                            previewBtn.AddThemeFontSizeOverride("font_size", 10);
                            string fullPath = GodotPathCombine(textureDir, folderName, fileName);
                            previewBtn.Pressed += () => ShowTexturePreview(fullPath);
                            row.AddChild(previewBtn);

                            var delBtn = new Button();
                            TranslateGroup.SetText(delBtn, "删除");
                            delBtn.CustomMinimumSize = new Vector2(60, 24);
                            delBtn.AddThemeFontSizeOverride("font_size", 10);
                            delBtn.AddThemeColorOverride("font_color", new Color(1, 0.4f, 0.4f));
                            delBtn.Pressed += () => {
                                try { DirAccess.RemoveAbsolute(fullPath); } catch (System.Exception ex) { GD.PushWarning($"[CustomConfigUI] {ex.GetType().Name}: {ex.Message}"); }
                                RefreshUploadedTextureList();
                                RefreshApplyTextureList();
                            };
                            row.AddChild(delBtn);

                            list.AddChild(row);
                        }
                        subDir.ListDirEnd();
                    }
                }
                dir.ListDirEnd();
            }
        }

        if (list.GetChildCount() == 0)
        {
            var emptyLabel = new Label();
            TranslateGroup.SetText(emptyLabel, "暂无上传的贴图");
            emptyLabel.AddThemeFontSizeOverride("font_size", 12);
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            list.AddChild(emptyLabel);
        }
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

    // ✅ 贴图应用模式
    private void StartTextureApplyMode()
    {
        var textureOption = applyTextureSelect;
        var targetTypeOption = this.targetTypeSelect;

        if (textureOption == null || targetTypeOption == null || !IsInstanceValid(textureOption) || !IsInstanceValid(targetTypeOption))
        {
            ShowToast(TranslateGroup.T("❌ UI组件未初始化，请重新打开面板"));
            return;
        }
        if (textureOption.Selected < 0 || textureOption.Selected >= textureOption.ItemCount) 
        {
            ShowToast(TranslateGroup.T("❌ 请先选择贴图"));
            return;
        }

        // ✅ 从metadata读取贴图路径
        string texturePath = "";
        var metadata = textureOption.GetItemMetadata(textureOption.Selected);
        if (metadata.VariantType != Variant.Type.Nil)
        {
            texturePath = metadata.AsString();
        }

        if (string.IsNullOrEmpty(texturePath))
        {
            ShowToast(TranslateGroup.T("❌ 贴图路径无效"));
            return;
        }

        pendingApplyTexturePath = texturePath;
        pendingApplyTargetType = targetTypeOption.GetItemText(targetTypeOption.Selected);

        if (string.IsNullOrEmpty(pendingApplyTexturePath))
        {
            ShowToast(TranslateGroup.T("❌ 贴图路径为空"));
            return;
        }
        if (!Godot.FileAccess.FileExists(pendingApplyTexturePath))
        {
            ShowToast(TranslateGroup.T("❌ 贴图文件不存在"));
            return;
        }

        if (terrainEditor != null)
        {
            terrainEditor.SetTextureApplyMode(pendingApplyTexturePath, pendingApplyTargetType);
            ShowToast($"🖱️ {TranslateGroup.T("点击地图上的")}{pendingApplyTargetType}{TranslateGroup.T("替换贴图，")}ESC{TranslateGroup.T("取消")}");
            Hide();
        }
        else
        {
            ShowToast(TranslateGroup.T("❌ TerrainEditor未连接"));
        }
    }

    private void RefreshApplyTextureList()
    {
        var textureOption = applyTextureSelect;
        if (textureOption == null || !IsInstanceValid(textureOption)) return;

        textureOption.Clear();

        // ✅ 从文件系统读取
        string textureDir = ExtraSave.CUSTOM_TEXTURE_DIR;
        if (DirAccess.DirExistsAbsolute(textureDir))
        {
                var dir = DirAccess.Open(textureDir);
                if (dir != null)
                {
                    dir.ListDirBegin();
                    string folderName;
                    while ((folderName = dir.GetNext()) != "")
                    {
                        if (dir.CurrentIsDir() && folderName != "." && folderName != "..")
                        {
                            var subDir = DirAccess.Open(GodotPathCombine(textureDir, folderName));
                            if (subDir == null) continue;

                            subDir.ListDirBegin();
                            string fileName;
                            while ((fileName = subDir.GetNext()) != "")
                            {
                                if (subDir.CurrentIsDir()) continue;
                                string fullPath = GodotPathCombine(textureDir, folderName, fileName);
                                int idx = textureOption.ItemCount;
                                textureOption.AddItem($"[{folderName}] {fileName}", idx);
                                textureOption.SetItemMetadata(idx, fullPath);
                            }
                            subDir.ListDirEnd();
                        }
                    }
                    dir.ListDirEnd();
                }
            }
    }

    // ==================== 剪贴板贴图粘贴 ====================

    private void ShowClipboardCategoryDialog()
    {
        var dialog = new Panel();
        dialog.CustomMinimumSize = new Vector2(350, 320);
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
        TranslateGroup.SetText(title, "📋 选择粘贴分类");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", Colors.Yellow);
        vbox.AddChild(title);

        var hint = new Label();
        TranslateGroup.SetText(hint, "请选择剪贴板图片要保存到的分类");
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        vbox.AddChild(hint);

        var categories = new[] { TranslateGroup.T("单位"), TranslateGroup.T("兵器"), TranslateGroup.T("设施"), TranslateGroup.T("地形"), TranslateGroup.T("移动方式") };
        foreach (var cat in categories)
        {
            var btn = new Button();
            btn.Text = $"🎨 {cat}";
            btn.CustomMinimumSize = new Vector2(0, 36);
            btn.AddThemeFontSizeOverride("font_size", 12);
            btn.Pressed += () => {
                dialog.QueueFree();
                PasteTextureFromClipboard(cat);
            };
            vbox.AddChild(btn);
        }

        var cancelBtn = new Button();
        TranslateGroup.SetText(cancelBtn, "❌ 取消");
        cancelBtn.CustomMinimumSize = new Vector2(0, 36);
        cancelBtn.AddThemeFontSizeOverride("font_size", 12);
        cancelBtn.Pressed += () => dialog.QueueFree();
        vbox.AddChild(cancelBtn);

        dialog.AddChild(vbox);
        AddChild(dialog);
        MakeMenuDraggable(dialog);
    }

    private void PasteTextureFromClipboard(string category)
    {
        try
        {
            var image = DisplayServer.ClipboardGetImage();
            if (image == null || image.IsEmpty())
            {
                ShowToast(TranslateGroup.T("❌ 剪贴板中没有图片，请先复制图片"));
                return;
            }

            int w = image.GetWidth();
            int h = image.GetHeight();
            if (w != h)
            {
                ShowToast($"❌ {TranslateGroup.T("尺寸")} {w}×{h} {TranslateGroup.T("不是正方形！")}\n{TranslateGroup.T("请裁剪为正方形后重新复制")}");
                return;
            }
            if (w != 32 || h != 32)
            {
                image.Resize(32, 32, Image.Interpolation.Lanczos);
                ShowToast($"⚠️ {TranslateGroup.T("尺寸")} {w}×{h} {TranslateGroup.T("非标准，已自动")}resize{TranslateGroup.T("到")} 32×32");
            }

            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"clipboard_{Guid.NewGuid()}.png");
            image.SavePng(tempPath);

            string savedPath = extraSave?.ProcessUploadedTexture(tempPath, category, $"clipboard_{DateTime.Now:yyyyMMddHHmmss}");
            if (!string.IsNullOrEmpty(savedPath) && !savedPath.StartsWith("ERROR"))
            {
                ShowToast($"✅ {TranslateGroup.T("剪贴板贴图保存成功！")}\n{System.IO.Path.GetFileName(savedPath)}");
                RefreshUploadedTextureList();
                RefreshApplyTextureList();
            }
            else
            {
                string errorMsg = savedPath switch
                {
                    "ERROR_NOT_SQUARE" => TranslateGroup.T("❌ 贴图不是正方形，请裁剪后重新复制"),
                    "ERROR_TOO_SMALL" => TranslateGroup.T("❌ 贴图尺寸过小，无法resize"),
                    "ERROR_LOAD_FAILED" => TranslateGroup.T("❌ 贴图加载失败"),
                    _ => TranslateGroup.T("❌ 贴图保存失败")
                };
                ShowToast(errorMsg);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[CustomConfigUI] {TranslateGroup.T("剪贴板粘贴异常")}: {e.Message}");
            ShowToast(TranslateGroup.T("❌ 剪贴板读取失败，可能当前平台不支持"));
        }
    }
}
