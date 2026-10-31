using Godot;
using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;

// TerrainEditor · Texture（由 TerrainEditor.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class TerrainEditor
{
    // ==================== 贴图应用模式 ====================

    public void SetTextureApplyMode(string texturePath, string targetType)
    {
        isTextureApplyMode = true;
        pendingTextureApplyPath = texturePath;
        pendingTextureApplyTargetType = targetType;

        spawnHintLabel.Text = $"🖱️ {TranslateGroup.T("贴图替换模式：点击地图上的")}{targetType}\nESC{TranslateGroup.T("取消")}";
        spawnHintLabel.AddThemeColorOverride("font_color", new Color(0.7f, 1f, 0.4f));

        GD.Print($"[TerrainEditor] {TranslateGroup.T("进入贴图替换模式")}: {targetType} -> {texturePath}");
    }

    /// <summary>执行贴图替换到运行时实例（支持GIF/PNG/JPG/WebP）</summary>
    private void ExecuteTextureApply(Grids grid)
    {
        if (!isTextureApplyMode || string.IsNullOrEmpty(pendingTextureApplyPath)) return;

        try
        {
            var texture = LoadTextureFromPath(pendingTextureApplyPath);
            if (texture == null)
            {
                TranslateGroup.SetText(spawnHintLabel, "❌ 贴图加载失败（格式不支持或文件损坏）");
                return;
            }

            bool isGif = pendingTextureApplyPath.ToLower().EndsWith(".gif");
            List<Texture2D> gifFrames = null;
            if (isGif)
            {
                gifFrames = TryExtractGifFrames(pendingTextureApplyPath);
                spawnHintLabel.Text = gifFrames != null && gifFrames.Count > 1
                    ? $"🎞️ {TranslateGroup.T("GIF已加载")} {gifFrames.Count} 帧"
                    : TranslateGroup.T("⚠️ GIF只加载了第一帧（建议预转为PNG序列）");
            }

            if (pendingTextureApplyTargetType == TranslateGroup.T("单位"))
            {
                foreach (var unit in grid.infantries.ToList())
                {
                    if (unit?.sprite != null)
                    {
                        unit.sprite.Texture = texture;
                        spawnHintLabel.Text = $"✅ {TranslateGroup.T("已替换")} {unit.Name} {TranslateGroup.T("贴图")}";
                    }
                    // ✅ 通用：任何有 animSprite 的单位
                    if (unit.animSprite != null && IsInstanceValid(unit.animSprite))
                    {
                        string animName = unit.GetIdleAnimName();
                        if (!string.IsNullOrEmpty(animName))
                        {
                            ApplyFramesToAnimSprite(unit.animSprite, gifFrames ?? new List<Texture2D>{texture}, animName);
                            spawnHintLabel.Text = $"✅ {TranslateGroup.T("已替换")} {unit.Name} {TranslateGroup.T("动画贴图")}";
                        }
                    }
                }
            }
            else if (pendingTextureApplyTargetType == TranslateGroup.T("兵器"))
            {
                foreach (var weapon in grid.weapons.ToList())
                {
                    if (weapon?.GetNodeOrNull<Sprite2D>("Sprite2D") != null)
                    {
                        weapon.GetNode<Sprite2D>("Sprite2D").Texture = texture;
                        spawnHintLabel.Text = $"✅ {TranslateGroup.T("已替换")} {weapon.Name} {TranslateGroup.T("贴图")}";
                    }
                    else if (weapon?.animSprite != null)
                    {
                        var frames = new SpriteFrames();
                        frames.AddAnimation("default");
                        frames.AddFrame("default", texture);
                        weapon.animSprite.SpriteFrames = frames;
                        weapon.animSprite.Play("default");
                        spawnHintLabel.Text = $"✅ {TranslateGroup.T("已替换")} {weapon.Name} {TranslateGroup.T("贴图")}";
                    }
                }
            }
            else if (pendingTextureApplyTargetType == TranslateGroup.T("设施"))
            {
                if (grid.city != null)
                {
                    var facSprite = grid.city.GetNodeOrNull<Sprite2D>("Sprite2D");
                    if (facSprite != null) facSprite.Texture = texture;
                    spawnHintLabel.Text = $"✅ {TranslateGroup.T("已替换设施贴图")}";
                }
            }
            else if (pendingTextureApplyTargetType == TranslateGroup.T("地形"))
            {
                var terrainSprite = grid.GetNodeOrNull<Sprite2D>("Sprite2D");
                if (terrainSprite != null)
                {
                    terrainSprite.Texture = texture;
                    TranslateGroup.SetText(spawnHintLabel, "✅ 已替换地形贴图");
                }
            }
            else if (pendingTextureApplyTargetType == TranslateGroup.T("移动方式"))
            {
                TranslateGroup.SetText(spawnHintLabel, "⚠️ 移动方式贴图请在CustomConfigUI中设置");
            }
        }
        catch (Exception e)
        {
            spawnHintLabel.Text = $"❌ {TranslateGroup.T("贴图替换失败")}: {e.Message}";
            GD.PrintErr($"[TerrainEditor] {TranslateGroup.T("贴图替换异常")}: {e.Message}");
        }

        isTextureApplyMode = false;
        pendingTextureApplyPath = "";
        pendingTextureApplyTargetType = "";
    }

    // ========== ✅ GIF/图片加载辅助方法 ==========
    public Texture2D LoadTextureFromPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var image = new Image();
        if (image.Load(path) == Error.Ok)
            return ImageTexture.CreateFromImage(image);
        return null;
    }

    /// <summary>尝试提取GIF所有帧。优先外部工具(ImageMagick/FFmpeg)，失败时回退到Godot原生加载第一帧</summary>
    public List<Texture2D> TryExtractGifFrames(string gifPath)
    {
        var frames = new List<Texture2D>();

        // ✅ 先准备备用帧：用Godot原生加载GIF第一帧
        Texture2D fallbackFrame = null;
        try
        {
            var img = new Image();
            if (img.Load(gifPath) == Error.Ok)
                fallbackFrame = ImageTexture.CreateFromImage(img);
        }
        catch (System.Exception ex) { GD.PushWarning($"[TerrainEditor] {ex.GetType().Name}: {ex.Message}"); }

        try
        {
            var tempDir = Path.Combine(OS.GetUserDataDir(), "temp_gif_frames");
            System.IO.Directory.CreateDirectory(tempDir);
            foreach (var f in System.IO.Directory.GetFiles(tempDir, "gif_frame_*.png"))
                System.IO.File.Delete(f);

            bool externalSuccess = false;

            // 尝试ImageMagick (magick convert)
            try
            {
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = "magick";
                proc.StartInfo.Arguments = $"convert \"{gifPath}\" \"{tempDir}/gif_frame_%03d.png\"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();
                proc.WaitForExit(5000);
                externalSuccess = proc.ExitCode == 0;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                externalSuccess = false; // magick命令不存在，继续尝试ffmpeg
            }

            if (!externalSuccess)
            {
                // 尝试FFmpeg
                try
                {
                    var proc = new System.Diagnostics.Process();
                    proc.StartInfo.FileName = "ffmpeg";
                    proc.StartInfo.Arguments = $"-i \"{gifPath}\" -vsync 0 \"{tempDir}/gif_frame_%03d.png\"";
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.RedirectStandardError = true;
                    proc.StartInfo.CreateNoWindow = true;
                    proc.Start();
                    proc.WaitForExit(5000);
                    externalSuccess = proc.ExitCode == 0;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    externalSuccess = false; // ffmpeg也不存在
                }
            }

            if (externalSuccess)
            {
                var frameFiles = System.IO.Directory.GetFiles(tempDir, "gif_frame_*.png").OrderBy(f => f).ToArray();
                foreach (var ff in frameFiles)
                {
                    var img = new Image();
                    if (img.Load(ff) == Error.Ok)
                        frames.Add(ImageTexture.CreateFromImage(img));
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[TerrainEditor] GIF{TranslateGroup.T("提取失败")}: {e.Message}");
        }

        // ✅ 外部工具失败时，至少返回Godot原生加载的第一帧
        if (frames.Count == 0 && fallbackFrame != null)
        {
            frames.Add(fallbackFrame);
            GD.Print($"[TerrainEditor] GIF{TranslateGroup.T("外部工具不可用，回退到")}Godot{TranslateGroup.T("原生第一帧")}: {gifPath}");
        }

        return frames.Count > 0 ? frames : null;
    }

    /// <summary>将帧列表应用到AnimatedSprite2D，创建default动画</summary>
    public void ApplyFramesToAnimSprite(AnimatedSprite2D animSprite, List<Texture2D> frames, string animName = "default")
    {
        if (animSprite == null || frames == null || frames.Count == 0) return;
        var sf = new SpriteFrames();
        sf.AddAnimation(animName);
#pragma warning disable CS0618
        sf.SetAnimationLoop(animName, true);
#pragma warning restore CS0618
        sf.SetAnimationSpeed(animName, 5.0);
        foreach (var tex in frames)
            sf.AddFrame(animName, tex);
        animSprite.SpriteFrames = sf;
        animSprite.Play(animName);
    }
}
