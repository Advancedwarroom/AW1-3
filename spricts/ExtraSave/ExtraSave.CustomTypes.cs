using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// ExtraSave · CustomTypes（由 ExtraSave.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class ExtraSave
{



    // ==================== 自定义移动方式 ====================

    public void AddCustomMoveType(string id, string displayName, Dictionary<string, int> moveCosts)
    {
        var data = new CustomMoveTypeData
        {
            id = id,
            displayName = displayName,
            moveCosts = moveCosts ?? new Dictionary<string, int>(),
            isBuiltIn = false
        };

        CustomMoveTypes[id] = data;
        if (!Data.customMoveTypes.Any(c => c.id == id))
            Data.customMoveTypes.Add(data);
        else
        {
            var existing = Data.customMoveTypes.First(c => c.id == id);
            existing.displayName = displayName;
            existing.moveCosts = new Dictionary<string, int>(moveCosts);
        }

        SaveCustomMoveTypes();
        SaveAllData();
    }

    public void RemoveCustomMoveType(string id)
    {
        CustomMoveTypes.Remove(id);
        Data.customMoveTypes.RemoveAll(c => c.id == id);
        SaveCustomMoveTypes();
        SaveAllData();
    }

    /// <summary>
    /// 获取移动消耗。自定义移动方式×自定义地形 走这里。
    /// 内置×内置 返回 -1 表示"使用内置逻辑"（在 Infantry.GetMoveCost 中处理）
    /// </summary>
    public int GetMoveCost(string moveTypeId, string terrainTypeId)
    {
        // 先查自定义移动方式
        if (CustomMoveTypes.TryGetValue(moveTypeId, out var customMove))
        {
            if (customMove.moveCosts.TryGetValue(terrainTypeId, out int cost))
                return cost;
            return 999; // 自定义移动方式未配置的地形 = 不可移动
        }

        // 再查自定义地形对内置移动方式的配置
        if (CustomTerrainTypes.TryGetValue(terrainTypeId, out var customTerrain))
        {
            if (customTerrain.moveCosts.TryGetValue(moveTypeId, out int cost))
                return cost;
            return 999; // 自定义地形未配置的移动方式 = 不可移动
        }

        // 内置移动方式 × 内置地形：走原有代码逻辑（在 Infantry.GetMoveCost 中处理）
        return -1; // 表示"使用内置逻辑"
    }

    private void SaveCustomMoveTypes()
    {
        try
        {
            var dict = new Godot.Collections.Dictionary();
            foreach (var kvp in CustomMoveTypes)
            {
                var d = new Godot.Collections.Dictionary();
                d["id"] = kvp.Value.id;
                d["displayName"] = kvp.Value.displayName;
                d["isBuiltIn"] = kvp.Value.isBuiltIn;
                d["moveCosts"] = DictToGodot(kvp.Value.moveCosts);
                d["customTexturePath"] = kvp.Value.customTexturePath;
                d["useCustomTexture"] = kvp.Value.useCustomTexture;
                dict[kvp.Key] = d;
            }

            using var file = Godot.FileAccess.Open(CUSTOM_MOVE_TYPES_FILE, Godot.FileAccess.ModeFlags.Write);
            file.StoreString(Json.Stringify(dict));
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ExtraSave] {TranslateGroup.T("保存自定义移动方式失败")}: {e.Message}");
        }
    }

    // ==================== 自定义地形 ====================

    public void AddCustomTerrainType(string id, string displayName, Dictionary<string, int> moveCosts, float defenseBonus, Color color)
    {
        var data = new CustomTerrainTypeData
        {
            id = id,
            displayName = displayName,
            moveCosts = moveCosts ?? new Dictionary<string, int>(),
            defenseBonus = defenseBonus,
            terrainColorHex = color.ToHtml(),
            isBuiltIn = false
        };

        CustomTerrainTypes[id] = data;
        if (!Data.customTerrainTypes.Any(c => c.id == id))
            Data.customTerrainTypes.Add(data);
        else
        {
            var existing = Data.customTerrainTypes.First(c => c.id == id);
            existing.displayName = displayName;
            existing.moveCosts = new Dictionary<string, int>(moveCosts);
            existing.defenseBonus = defenseBonus;
            existing.terrainColorHex = color.ToHtml();
        }

        SaveCustomTerrainTypes();
        SaveAllData();
    }

    public void RemoveCustomTerrainType(string id)
    {
        CustomTerrainTypes.Remove(id);
        Data.customTerrainTypes.RemoveAll(c => c.id == id);
        SaveCustomTerrainTypes();
        SaveAllData();
    }

    private void SaveCustomTerrainTypes()
    {
        try
        {
            var dict = new Godot.Collections.Dictionary();
            foreach (var kvp in CustomTerrainTypes)
            {
                var d = new Godot.Collections.Dictionary();
                d["id"] = kvp.Value.id;
                d["displayName"] = kvp.Value.displayName;
                d["isBuiltIn"] = kvp.Value.isBuiltIn;
                d["moveCosts"] = DictToGodot(kvp.Value.moveCosts);
                d["defenseBonus"] = kvp.Value.defenseBonus;
                d["terrainColorHex"] = kvp.Value.terrainColorHex;
                d["customTexturePath"] = kvp.Value.customTexturePath;
                d["useCustomTexture"] = kvp.Value.useCustomTexture;
                dict[kvp.Key] = d;
            }

            using var file = Godot.FileAccess.Open(CUSTOM_TERRAIN_TYPES_FILE, Godot.FileAccess.ModeFlags.Write);
            file.StoreString(Json.Stringify(dict));
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ExtraSave] {TranslateGroup.T("保存自定义地形失败")}: {e.Message}");
        }
    }
}
