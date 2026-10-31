// FacilityDatabase.cs - 建筑场景数据库（建造模块/存档通用）
using Godot;
using System.Collections.Generic;

/// <summary>
/// 建筑类型 → 场景路径 的统一登记表。
/// 建造模块的白名单、地图导入导出、内部建造的产出都从这里解析场景。
/// </summary>
public static class FacilityDatabase
{
    private static readonly Dictionary<string, string> scenePaths = new()
    {
        ["City"]   = "res://Prefabs/City.tscn",
        ["Base"]   = "res://Prefabs/Base.tscn",
        ["AirPort"] = "res://Prefabs/AirPort.tscn",
        ["Port"]   = "res://Prefabs/Port.tscn",
    };

    /// <summary>按类型名取场景路径（未登记返回 null）</summary>
    public static string GetScenePath(string facilityType)
    {
        if (string.IsNullOrEmpty(facilityType)) return null;
        return scenePaths.GetValueOrDefault(facilityType);
    }

    /// <summary>全部已登记建筑类型（建造白名单选项）</summary>
    public static IEnumerable<string> AllTypes => scenePaths.Keys;
}
