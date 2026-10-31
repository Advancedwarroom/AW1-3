using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// ExtraSave · Texture（由 ExtraSave.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class ExtraSave
{

    // ==================== 贴图上传 ====================

    /// <summary>
    /// 处理上传的贴图，验证尺寸并保存
    /// </summary>
    /// <summary>
    /// 处理上传的贴图，验证尺寸并自动resize到32x32，非正方形拒绝
    /// </summary>
    public string ProcessUploadedTexture(string sourcePath, string category, string slotName)
    {
        if (string.IsNullOrEmpty(sourcePath) || !Godot.FileAccess.FileExists(sourcePath))
            return null;

        try
        {
            // 加载图片验证尺寸
            var image = new Image();
            Error err = image.Load(sourcePath);
            if (err != Error.Ok)
            {
                GD.PrintErr($"[ExtraSave] {TranslateGroup.T("加载图片失败")}: {err}");
                return "ERROR_LOAD_FAILED";
            }

            // 验证尺寸 - 必须是正方形才能resize
            int w = image.GetWidth();
            int h = image.GetHeight();
            if (w != h)
            {
                GD.PrintErr($"[ExtraSave] {TranslateGroup.T("图片尺寸")} {w}×{h} {TranslateGroup.T("不是正方形，无法自动")}resize{TranslateGroup.T("到")} {MAX_TEXTURE_SIZE}×{MAX_TEXTURE_SIZE}");
                return "ERROR_NOT_SQUARE";
            }

            // 如果不是32x32，自动resize
            if (w != MAX_TEXTURE_SIZE || h != MAX_TEXTURE_SIZE)
            {
                if (w < 2 || h < 2)
                {
                    GD.PrintErr($"[ExtraSave] {TranslateGroup.T("图片尺寸")} {w}×{h} {TranslateGroup.T("过小，无法")}resize");
                    return "ERROR_TOO_SMALL";
                }
                image.Resize(MAX_TEXTURE_SIZE, MAX_TEXTURE_SIZE, Image.Interpolation.Nearest);
                GD.Print($"[ExtraSave] {TranslateGroup.T("图片已自动")}resize: {w}×{h} -> {MAX_TEXTURE_SIZE}×{MAX_TEXTURE_SIZE}");
            }

            // 生成目标路径
            string ext = Path.GetExtension(sourcePath).ToLower();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".gif")
            {
                GD.PrintErr($"[ExtraSave] {TranslateGroup.T("不支持的格式")}: {ext}");
                return null;
            }

            string targetDir = GodotPathCombine(CUSTOM_TEXTURE_DIR, category);
            EnsureDirectoryExists(targetDir);

            string safeSlotName = string.Join("_", slotName.Split(Path.GetInvalidFileNameChars()));
            string targetPath = GodotPathCombine(targetDir, $"{safeSlotName}{ext}");

            // 保存图片（如果是resize后的，保存resize后的版本）
            if (w != MAX_TEXTURE_SIZE || h != MAX_TEXTURE_SIZE)
            {
                // 保存resize后的PNG
                err = image.SavePng(targetPath);
                if (err != Error.Ok)
                {
                    GD.PrintErr($"[ExtraSave] {TranslateGroup.T("保存")}resize{TranslateGroup.T("后图片失败")}: {err}");
                    return null;
                }
            }
            else
            {
                // 原尺寸，直接复制
                using var sourceFile = Godot.FileAccess.Open(sourcePath, Godot.FileAccess.ModeFlags.Read);
                using var targetFile = Godot.FileAccess.Open(targetPath, Godot.FileAccess.ModeFlags.Write);
                var buffer = sourceFile.GetBuffer((long)sourceFile.GetLength());
                targetFile.StoreBuffer(buffer);
            }

            GD.Print($"[ExtraSave] {TranslateGroup.T("贴图已保存")}: {targetPath}");
            return targetPath;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ExtraSave] {TranslateGroup.T("处理贴图失败")}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 将贴图应用到指定槽位的保存数据（不直接应用到游戏内实例）
    /// </summary>
    public bool ApplyCustomTextureToSavedUnit(int slotIndex, string texturePath)
    {
        if (slotIndex < 0 || slotIndex >= Data.savedUnits.Count) return false;
        Data.savedUnits[slotIndex].customTexturePath = texturePath;
        Data.savedUnits[slotIndex].useCustomTexture = !string.IsNullOrEmpty(texturePath);
        Data.savedUnits[slotIndex].savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        SaveAllData();
        return true;
    }

    public bool ApplyCustomTextureToSavedWeapon(int slotIndex, string texturePath)
    {
        if (slotIndex < 0 || slotIndex >= Data.savedWeapons.Count) return false;
        Data.savedWeapons[slotIndex].customTexturePath = texturePath;
        Data.savedWeapons[slotIndex].useCustomTexture = !string.IsNullOrEmpty(texturePath);
        Data.savedWeapons[slotIndex].savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        SaveAllData();
        return true;
    }

    public bool ApplyCustomTextureToSavedFacility(int slotIndex, string texturePath)
    {
        if (slotIndex < 0 || slotIndex >= Data.savedFacilities.Count) return false;
        Data.savedFacilities[slotIndex].customTexturePath = texturePath;
        Data.savedFacilities[slotIndex].useCustomTexture = !string.IsNullOrEmpty(texturePath);
        Data.savedFacilities[slotIndex].savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        SaveAllData();
        return true;
    }

    public bool ApplyCustomTextureToSavedGrid(int slotIndex, string texturePath)
    {
        if (slotIndex < 0 || slotIndex >= Data.savedGridTerrains.Count) return false;
        Data.savedGridTerrains[slotIndex].customTexturePath = texturePath;
        Data.savedGridTerrains[slotIndex].useCustomTexture = !string.IsNullOrEmpty(texturePath);
        Data.savedGridTerrains[slotIndex].savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        SaveAllData();
        return true;
    }

    /// <summary>
    /// 将贴图应用到自定义移动方式
    /// </summary>
    public bool ApplyCustomTextureToMoveType(string moveTypeId, string texturePath)
    {
        if (!CustomMoveTypes.TryGetValue(moveTypeId, out var data)) return false;
        data.customTexturePath = texturePath;
        data.useCustomTexture = !string.IsNullOrEmpty(texturePath);
        SaveCustomMoveTypes();
        SaveAllData();
        return true;
    }

    /// <summary>
    /// 将贴图应用到自定义地形
    /// </summary>
    public bool ApplyCustomTextureToTerrainType(string terrainTypeId, string texturePath)
    {
        if (!CustomTerrainTypes.TryGetValue(terrainTypeId, out var data)) return false;
        data.customTexturePath = texturePath;
        data.useCustomTexture = !string.IsNullOrEmpty(texturePath);
        SaveCustomTerrainTypes();
        SaveAllData();
        return true;
    }

    // ==================== Android相册权限 ====================

    public static void RequestGalleryPermission()
    {
        if (OS.GetName() == "Android")
        {
            OS.RequestPermissions();
            // Android 13+ 需要 READ_MEDIA_IMAGES
            if (!HasGalleryPermission())
            {
                OS.RequestPermission("android.permission.READ_MEDIA_IMAGES");
                OS.RequestPermission("android.permission.READ_EXTERNAL_STORAGE");
            }
        }
    }

    public static bool HasGalleryPermission()
    {
        if (OS.GetName() != "Android") return true;
        var perms = OS.GetGrantedPermissions();
        return perms.Contains("android.permission.READ_MEDIA_IMAGES") 
            || perms.Contains("android.permission.READ_EXTERNAL_STORAGE");
    }

    // ==================== 自定义地形颜色和贴图辅助 ====================
    /// <summary>
    /// 获取自定义地形的颜色（如果配置了）
    /// </summary>
    public Color? GetCustomTerrainColor(string terrainTypeId)
    {
        if (CustomTerrainTypes.TryGetValue(terrainTypeId, out var data))
        {
            if (!string.IsNullOrEmpty(data.terrainColorHex))
            {
                try { return new Color(data.terrainColorHex); } catch (System.Exception ex) { GD.PushWarning($"[ExtraSave] {ex.GetType().Name}: {ex.Message}"); }
            }
        }
        return null;
    }

    /// <summary>
    /// 获取自定义地形的贴图路径（如果配置了）
    /// </summary>
    public string GetCustomTerrainTexturePath(string terrainTypeId)
    {
        if (CustomTerrainTypes.TryGetValue(terrainTypeId, out var data))
        {
            if (data.useCustomTexture && !string.IsNullOrEmpty(data.customTexturePath))
                return data.customTexturePath;
        }
        return null;
    }

    /// <summary>
    /// 获取自定义移动方式的贴图路径（如果配置了）
    /// </summary>
    public string GetCustomMoveTypeTexturePath(string moveTypeId)
    {
        if (CustomMoveTypes.TryGetValue(moveTypeId, out var data))
        {
            if (data.useCustomTexture && !string.IsNullOrEmpty(data.customTexturePath))
                return data.customTexturePath;
        }
        return null;
    }
}
