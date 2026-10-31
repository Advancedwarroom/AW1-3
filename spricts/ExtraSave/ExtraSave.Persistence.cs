using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// ExtraSave · Persistence（由 ExtraSave.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class ExtraSave
{

    // ==================== 存档/读档 ====================

    public void LoadAllData()
    {
        // 加载主数据
        if (Godot.FileAccess.FileExists(SAVE_FILE_NAME))
        {
            try
            {
                using var file = Godot.FileAccess.Open(SAVE_FILE_NAME, Godot.FileAccess.ModeFlags.Read);
                string json = file.GetAsText();
                Data = ParseFromJson(json);
                GD.Print(TranslateGroup.T("[ExtraSave] 数据加载成功"));
            }
            catch (Exception e)
            {
                GD.PrintErr($"[ExtraSave] {TranslateGroup.T("加载失败")}: {e.Message}，{TranslateGroup.T("创建新数据")}");
                Data = new ExtraSaveData();
            }
        }
        else
        {
            Data = new ExtraSaveData();
        }

        // 加载自定义移动方式
        LoadCustomMoveTypes();
        // 加载自定义地形
        LoadCustomTerrainTypes();
    }

    public void SaveAllData()
    {
        Data.lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds();

        try
        {
            string json = SerializeToJson(Data);
            using var file = Godot.FileAccess.Open(SAVE_FILE_NAME, Godot.FileAccess.ModeFlags.Write);
            file.StoreString(json);
            GD.Print(TranslateGroup.T("[ExtraSave] 数据保存成功"));
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ExtraSave] {TranslateGroup.T("保存失败")}: {e.Message}");
        }

        // 同时保存自定义移动方式和地形
        SaveCustomMoveTypes();
        SaveCustomTerrainTypes();
    }

    private string SerializeToJson(ExtraSaveData data)
    {
        var dict = new Godot.Collections.Dictionary();
        dict["version"] = data.version;
        dict["lastSaveTime"] = data.lastSaveTime;

        dict["savedUnits"] = SerializeUnitList(data.savedUnits);
        dict["savedWeapons"] = SerializeWeaponList(data.savedWeapons);
        dict["savedFacilities"] = SerializeFacilityList(data.savedFacilities);
        dict["savedGridTerrains"] = SerializeGridList(data.savedGridTerrains);
        dict["savedMaps"] = SerializeMapList(data.savedMaps);
        dict["favoriteUnitSlots"] = new Godot.Collections.Array<string>(data.favoriteUnitSlots);
        dict["favoriteWeaponSlots"] = new Godot.Collections.Array<string>(data.favoriteWeaponSlots);
        dict["favoriteFacilitySlots"] = new Godot.Collections.Array<string>(data.favoriteFacilitySlots);
        dict["favoriteGridSlots"] = new Godot.Collections.Array<string>(data.favoriteGridSlots);
        dict["customMoveTypes"] = SerializeCustomMoveTypeList(data.customMoveTypes);
        dict["customTerrainTypes"] = SerializeCustomTerrainTypeList(data.customTerrainTypes);

        return Json.Stringify(dict);
    }

    private ExtraSaveData ParseFromJson(string json)
    {
        var data = new ExtraSaveData();
        var dict = Json.ParseString(json).AsGodotDictionary();
        if (dict == null) return data;

        data.version = dict.GetValueOrDefault("version", 2).AsInt32();
        data.lastSaveTime = dict.GetValueOrDefault("lastSaveTime", 0).AsInt64();

        data.savedUnits = ParseUnitList(dict.GetValueOrDefault("savedUnits", new Variant()).AsGodotArray());
        data.savedWeapons = ParseWeaponList(dict.GetValueOrDefault("savedWeapons", new Variant()).AsGodotArray());
        data.savedFacilities = ParseFacilityList(dict.GetValueOrDefault("savedFacilities", new Variant()).AsGodotArray());
        data.savedGridTerrains = ParseGridList(dict.GetValueOrDefault("savedGridTerrains", new Variant()).AsGodotArray());
        data.savedMaps = ParseMapList(dict.GetValueOrDefault("savedMaps", new Variant()).AsGodotArray());

        data.favoriteUnitSlots = ParseStringList(dict.GetValueOrDefault("favoriteUnitSlots", new Variant()).AsGodotArray());
        data.favoriteWeaponSlots = ParseStringList(dict.GetValueOrDefault("favoriteWeaponSlots", new Variant()).AsGodotArray());
        data.favoriteFacilitySlots = ParseStringList(dict.GetValueOrDefault("favoriteFacilitySlots", new Variant()).AsGodotArray());
        data.favoriteGridSlots = ParseStringList(dict.GetValueOrDefault("favoriteGridSlots", new Variant()).AsGodotArray());

        data.customMoveTypes = ParseCustomMoveTypeList(dict.GetValueOrDefault("customMoveTypes", new Variant()).AsGodotArray());
        data.customTerrainTypes = ParseCustomTerrainTypeList(dict.GetValueOrDefault("customTerrainTypes", new Variant()).AsGodotArray());

        return data;
    }
}
