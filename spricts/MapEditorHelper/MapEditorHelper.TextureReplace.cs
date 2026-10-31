// MapEditorHelper.cs - 编辑器大更新：图形填充、区域填充、地形填充、覆盖全图、选区复制粘贴（矩形+不规则）、对称放置
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// MapEditorHelper · TextureReplace（由 MapEditorHelper.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class MapEditorHelper
{

    // ========== 贴图替换 ==========
    private void OpenTextureReplaceDialog()
    {
        // ✅ 使用 Panel 替代 AcceptDialog，彻底避免 exclusive 窗口冲突
        var dialog = new Panel();
        dialog.Name = "TextureReplaceDialog";
        var viewportSize = GetViewport().GetVisibleRect().Size;
        float dlgW = Mathf.Min(450, viewportSize.X * 0.9f);
        float dlgH = Mathf.Min(400, viewportSize.Y * 0.7f);
        dialog.CustomMinimumSize = new Vector2(dlgW, dlgH);
        dialog.SetAnchorsPreset(Control.LayoutPreset.Center);
        dialog.ZIndex = 1600;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.1f, 0.12f, 0.18f, 0.98f);
        style.SetCornerRadiusAll(12);
        style.SetBorderWidthAll(2);
        style.BorderColor = new Color(0.4f, 0.7f, 0.9f);
        dialog.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("margin_left", 15);
        vbox.AddThemeConstantOverride("margin_right", 15);
        vbox.AddThemeConstantOverride("margin_top", 15);
        vbox.AddThemeConstantOverride("margin_bottom", 15);
        vbox.AddThemeConstantOverride("separation", 10);
        dialog.AddChild(vbox);

        // 标题栏
        var titleBar = new HBoxContainer();
        titleBar.AddThemeConstantOverride("separation", 8);

        var titleLabel = new Label();
        titleLabel.Text = TranslateGroup.T("🖼️ 贴图替换");
        titleLabel.AddThemeFontSizeOverride("font_size", 16);
        titleLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleBar.AddChild(titleLabel);

        var closeBtn = new Button();
        closeBtn.Text = "✕";
        closeBtn.CustomMinimumSize = new Vector2(32, 32);
        closeBtn.Pressed += () => dialog.QueueFree();
        titleBar.AddChild(closeBtn);

        vbox.AddChild(titleBar);

        var hintLabel = new Label();
        TranslateGroup.SetText(hintLabel, "点击地图上的单位/兵器/设施来替换其贴图");
        hintLabel.AddThemeFontSizeOverride("font_size", 11);
        hintLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        vbox.AddChild(hintLabel);

        var fileBtn = new Button();
        TranslateGroup.SetText(fileBtn, "📁 选择贴图文件...");
        fileBtn.CustomMinimumSize = new Vector2(0, 40);
        fileBtn.AddThemeFontSizeOverride("font_size", 13);
        var fileStyle = new StyleBoxFlat();
        fileStyle.BgColor = new Color(0.3f, 0.5f, 0.7f, 0.9f);
        fileStyle.SetCornerRadiusAll(8);
        fileBtn.AddThemeStyleboxOverride("normal", fileStyle);
        fileBtn.Pressed += () =>
        {
            dialog.Hide();
            var fileDialog = new FileDialog();
            fileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
            fileDialog.Access = FileDialog.AccessEnum.Filesystem;
            fileDialog.AddFilter("*.png", "PNG Images");
            fileDialog.AddFilter("*.jpg,*.jpeg", "JPEG Images");
            fileDialog.AddFilter("*.webp", "WEBP Images");
            fileDialog.AddFilter("*.gif", "GIF Images");
            fileDialog.FileSelected += (fpath) =>
            {
                StartTextureReplaceMode(fpath);
                fileDialog.QueueFree();
                dialog.QueueFree();
            };
            fileDialog.Canceled += () =>
            {
                fileDialog.QueueFree();
                dialog.Show();
            };
            AddChild(fileDialog);
            fileDialog.PopupCentered(new Vector2I(600, 400));
        };
        vbox.AddChild(fileBtn);

        var typeLabel = new Label();
        TranslateGroup.SetText(typeLabel, "或选择要替换的类型:");
        typeLabel.AddThemeFontSizeOverride("font_size", 12);
        vbox.AddChild(typeLabel);

        var typeGrid = new GridContainer();
        typeGrid.Columns = 2;
        typeGrid.AddThemeConstantOverride("h_separation", 8);
        typeGrid.AddThemeConstantOverride("v_separation", 8);
        vbox.AddChild(typeGrid);

        string[] types = { "Infantry", "Weapon", "Facility", "Grid" };
        foreach (var t in types)
        {
            var btn = new Button();
            btn.Text = t;
            btn.CustomMinimumSize = new Vector2(100, 36);
            btn.Pressed += () => { StartTextureReplaceModeForType(t); dialog.QueueFree(); };
            typeGrid.AddChild(btn);
        }

        AddChild(dialog);
    }

    public string pendingTexturePath = "";
    private string pendingTextureTargetType = "";

    private void StartTextureReplaceMode(string texturePath)
    {
        pendingTexturePath = texturePath;
        terrainEditor?.SetTextureApplyMode(texturePath, "");
        UpdateStatusHint(TranslateGroup.T("贴图替换: 点击目标单位/兵器/设施来应用贴图"));
    }

    private void StartTextureReplaceModeForType(string targetType)
    {
        pendingTextureTargetType = targetType;
        terrainEditor?.SetTextureApplyMode("", targetType);
        UpdateStatusHint($"{TranslateGroup.T("贴图替换模式")}: {TranslateGroup.T("选择贴图文件，然后点击地图上的")} {targetType} {TranslateGroup.T("来替换")}");
    }

    public void OnEntityClickForTextureReplace(Node entity)
    {
        if (string.IsNullOrEmpty(pendingTexturePath)) return;

        // ✅ 使用TerrainEditor的纹理加载方法（支持GIF/PNG/JPG/WebP）
        var texture = terrainEditor?.LoadTextureFromPath(pendingTexturePath);
        if (texture == null)
        {
            UpdateStatusHint(TranslateGroup.T("贴图加载失败！"));
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
            // ✅ 通用：任何有 animSprite 的单位
            if (inf.animSprite != null && IsInstanceValid(inf.animSprite))
            {
                string animName = inf.GetIdleAnimName();
                if (!string.IsNullOrEmpty(animName))
                    terrainEditor?.ApplyFramesToAnimSprite(inf.animSprite, gifFrames ?? new List<Texture2D>{texture}, animName);
            }
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
        UpdateStatusHint(TranslateGroup.T("贴图已替换！"));
        pendingTexturePath = "";
    }
}
