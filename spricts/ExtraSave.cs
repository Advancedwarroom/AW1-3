using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// ==================== 数据定义 ====================

[Serializable]
public class SavedUnitData
{
    public string slotName = "";
    public string displayName = "";
    public string scenePath = "";
    public string typeName = "";
    public string team = "Player1";
    public bool isFavorite = false;
    public long savedTimestamp = 0;      // 最后保存时间（修改优先级最高）
    public long lastUsedTimestamp = 0;     // 最后使用时间（加载/放置时更新）

    // 所有Export字段的序列化数据
    public Dictionary<string, string> fieldValues = new();

    // 自定义贴图路径
    public string customTexturePath = "";
    public bool useCustomTexture = false;

    // 攻防表
    public Dictionary<string, int> attackMatrix = new();
    public Dictionary<string, int> secondaryAttackMatrix = new();

    // 地图位置（用于战局地图保存）
    public int positionX = 0;
    public int positionY = 0;
}

[Serializable]
public class SavedWeaponData
{
    public string slotName = "";
    public string displayName = "";
    public string scenePath = "";
    public string typeName = "";
    public string team = "Player1";
    public bool isFavorite = false;
    public long savedTimestamp = 0;
    public long lastUsedTimestamp = 0;
    public Dictionary<string, string> fieldValues = new();
    public string customTexturePath = "";
    public bool useCustomTexture = false;
    
    // 添加这些缺失的字段
    public bool isMultiTile = false;
    public int sizeX = 1;
    public int sizeY = 1;
    public int direction = 0;

    // 地图位置（用于战局地图保存）
    public int positionX = 0;
    public int positionY = 0;
}

[Serializable]
public class SavedFacilityData
{
    public string slotName = "";
    public string displayName = "";
    public string scenePath = "";
    public string typeName = "";
    public string team = "Player0";
    public bool isFavorite = false;
    public long savedTimestamp = 0;
    public long lastUsedTimestamp = 0;
    public Dictionary<string, string> fieldValues = new();
    public string customTexturePath = "";
    public bool useCustomTexture = false;

    // 地图位置（用于战局地图保存）
    public int positionX = 0;
    public int positionY = 0;
}

[Serializable]
public class SavedGridTerrainData
{
    public string slotName = "";
    public string displayName = "";
    public bool isFavorite = false;
    public long savedTimestamp = 0;
    public long lastUsedTimestamp = 0;

    // 地形基础属性
    public GridType gridType = GridType.GROUND;
    public float terrainDefenseBonus = 0f;
    public bool canDestroyUnit = false;
    public int fixedDamagePerTurn = 0;
    public int fixedAttackPerTurn = 0;
    public bool canOverMaxHealth = false;
    public int fixedAmmoChangePerTurn = 0;
    public bool ammoCanOverMax = false;
    public bool ammoCanReachZero = true;
    public int fixedFuelChangePerTurn = 0;
    public bool fuelCanOverMax = false;
    public bool fuelCanReachZero = true;
    public int gridVisionRange = 0;
    public int gridVisionMode = 0; // GridVisionMode serialized as int
    public int towerDirection = 0; // TowerDirection serialized as int
    public bool towerCanRotate = false;
    public float towerRayAngle = 45f;
    public bool gridVisionIgnoreTerrain = false;
    public int visionBonus = 0;
    public bool requiresAdjacentVision = false;
    public bool isWatchtower = false;

    // 自定义贴图
    public string customTexturePath = "";
    public bool useCustomTexture = false;
    public string terrainColorHex = "ffffff";
}

[Serializable]
public class SavedMapData
{
    public string slotName = "";
    public string displayName = "";
    public long savedTimestamp = 0;
    public long lastUsedTimestamp = 0;
    public int width = 0;
    public int height = 0;
    public bool fogOfWarEnabled = false;

    // 地形矩阵
    public List<int> terrainIds = new();
    // 单位列表
    public List<SavedUnitData> units = new();
    // 兵器列表
    public List<SavedWeaponData> weapons = new();
    // 设施列表
    public List<SavedFacilityData> facilities = new();
    // 资金
    public int p1Funds = 0;
    public int p2Funds = 0;
}

[Serializable]
public class CustomMoveTypeData
{
    public string id = "";
    public string displayName = "";
    // key: 地形类型名, value: 移动消耗
    public Dictionary<string, int> moveCosts = new();
    public bool isBuiltIn = false;
    public string customTexturePath = "";
    public bool useCustomTexture = false;
}

[Serializable]
public class CustomTerrainTypeData
{
    public string id = "";
    public string displayName = "";
    // key: 移动方式类型名, value: 移动消耗
    public Dictionary<string, int> moveCosts = new();
    public float defenseBonus = 0f;
    public string terrainColorHex = "ffffff";
    public string customTexturePath = "";
    public bool useCustomTexture = false;
    public bool isBuiltIn = false;
}

[Serializable]
public class ExtraSaveData
{
    public int version = 2; // 版本升级
    public long lastSaveTime = 0;

    // 各10个槽位
    public List<SavedUnitData> savedUnits = new();
    public List<SavedWeaponData> savedWeapons = new();
    public List<SavedFacilityData> savedFacilities = new();
    public List<SavedGridTerrainData> savedGridTerrains = new();

    // 5个地图槽位
    public List<SavedMapData> savedMaps = new();

    // 自定义移动方式
    public List<CustomMoveTypeData> customMoveTypes = new();
    // 自定义地形
    public List<CustomTerrainTypeData> customTerrainTypes = new();

    // 收藏列表（存储slotName）
    public List<string> favoriteUnitSlots = new();
    public List<string> favoriteWeaponSlots = new();
    public List<string> favoriteFacilitySlots = new();
    public List<string> favoriteGridSlots = new();
}

// ==================== 主管理类 ====================

public partial class ExtraSave : Node
{
    public const int MAX_SLOTS = 10;
    public const int MAX_FAVORITES = 5;
    public const int MAX_MAP_SLOTS = 5;
    public const int MAX_TEXTURE_SIZE = 32;
    public const string SAVE_FILE_NAME = "user://ExtraSaveData.json";
    public const string CUSTOM_TEXTURE_DIR = "user://CustomTextures/";
    public const string CUSTOM_MOVE_TYPES_FILE = "user://CustomMoveTypes.json";
    public const string CUSTOM_TERRAIN_TYPES_FILE = "user://CustomTerrainTypes.json";

    public ExtraSaveData Data { get; private set; } = new();

    // 运行时缓存：自定义移动方式
    public Dictionary<string, CustomMoveTypeData> CustomMoveTypes { get; private set; } = new();
    // 运行时缓存：自定义地形
    public Dictionary<string, CustomTerrainTypeData> CustomTerrainTypes { get; private set; } = new();

    // 内置移动方式列表（用于显示和配置）
    public static readonly string[] BuiltInMoveTypes = new[]
    {
        "Infantry", "Mech", "Oozium", "Treads", "Naval", "AirPlane", 
        "AirShip", "Drone", "AeroSpacer", "HeliCopter", "SpaceShiper",
        "Hover", "PipeRunner", "Tire", "LAVARUNNER", "LAVAHOVER",
        "Train", "GasTrain", "FASTER", "Missile"
    };

    // 内置地形列表
    public static readonly string[] BuiltInTerrainTypes = new[]
    {
        "GROUND", "FOREST", "ROAD", "SEA", "RIVER", "HILL", "METEORITE",
        "PIPE", "LAVA", "BEACH", "TP", "REEF", "WHIRLPOOL", "LAVASIDE",
        "SEAFOG", "LANDFOG", "WATERFALL", "CLIFF", "SLOPE", "CAVE", "HOLE",
        "PIPESEAM", "TRACK", "STATION", "BRIDGE", "LAVABRIDGE", "PASSABLEPIPE",
        "SHIPGATE", "OVERPASS", "BROKENPIPE", "RUINS", "BROKENTRACK", "LAVAFOG"
    };

    public static ExtraSave Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        AddToGroup("extra_save");

        // 确保目录存在
        EnsureDirectoryExists(CUSTOM_TEXTURE_DIR);
        EnsureDirectoryExists(GodotPathCombine(CUSTOM_TEXTURE_DIR, TranslateGroup.T("单位")));
        EnsureDirectoryExists(GodotPathCombine(CUSTOM_TEXTURE_DIR, TranslateGroup.T("兵器")));
        EnsureDirectoryExists(GodotPathCombine(CUSTOM_TEXTURE_DIR, TranslateGroup.T("设施")));
        EnsureDirectoryExists(GodotPathCombine(CUSTOM_TEXTURE_DIR, TranslateGroup.T("地形")));
        EnsureDirectoryExists(GodotPathCombine(CUSTOM_TEXTURE_DIR, TranslateGroup.T("移动方式")));

        LoadAllData();
        InitializeDefaultSlots();

        GD.Print(TranslateGroup.T("[ExtraSave] 初始化完成，版本: ") + Data.version);
    }

    private void EnsureDirectoryExists(string path)
    {
        if (!DirAccess.DirExistsAbsolute(path))
        {
            DirAccess.MakeDirRecursiveAbsolute(path);
        }
    }

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

    // ==================== 序列化辅助 ====================

    private Godot.Collections.Array SerializeUnitList(List<SavedUnitData> list)
    {
        var arr = new Godot.Collections.Array();
        foreach (var item in list)
        {
            if (item == null) continue;
            var d = new Godot.Collections.Dictionary();
            d["slotName"] = item.slotName;
            d["displayName"] = item.displayName;
            d["scenePath"] = item.scenePath;
            d["typeName"] = item.typeName;
            d["team"] = item.team;
            d["isFavorite"] = item.isFavorite;
            d["savedTimestamp"] = item.savedTimestamp;
            d["lastUsedTimestamp"] = item.lastUsedTimestamp;
            d["fieldValues"] = DictToGodot(item.fieldValues);
            d["customTexturePath"] = item.customTexturePath;
            d["useCustomTexture"] = item.useCustomTexture;
            d["attackMatrix"] = DictToGodot(item.attackMatrix);
            d["secondaryAttackMatrix"] = DictToGodot(item.secondaryAttackMatrix);
            arr.Add(d);
        }
        return arr;
    }

    private List<SavedUnitData> ParseUnitList(Godot.Collections.Array arr)
    {
        var list = new List<SavedUnitData>();
        foreach (var item in arr)
        {
            var d = item.AsGodotDictionary();
            if (d == null) continue;
            list.Add(new SavedUnitData
            {
                slotName = d.GetValueOrDefault("slotName", "").AsString(),
                displayName = d.GetValueOrDefault("displayName", "").AsString(),
                scenePath = d.GetValueOrDefault("scenePath", "").AsString(),
                typeName = d.GetValueOrDefault("typeName", "").AsString(),
                team = d.GetValueOrDefault("team", "Player1").AsString(),
                isFavorite = d.GetValueOrDefault("isFavorite", false).AsBool(),
                savedTimestamp = d.GetValueOrDefault("savedTimestamp", 0).AsInt64(),
                lastUsedTimestamp = d.GetValueOrDefault("lastUsedTimestamp", 0).AsInt64(),
                fieldValues = ParseStringDict(d.GetValueOrDefault("fieldValues", new Variant()).AsGodotDictionary()),
                customTexturePath = d.GetValueOrDefault("customTexturePath", "").AsString(),
                useCustomTexture = d.GetValueOrDefault("useCustomTexture", false).AsBool(),
                attackMatrix = ParseIntDict(d.GetValueOrDefault("attackMatrix", new Variant()).AsGodotDictionary()),
                secondaryAttackMatrix = ParseIntDict(d.GetValueOrDefault("secondaryAttackMatrix", new Variant()).AsGodotDictionary())
            });
        }
        return list;
    }

    private Godot.Collections.Array SerializeWeaponList(List<SavedWeaponData> list)
    {
        var arr = new Godot.Collections.Array();
        foreach (var item in list)
        {
            if (item == null) continue;
            var d = new Godot.Collections.Dictionary();
            d["slotName"] = item.slotName;
            d["displayName"] = item.displayName;
            d["scenePath"] = item.scenePath;
            d["typeName"] = item.typeName;
            d["team"] = item.team;
            d["isFavorite"] = item.isFavorite;
            d["savedTimestamp"] = item.savedTimestamp;
            d["lastUsedTimestamp"] = item.lastUsedTimestamp;
            d["fieldValues"] = DictToGodot(item.fieldValues);
            d["customTexturePath"] = item.customTexturePath;
            d["useCustomTexture"] = item.useCustomTexture;
            d["isMultiTile"] = item.isMultiTile;
            d["sizeX"] = item.sizeX;
            d["sizeY"] = item.sizeY;
            d["direction"] = item.direction;
            arr.Add(d);
        }
        return arr;
    }

    private List<SavedWeaponData> ParseWeaponList(Godot.Collections.Array arr)
    {
        var list = new List<SavedWeaponData>();
        foreach (var item in arr)
        {
            var d = item.AsGodotDictionary();
            if (d == null) continue;
            list.Add(new SavedWeaponData
            {
                slotName = d.GetValueOrDefault("slotName", "").AsString(),
                displayName = d.GetValueOrDefault("displayName", "").AsString(),
                scenePath = d.GetValueOrDefault("scenePath", "").AsString(),
                typeName = d.GetValueOrDefault("typeName", "").AsString(),
                team = d.GetValueOrDefault("team", "Player1").AsString(),
                isFavorite = d.GetValueOrDefault("isFavorite", false).AsBool(),
                savedTimestamp = d.GetValueOrDefault("savedTimestamp", 0).AsInt64(),
                lastUsedTimestamp = d.GetValueOrDefault("lastUsedTimestamp", 0).AsInt64(),
                fieldValues = ParseStringDict(d.GetValueOrDefault("fieldValues", new Variant()).AsGodotDictionary()),
                customTexturePath = d.GetValueOrDefault("customTexturePath", "").AsString(),
                useCustomTexture = d.GetValueOrDefault("useCustomTexture", false).AsBool(),
                isMultiTile = d.GetValueOrDefault("isMultiTile", false).AsBool(),
                sizeX = d.GetValueOrDefault("sizeX", 1).AsInt32(),
                sizeY = d.GetValueOrDefault("sizeY", 1).AsInt32(),
                direction = d.GetValueOrDefault("direction", 0).AsInt32()
            });
        }
        return list;
    }

    private Godot.Collections.Array SerializeFacilityList(List<SavedFacilityData> list)
    {
        var arr = new Godot.Collections.Array();
        foreach (var item in list)
        {
            if (item == null) continue;
            var d = new Godot.Collections.Dictionary();
            d["slotName"] = item.slotName;
            d["displayName"] = item.displayName;
            d["scenePath"] = item.scenePath;
            d["typeName"] = item.typeName;
            d["team"] = item.team;
            d["isFavorite"] = item.isFavorite;
            d["savedTimestamp"] = item.savedTimestamp;
            d["lastUsedTimestamp"] = item.lastUsedTimestamp;
            d["fieldValues"] = DictToGodot(item.fieldValues);
            d["customTexturePath"] = item.customTexturePath;
            d["useCustomTexture"] = item.useCustomTexture;
            arr.Add(d);
        }
        return arr;
    }

    private List<SavedFacilityData> ParseFacilityList(Godot.Collections.Array arr)
    {
        var list = new List<SavedFacilityData>();
        foreach (var item in arr)
        {
            var d = item.AsGodotDictionary();
            if (d == null) continue;
            list.Add(new SavedFacilityData
            {
                slotName = d.GetValueOrDefault("slotName", "").AsString(),
                displayName = d.GetValueOrDefault("displayName", "").AsString(),
                scenePath = d.GetValueOrDefault("scenePath", "").AsString(),
                typeName = d.GetValueOrDefault("typeName", "").AsString(),
                team = d.GetValueOrDefault("team", "Player0").AsString(),
                isFavorite = d.GetValueOrDefault("isFavorite", false).AsBool(),
                savedTimestamp = d.GetValueOrDefault("savedTimestamp", 0).AsInt64(),
                lastUsedTimestamp = d.GetValueOrDefault("lastUsedTimestamp", 0).AsInt64(),
                fieldValues = ParseStringDict(d.GetValueOrDefault("fieldValues", new Variant()).AsGodotDictionary()),
                customTexturePath = d.GetValueOrDefault("customTexturePath", "").AsString(),
                useCustomTexture = d.GetValueOrDefault("useCustomTexture", false).AsBool()
            });
        }
        return list;
    }

    private Godot.Collections.Array SerializeGridList(List<SavedGridTerrainData> list)
    {
        var arr = new Godot.Collections.Array();
        foreach (var item in list)
        {
            if (item == null) continue;
            var d = new Godot.Collections.Dictionary();
            d["slotName"] = item.slotName;
            d["displayName"] = item.displayName;
            d["isFavorite"] = item.isFavorite;
            d["savedTimestamp"] = item.savedTimestamp;
            d["lastUsedTimestamp"] = item.lastUsedTimestamp;
            d["gridType"] = (int)item.gridType;
            d["terrainDefenseBonus"] = item.terrainDefenseBonus;
            d["canDestroyUnit"] = item.canDestroyUnit;
            d["fixedDamagePerTurn"] = item.fixedDamagePerTurn;
            d["fixedAttackPerTurn"] = item.fixedAttackPerTurn;
            d["canOverMaxHealth"] = item.canOverMaxHealth;
            d["fixedAmmoChangePerTurn"] = item.fixedAmmoChangePerTurn;
            d["ammoCanOverMax"] = item.ammoCanOverMax;
            d["ammoCanReachZero"] = item.ammoCanReachZero;
            d["fixedFuelChangePerTurn"] = item.fixedFuelChangePerTurn;
            d["fuelCanOverMax"] = item.fuelCanOverMax;
            d["fuelCanReachZero"] = item.fuelCanReachZero;
            d["gridVisionRange"] = item.gridVisionRange;
            d["gridVisionMode"] = item.gridVisionMode;
            d["towerDirection"] = item.towerDirection;
            d["towerCanRotate"] = item.towerCanRotate;
            d["towerRayAngle"] = item.towerRayAngle;
            d["gridVisionIgnoreTerrain"] = item.gridVisionIgnoreTerrain;
            d["visionBonus"] = item.visionBonus;
            d["requiresAdjacentVision"] = item.requiresAdjacentVision;
            d["isWatchtower"] = item.isWatchtower;
            d["customTexturePath"] = item.customTexturePath;
            d["useCustomTexture"] = item.useCustomTexture;
            d["terrainColorHex"] = item.terrainColorHex;
            arr.Add(d);
        }
        return arr;
    }

    private List<SavedGridTerrainData> ParseGridList(Godot.Collections.Array arr)
    {
        var list = new List<SavedGridTerrainData>();
        foreach (var item in arr)
        {
            var d = item.AsGodotDictionary();
            if (d == null) continue;
            list.Add(new SavedGridTerrainData
            {
                slotName = d.GetValueOrDefault("slotName", "").AsString(),
                displayName = d.GetValueOrDefault("displayName", "").AsString(),
                isFavorite = d.GetValueOrDefault("isFavorite", false).AsBool(),
                savedTimestamp = d.GetValueOrDefault("savedTimestamp", 0).AsInt64(),
                lastUsedTimestamp = d.GetValueOrDefault("lastUsedTimestamp", 0).AsInt64(),
                gridType = (GridType)d.GetValueOrDefault("gridType", 0).AsInt32(),
                terrainDefenseBonus = d.GetValueOrDefault("terrainDefenseBonus", 0f).AsSingle(),
                canDestroyUnit = d.GetValueOrDefault("canDestroyUnit", false).AsBool(),
                fixedDamagePerTurn = d.GetValueOrDefault("fixedDamagePerTurn", 0).AsInt32(),
                fixedAttackPerTurn = d.GetValueOrDefault("fixedAttackPerTurn", 0).AsInt32(),
                canOverMaxHealth = d.GetValueOrDefault("canOverMaxHealth", false).AsBool(),
                fixedAmmoChangePerTurn = d.GetValueOrDefault("fixedAmmoChangePerTurn", 0).AsInt32(),
                ammoCanOverMax = d.GetValueOrDefault("ammoCanOverMax", false).AsBool(),
                ammoCanReachZero = d.GetValueOrDefault("ammoCanReachZero", true).AsBool(),
                fixedFuelChangePerTurn = d.GetValueOrDefault("fixedFuelChangePerTurn", 0).AsInt32(),
                fuelCanOverMax = d.GetValueOrDefault("fuelCanOverMax", false).AsBool(),
                fuelCanReachZero = d.GetValueOrDefault("fuelCanReachZero", true).AsBool(),
                gridVisionRange = d.GetValueOrDefault("gridVisionRange", 0).AsInt32(),
                gridVisionMode = d.GetValueOrDefault("gridVisionMode", 0).AsInt32(),
                towerDirection = d.GetValueOrDefault("towerDirection", 0).AsInt32(),
                towerCanRotate = d.GetValueOrDefault("towerCanRotate", false).AsBool(),
                towerRayAngle = d.GetValueOrDefault("towerRayAngle", 45f).AsSingle(),
                gridVisionIgnoreTerrain = d.GetValueOrDefault("gridVisionIgnoreTerrain", false).AsBool(),
                visionBonus = d.GetValueOrDefault("visionBonus", 0).AsInt32(),
                requiresAdjacentVision = d.GetValueOrDefault("requiresAdjacentVision", false).AsBool(),
                isWatchtower = d.GetValueOrDefault("isWatchtower", false).AsBool(),
                customTexturePath = d.GetValueOrDefault("customTexturePath", "").AsString(),
                useCustomTexture = d.GetValueOrDefault("useCustomTexture", false).AsBool(),
                terrainColorHex = d.GetValueOrDefault("terrainColorHex", "ffffff").AsString()
            });
        }
        return list;
    }

    private Godot.Collections.Array SerializeMapList(List<SavedMapData> list)
    {
        var arr = new Godot.Collections.Array();
        foreach (var item in list)
        {
            if (item == null) continue;
            var d = new Godot.Collections.Dictionary();
            d["slotName"] = item.slotName;
            d["displayName"] = item.displayName;
            d["savedTimestamp"] = item.savedTimestamp;
            d["lastUsedTimestamp"] = item.lastUsedTimestamp;
            d["width"] = item.width;
            d["height"] = item.height;
            d["fogOfWarEnabled"] = item.fogOfWarEnabled;
            d["terrainIds"] = new Godot.Collections.Array<int>(item.terrainIds);
            d["units"] = SerializeUnitList(item.units);
            d["weapons"] = SerializeWeaponList(item.weapons);
            d["facilities"] = SerializeFacilityList(item.facilities);
            d["p1Funds"] = item.p1Funds;
            d["p2Funds"] = item.p2Funds;
            arr.Add(d);
        }
        return arr;
    }

    private List<SavedMapData> ParseMapList(Godot.Collections.Array arr)
    {
        var list = new List<SavedMapData>();
        foreach (var item in arr)
        {
            var d = item.AsGodotDictionary();
            if (d == null) continue;
            var mapData = new SavedMapData
            {
                slotName = d.GetValueOrDefault("slotName", "").AsString(),
                displayName = d.GetValueOrDefault("displayName", "").AsString(),
                savedTimestamp = d.GetValueOrDefault("savedTimestamp", 0).AsInt64(),
                lastUsedTimestamp = d.GetValueOrDefault("lastUsedTimestamp", 0).AsInt64(),
                width = d.GetValueOrDefault("width", 0).AsInt32(),
                height = d.GetValueOrDefault("height", 0).AsInt32(),
                fogOfWarEnabled = d.GetValueOrDefault("fogOfWarEnabled", false).AsBool(),
                p1Funds = d.GetValueOrDefault("p1Funds", 0).AsInt32(),
                p2Funds = d.GetValueOrDefault("p2Funds", 0).AsInt32()
            };

            var terrainArr = d.GetValueOrDefault("terrainIds", new Variant()).AsGodotArray();
            foreach (var t in terrainArr) mapData.terrainIds.Add(t.AsInt32());

            mapData.units = ParseUnitList(d.GetValueOrDefault("units", new Variant()).AsGodotArray());
            mapData.weapons = ParseWeaponList(d.GetValueOrDefault("weapons", new Variant()).AsGodotArray());
            mapData.facilities = ParseFacilityList(d.GetValueOrDefault("facilities", new Variant()).AsGodotArray());

            list.Add(mapData);
        }
        return list;
    }

    private Godot.Collections.Array SerializeCustomMoveTypeList(List<CustomMoveTypeData> list)
    {
        var arr = new Godot.Collections.Array();
        foreach (var item in list)
        {
            var d = new Godot.Collections.Dictionary();
            d["id"] = item.id;
            d["displayName"] = item.displayName;
            d["isBuiltIn"] = item.isBuiltIn;
            d["moveCosts"] = DictToGodot(item.moveCosts);
            d["customTexturePath"] = item.customTexturePath;
            d["useCustomTexture"] = item.useCustomTexture;
            arr.Add(d);
        }
        return arr;
    }

    private List<CustomMoveTypeData> ParseCustomMoveTypeList(Godot.Collections.Array arr)
    {
        var list = new List<CustomMoveTypeData>();
        foreach (var item in arr)
        {
            var d = item.AsGodotDictionary();
            if (d == null) continue;
            list.Add(new CustomMoveTypeData
            {
                id = d.GetValueOrDefault("id", "").AsString(),
                displayName = d.GetValueOrDefault("displayName", "").AsString(),
                isBuiltIn = d.GetValueOrDefault("isBuiltIn", false).AsBool(),
                moveCosts = ParseIntDict(d.GetValueOrDefault("moveCosts", new Variant()).AsGodotDictionary()),
                customTexturePath = d.GetValueOrDefault("customTexturePath", "").AsString(),
                useCustomTexture = d.GetValueOrDefault("useCustomTexture", false).AsBool()
            });
        }
        return list;
    }

    private Godot.Collections.Array SerializeCustomTerrainTypeList(List<CustomTerrainTypeData> list)
    {
        var arr = new Godot.Collections.Array();
        foreach (var item in list)
        {
            var d = new Godot.Collections.Dictionary();
            d["id"] = item.id;
            d["displayName"] = item.displayName;
            d["isBuiltIn"] = item.isBuiltIn;
            d["moveCosts"] = DictToGodot(item.moveCosts);
            d["defenseBonus"] = item.defenseBonus;
            d["terrainColorHex"] = item.terrainColorHex;
            d["customTexturePath"] = item.customTexturePath;
            d["useCustomTexture"] = item.useCustomTexture;
            arr.Add(d);
        }
        return arr;
    }

    private List<CustomTerrainTypeData> ParseCustomTerrainTypeList(Godot.Collections.Array arr)
    {
        var list = new List<CustomTerrainTypeData>();
        foreach (var item in arr)
        {
            var d = item.AsGodotDictionary();
            if (d == null) continue;
            list.Add(new CustomTerrainTypeData
            {
                id = d.GetValueOrDefault("id", "").AsString(),
                displayName = d.GetValueOrDefault("displayName", "").AsString(),
                isBuiltIn = d.GetValueOrDefault("isBuiltIn", false).AsBool(),
                moveCosts = ParseIntDict(d.GetValueOrDefault("moveCosts", new Variant()).AsGodotDictionary()),
                defenseBonus = d.GetValueOrDefault("defenseBonus", 0f).AsSingle(),
                terrainColorHex = d.GetValueOrDefault("terrainColorHex", "ffffff").AsString(),
                customTexturePath = d.GetValueOrDefault("customTexturePath", "").AsString(),
                useCustomTexture = d.GetValueOrDefault("useCustomTexture", false).AsBool()
            });
        }
        return list;
    }

    private Godot.Collections.Dictionary DictToGodot(Dictionary<string, string> dict)
    {
        var gd = new Godot.Collections.Dictionary();
        foreach (var kvp in dict) gd[kvp.Key] = kvp.Value;
        return gd;
    }

    private Godot.Collections.Dictionary DictToGodot(Dictionary<string, int> dict)
    {
        var gd = new Godot.Collections.Dictionary();
        foreach (var kvp in dict) gd[kvp.Key] = kvp.Value;
        return gd;
    }

    private Dictionary<string, string> ParseStringDict(Godot.Collections.Dictionary gd)
    {
        var dict = new Dictionary<string, string>();
        foreach (var k in gd.Keys) dict[k.AsString()] = gd[k].AsString();
        return dict;
    }

    private Dictionary<string, int> ParseIntDict(Godot.Collections.Dictionary gd)
    {
        var dict = new Dictionary<string, int>();
        foreach (var k in gd.Keys) dict[k.AsString()] = gd[k].AsInt32();
        return dict;
    }

    private List<string> ParseStringList(Godot.Collections.Array arr)
    {
        var list = new List<string>();
        foreach (var item in arr) list.Add(item.AsString());
        return list;
    }

    // ==================== 初始化默认槽位 ====================

    private void InitializeDefaultSlots()
    {
        EnsureSlotList(ref Data.savedUnits, TranslateGroup.T("单位"));
        EnsureSlotList(ref Data.savedWeapons, TranslateGroup.T("兵器"));
        EnsureSlotList(ref Data.savedFacilities, TranslateGroup.T("设施"));
        EnsureSlotList(ref Data.savedGridTerrains, TranslateGroup.T("地形"));
        EnsureSlotList(ref Data.savedMaps, TranslateGroup.T("地图"));
    }

    private void EnsureSlotList<T>(ref List<T> list, string prefix) where T : class
    {
        int maxSlots = typeof(T) == typeof(SavedMapData) ? MAX_MAP_SLOTS : MAX_SLOTS;

        while (list.Count < maxSlots)
        {
            list.Add(null);
        }
    }

    // ==================== 保存操作（带自动淘汰） ====================

    /// <summary>
    /// 保存单位到槽位，自动淘汰最旧的（修改时间优先）
    /// </summary>
    public bool SaveUnit(Infantry unit, int slotIndex, string customName = null)
    {
        if (unit == null || slotIndex < 0 || slotIndex >= MAX_SLOTS) return false;

        var data = new SavedUnitData
        {
            slotName = $"{TranslateGroup.T("单位")}{slotIndex + 1}",
            displayName = customName ?? unit.Name ?? $"{TranslateGroup.T("单位")}{slotIndex + 1}",
            scenePath = GetScenePathForInstance(unit),
            typeName = unit.GetType().Name,
            team = unit.team,
            savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            fieldValues = SerializeObjectFields(unit),
            attackMatrix = ToSystemDict(unit.attackMatrix),
            secondaryAttackMatrix = ToSystemDict(unit.secondaryAttackMatrix)
        };

        Data.savedUnits[slotIndex] = data;
        SaveAllData();
        return true;
    }

    public bool SaveWeapon(Weapon weapon, int slotIndex, string customName = null)
    {
        if (weapon == null || slotIndex < 0 || slotIndex >= MAX_SLOTS) return false;

        var data = new SavedWeaponData
        {
            slotName = $"{TranslateGroup.T("兵器")}{slotIndex + 1}",
            displayName = customName ?? weapon.Name ?? $"{TranslateGroup.T("兵器")}{slotIndex + 1}",
            scenePath = GetScenePathForInstance(weapon),
            typeName = weapon.GetType().Name,
            team = weapon.team,
            savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            fieldValues = SerializeObjectFields(weapon)
        };

        // 保存多格兵器属性
        data.isMultiTile = weapon.isMultiTile;
        data.sizeX = weapon.size.X;
        data.sizeY = weapon.size.Y;
        if (weapon is BlackCannon bc)
            data.direction = (int)bc.direction;
        else if (weapon is DeathRay dr)
            data.direction = (int)dr.direction;
        else if (weapon is LargeCannon lc)
            data.direction = (int)lc.direction;

        Data.savedWeapons[slotIndex] = data;
        SaveAllData();
        return true;
    }

    public bool SaveFacility(Facility facility, int slotIndex, string customName = null)
    {
        if (facility == null || slotIndex < 0 || slotIndex >= MAX_SLOTS) return false;

        var data = new SavedFacilityData
        {
            slotName = $"{TranslateGroup.T("设施")}{slotIndex + 1}",
            displayName = customName ?? facility.Name ?? $"{TranslateGroup.T("设施")}{slotIndex + 1}",
            scenePath = GetScenePathForInstance(facility),
            typeName = facility.GetType().Name,
            team = facility.facilityTeam,
            savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            fieldValues = SerializeObjectFields(facility)
        };

        Data.savedFacilities[slotIndex] = data;
        SaveAllData();
        return true;
    }

    public bool SaveGridTerrain(Grids grid, int slotIndex, string customName = null)
    {
        if (grid == null || slotIndex < 0 || slotIndex >= MAX_SLOTS) return false;

        var data = new SavedGridTerrainData
        {
            slotName = $"{TranslateGroup.T("地形")}{slotIndex + 1}",
            displayName = customName ?? $"{TranslateGroup.T("地形")}{slotIndex + 1}",
            savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            gridType = grid.gridType,
            terrainDefenseBonus = grid.TerrainDefenseBonus,
            canDestroyUnit = grid.canDestroyUnit,
            fixedDamagePerTurn = grid.fixedDamagePerTurn,
            fixedAttackPerTurn = grid.fixedAttackPerTurn,
            canOverMaxHealth = grid.canOverMaxHealth,
            fixedAmmoChangePerTurn = grid.fixedAmmoChangePerTurn,
            ammoCanOverMax = grid.ammoCanOverMax,
            ammoCanReachZero = grid.ammoCanReachZero,
            fixedFuelChangePerTurn = grid.fixedFuelChangePerTurn,
            fuelCanOverMax = grid.fuelCanOverMax,
            fuelCanReachZero = grid.fuelCanReachZero,
            gridVisionRange = grid.gridVisionRange,
            gridVisionMode = (int)grid.gridVisionMode,
            towerDirection = (int)grid.towerDirection,
            towerCanRotate = grid.towerCanRotate,
            towerRayAngle = grid.towerRayAngle,
            gridVisionIgnoreTerrain = grid.gridVisionIgnoreTerrain,
            visionBonus = grid.visionBonus,
            requiresAdjacentVision = grid.requiresAdjacentVision,
            isWatchtower = grid.isWatchtower
        };

        Data.savedGridTerrains[slotIndex] = data;
        SaveAllData();
        return true;
    }

    // ==================== 自动淘汰最旧的（非收藏） ====================
    /// <summary>
    /// 修改时间 > 使用时间。如果修改时间相同，比较使用时间。
    /// 返回要淘汰的槽位索引，-1表示无需淘汰（有空位）
    /// </summary>
    public int FindAutoEvictSlot<T>(List<T> list, List<string> favorites) where T : class
    {
        // 优先找空的
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            bool isEmpty = false;
            if (item is SavedUnitData u) isEmpty = string.IsNullOrEmpty(u.scenePath) && u.fieldValues.Count == 0;
            else if (item is SavedWeaponData w) isEmpty = string.IsNullOrEmpty(w.scenePath) && w.fieldValues.Count == 0;
            else if (item is SavedFacilityData f) isEmpty = string.IsNullOrEmpty(f.scenePath) && f.fieldValues.Count == 0;
            else if (item is SavedGridTerrainData g) isEmpty = g.savedTimestamp == 0;
            else if (item is SavedMapData m) isEmpty = m.width == 0;

            if (isEmpty) return i;
        }

        // 找最旧的非收藏（按 savedTimestamp 降序，修改时间优先）
        long oldestTime = long.MaxValue;
        int oldestIndex = -1;

        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            string slotName = "";
            bool isFav = false;
            long timestamp = 0;
            long usedTime = 0;

            if (item is SavedUnitData u) { slotName = u.slotName; isFav = u.isFavorite; timestamp = u.savedTimestamp; usedTime = u.lastUsedTimestamp; }
            else if (item is SavedWeaponData w) { slotName = w.slotName; isFav = w.isFavorite; timestamp = w.savedTimestamp; usedTime = w.lastUsedTimestamp; }
            else if (item is SavedFacilityData f) { slotName = f.slotName; isFav = f.isFavorite; timestamp = f.savedTimestamp; usedTime = f.lastUsedTimestamp; }
            else if (item is SavedGridTerrainData g) { slotName = g.slotName; isFav = g.isFavorite; timestamp = g.savedTimestamp; usedTime = g.lastUsedTimestamp; }
            else if (item is SavedMapData m) { slotName = m.slotName; timestamp = m.savedTimestamp; usedTime = m.lastUsedTimestamp; }

            if (isFav || favorites.Contains(slotName)) continue;

            // 比较：savedTimestamp 优先，相同则比较 lastUsedTimestamp
            long effectiveTime = timestamp * 1000000000L + usedTime; // 组合时间戳
            if (effectiveTime < oldestTime) { oldestTime = effectiveTime; oldestIndex = i; }
        }

        return oldestIndex;
    }

    /// <summary>
    /// 智能保存：如果指定槽位被占用，自动找到最旧的非收藏槽位淘汰
    /// </summary>
    public bool SmartSaveUnit(Infantry unit, int preferredSlot, string customName = null)
    {
        if (unit == null) return false;

        // 检查指定槽位是否为空
        bool isEmpty = preferredSlot >= 0 && preferredSlot < MAX_SLOTS && 
            Data.savedUnits[preferredSlot].savedTimestamp == 0;

        if (!isEmpty)
        {
            // 找要淘汰的槽位
            int evictSlot = FindAutoEvictSlot(Data.savedUnits, Data.favoriteUnitSlots);
            if (evictSlot < 0) return false; // 全满了且都是收藏
            preferredSlot = evictSlot;
        }

        return SaveUnit(unit, preferredSlot, customName);
    }

    public bool SmartSaveWeapon(Weapon weapon, int preferredSlot, string customName = null)
    {
        if (weapon == null) return false;
        bool isEmpty = preferredSlot >= 0 && preferredSlot < MAX_SLOTS && 
            Data.savedWeapons[preferredSlot].savedTimestamp == 0;
        if (!isEmpty)
        {
            int evictSlot = FindAutoEvictSlot(Data.savedWeapons, Data.favoriteWeaponSlots);
            if (evictSlot < 0) return false;
            preferredSlot = evictSlot;
        }
        return SaveWeapon(weapon, preferredSlot, customName);
    }

    public bool SmartSaveFacility(Facility facility, int preferredSlot, string customName = null)
    {
        if (facility == null) return false;
        bool isEmpty = preferredSlot >= 0 && preferredSlot < MAX_SLOTS && 
            Data.savedFacilities[preferredSlot].savedTimestamp == 0;
        if (!isEmpty)
        {
            int evictSlot = FindAutoEvictSlot(Data.savedFacilities, Data.favoriteFacilitySlots);
            if (evictSlot < 0) return false;
            preferredSlot = evictSlot;
        }
        return SaveFacility(facility, preferredSlot, customName);
    }

    public bool SmartSaveGridTerrain(Grids grid, int preferredSlot, string customName = null)
    {
        if (grid == null) return false;
        bool isEmpty = preferredSlot >= 0 && preferredSlot < MAX_SLOTS && 
            Data.savedGridTerrains[preferredSlot].savedTimestamp == 0;
        if (!isEmpty)
        {
            int evictSlot = FindAutoEvictSlot(Data.savedGridTerrains, Data.favoriteGridSlots);
            if (evictSlot < 0) return false;
            preferredSlot = evictSlot;
        }
        return SaveGridTerrain(grid, preferredSlot, customName);
    }

    // ==================== 收藏系统 ====================

    public bool ToggleFavorite<T>(List<T> list, List<string> favorites, int slotIndex) where T : class
    {
        if (slotIndex < 0 || slotIndex >= list.Count) return false;

        var item = list[slotIndex];
        string slotName = "";
        if (item is SavedUnitData u) slotName = u.slotName;
        else if (item is SavedWeaponData w) slotName = w.slotName;
        else if (item is SavedFacilityData f) slotName = f.slotName;
        else if (item is SavedGridTerrainData g) slotName = g.slotName;

        if (favorites.Contains(slotName))
        {
            favorites.Remove(slotName);
            if (item is SavedUnitData u2) u2.isFavorite = false;
            else if (item is SavedWeaponData w2) w2.isFavorite = false;
            else if (item is SavedFacilityData f2) f2.isFavorite = false;
            else if (item is SavedGridTerrainData g2) g2.isFavorite = false;
        }
        else
        {
            if (favorites.Count >= MAX_FAVORITES) return false;
            favorites.Add(slotName);
            if (item is SavedUnitData u2) u2.isFavorite = true;
            else if (item is SavedWeaponData w2) w2.isFavorite = true;
            else if (item is SavedFacilityData f2) f2.isFavorite = true;
            else if (item is SavedGridTerrainData g2) g2.isFavorite = true;
        }

        SaveAllData();
        return true;
    }

    public bool ToggleUnitFavorite(int slotIndex) => ToggleFavorite(Data.savedUnits, Data.favoriteUnitSlots, slotIndex);
    public bool ToggleWeaponFavorite(int slotIndex) => ToggleFavorite(Data.savedWeapons, Data.favoriteWeaponSlots, slotIndex);
    public bool ToggleFacilityFavorite(int slotIndex) => ToggleFavorite(Data.savedFacilities, Data.favoriteFacilitySlots, slotIndex);
    public bool ToggleGridFavorite(int slotIndex) => ToggleFavorite(Data.savedGridTerrains, Data.favoriteGridSlots, slotIndex);

    // ==================== 改名 ====================

    public bool RenameUnit(int slotIndex, string newName)
    {
        if (slotIndex < 0 || slotIndex >= Data.savedUnits.Count || string.IsNullOrWhiteSpace(newName)) return false;
        Data.savedUnits[slotIndex].displayName = newName;
        Data.savedUnits[slotIndex].savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(); // 改名也算修改
        SaveAllData();
        return true;
    }

    public bool RenameWeapon(int slotIndex, string newName)
    {
        if (slotIndex < 0 || slotIndex >= Data.savedWeapons.Count || string.IsNullOrWhiteSpace(newName)) return false;
        Data.savedWeapons[slotIndex].displayName = newName;
        Data.savedWeapons[slotIndex].savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        SaveAllData();
        return true;
    }

    public bool RenameFacility(int slotIndex, string newName)
    {
        if (slotIndex < 0 || slotIndex >= Data.savedFacilities.Count || string.IsNullOrWhiteSpace(newName)) return false;
        Data.savedFacilities[slotIndex].displayName = newName;
        Data.savedFacilities[slotIndex].savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        SaveAllData();
        return true;
    }

    public bool RenameGridTerrain(int slotIndex, string newName)
    {
        if (slotIndex < 0 || slotIndex >= Data.savedGridTerrains.Count || string.IsNullOrWhiteSpace(newName)) return false;
        Data.savedGridTerrains[slotIndex].displayName = newName;
        Data.savedGridTerrains[slotIndex].savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        SaveAllData();
        return true;
    }

    public bool RenameMap(int slotIndex, string newName)
    {
        if (slotIndex < 0 || slotIndex >= Data.savedMaps.Count || string.IsNullOrWhiteSpace(newName)) return false;
        Data.savedMaps[slotIndex].displayName = newName;
        Data.savedMaps[slotIndex].savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        SaveAllData();
        return true;
    }

    // ==================== 更新使用时间（加载/放置时调用） ====================
    public void UpdateUnitLastUsed(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Data.savedUnits.Count)
        {
            Data.savedUnits[slotIndex].lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            SaveAllData();
        }
    }
    public void UpdateWeaponLastUsed(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Data.savedWeapons.Count)
        {
            Data.savedWeapons[slotIndex].lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            SaveAllData();
        }
    }
    public void UpdateFacilityLastUsed(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Data.savedFacilities.Count)
        {
            Data.savedFacilities[slotIndex].lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            SaveAllData();
        }
    }
    public void UpdateGridLastUsed(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Data.savedGridTerrains.Count)
        {
            Data.savedGridTerrains[slotIndex].lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            SaveAllData();
        }
    }
    public void UpdateMapLastUsed(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Data.savedMaps.Count)
        {
            Data.savedMaps[slotIndex].lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            SaveAllData();
        }
    }

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

    private void LoadCustomMoveTypes()
    {
        if (!Godot.FileAccess.FileExists(CUSTOM_MOVE_TYPES_FILE)) return;

        try
        {
            using var file = Godot.FileAccess.Open(CUSTOM_MOVE_TYPES_FILE, Godot.FileAccess.ModeFlags.Read);
            var dict = Json.ParseString(file.GetAsText()).AsGodotDictionary();
            if (dict == null) return;

            CustomMoveTypes.Clear();
            Data.customMoveTypes.Clear();

            foreach (var k in dict.Keys)
            {
                var d = dict[k].AsGodotDictionary();
                if (d == null) continue;

                var data = new CustomMoveTypeData
                {
                    id = d.GetValueOrDefault("id", "").AsString(),
                    displayName = d.GetValueOrDefault("displayName", "").AsString(),
                    isBuiltIn = d.GetValueOrDefault("isBuiltIn", false).AsBool(),
                    moveCosts = ParseIntDict(d.GetValueOrDefault("moveCosts", new Variant()).AsGodotDictionary()),
                    customTexturePath = d.GetValueOrDefault("customTexturePath", "").AsString(),
                    useCustomTexture = d.GetValueOrDefault("useCustomTexture", false).AsBool()
                };

                CustomMoveTypes[data.id] = data;
                Data.customMoveTypes.Add(data);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ExtraSave] {TranslateGroup.T("加载自定义移动方式失败")}: {e.Message}");
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

    private void LoadCustomTerrainTypes()
    {
        if (!Godot.FileAccess.FileExists(CUSTOM_TERRAIN_TYPES_FILE)) return;

        try
        {
            using var file = Godot.FileAccess.Open(CUSTOM_TERRAIN_TYPES_FILE, Godot.FileAccess.ModeFlags.Read);
            var dict = Json.ParseString(file.GetAsText()).AsGodotDictionary();
            if (dict == null) return;

            CustomTerrainTypes.Clear();
            Data.customTerrainTypes.Clear();

            foreach (var k in dict.Keys)
            {
                var d = dict[k].AsGodotDictionary();
                if (d == null) continue;

                var data = new CustomTerrainTypeData
                {
                    id = d.GetValueOrDefault("id", "").AsString(),
                    displayName = d.GetValueOrDefault("displayName", "").AsString(),
                    isBuiltIn = d.GetValueOrDefault("isBuiltIn", false).AsBool(),
                    moveCosts = ParseIntDict(d.GetValueOrDefault("moveCosts", new Variant()).AsGodotDictionary()),
                    defenseBonus = d.GetValueOrDefault("defenseBonus", 0f).AsSingle(),
                    terrainColorHex = d.GetValueOrDefault("terrainColorHex", "ffffff").AsString(),
                    customTexturePath = d.GetValueOrDefault("customTexturePath", "").AsString(),
                    useCustomTexture = d.GetValueOrDefault("useCustomTexture", false).AsBool()
                };

                CustomTerrainTypes[data.id] = data;
                Data.customTerrainTypes.Add(data);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ExtraSave] {TranslateGroup.T("加载自定义地形失败")}: {e.Message}");
        }
    }

    // ==================== 地图保存/加载 ====================

    public bool SaveCurrentMap(int slotIndex, string customName, GameManager gm, GridManager gridMgr)
    {
        if (slotIndex < 0 || slotIndex >= MAX_MAP_SLOTS || gm == null || gridMgr == null) return false;

        var mapData = new SavedMapData
        {
            slotName = $"{TranslateGroup.T("地图")}{slotIndex + 1}",
            displayName = customName ?? $"{TranslateGroup.T("地图")}{slotIndex + 1}",
            savedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            lastUsedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            width = gridMgr.map?.GetLength(0) ?? 0,
            height = gridMgr.map?.GetLength(1) ?? 0,
            fogOfWarEnabled = gm.fogOfWarManager?.isFogOfWarEnabled ?? false,
            p1Funds = gm.p1Funds,
            p2Funds = gm.p2Funds
        };

        // 保存地形
        if (gridMgr.map != null)
        {
            for (int y = 0; y < gridMgr.map.GetLength(1); y++)
            {
                for (int x = 0; x < gridMgr.map.GetLength(0); x++)
                {
                    var grid = gridMgr.map[x, y];
                    mapData.terrainIds.Add(grid != null ? (int)grid.gridType : 0);
                }
            }
        }

        // 保存单位
        if (gm.unitManager?.AllUnits != null)
        {
            foreach (var unit in gm.unitManager.AllUnits)
            {
                if (unit == null || !IsInstanceValid(unit) || unit.grid == null) continue;
                var uData = new SavedUnitData
                {
                    slotName = unit.Name,
                    displayName = unit.Name,
                    scenePath = GetScenePathForInstance(unit),
                    typeName = unit.GetType().Name,
                    team = unit.team,
                    fieldValues = SerializeObjectFields(unit),
                    attackMatrix = ToSystemDict(unit.attackMatrix),
                    secondaryAttackMatrix = ToSystemDict(unit.secondaryAttackMatrix)
                };
                // 记录格子坐标用于加载
                uData.fieldValues["gridX"] = unit.grid.GridIndex.X.ToString();
                uData.fieldValues["gridY"] = unit.grid.GridIndex.Y.ToString();
                mapData.units.Add(uData);
            }
        }

        // 保存兵器
        if (gm.weaponManager?.AllWeapons != null)
        {
            foreach (var weapon in gm.weaponManager.AllWeapons)
            {
                if (weapon == null || !IsInstanceValid(weapon) || weapon.grid == null) continue;
                var wData = new SavedWeaponData
                {
                    slotName = weapon.Name,
                    displayName = weapon.Name,
                    scenePath = GetScenePathForInstance(weapon),
                    typeName = weapon.GetType().Name,
                    team = weapon.team,
                    fieldValues = SerializeObjectFields(weapon)
                };
                // 记录格子坐标用于加载
                wData.fieldValues["gridX"] = weapon.grid.GridIndex.X.ToString();
                wData.fieldValues["gridY"] = weapon.grid.GridIndex.Y.ToString();
                mapData.weapons.Add(wData);
            }
        }

        // 保存设施
        if (gridMgr.grids != null)
        {
            foreach (var grid in gridMgr.grids)
            {
                if (grid?.city == null || !IsInstanceValid(grid.city)) continue;
                var fData = new SavedFacilityData
                {
                    slotName = grid.city.Name,
                    displayName = grid.city.Name,
                    scenePath = GetScenePathForInstance(grid.city),
                    typeName = grid.city.GetType().Name,
                    team = grid.city.facilityTeam,
                    fieldValues = SerializeObjectFields(grid.city)
                };
                // 记录格子坐标用于加载
                fData.fieldValues["gridX"] = grid.GridIndex.X.ToString();
                fData.fieldValues["gridY"] = grid.GridIndex.Y.ToString();
                mapData.facilities.Add(fData);
            }
        }

        Data.savedMaps[slotIndex] = mapData;
        SaveAllData();
        return true;
    }

    /// <summary>
    /// 从保存的地图数据加载到游戏中（ TerrainEditor 调用）
    /// </summary>
    public bool LoadMapToGame(int slotIndex, GameManager gm, GridManager gridMgr, TerrainEditor terrainEditor)
    {
        if (slotIndex < 0 || slotIndex >= Data.savedMaps.Count) return false;
        var mapData = Data.savedMaps[slotIndex];
        if (mapData.width == 0) return false;

        // 更新时间戳
        UpdateMapLastUsed(slotIndex);

        if (gm == null || gridMgr == null || terrainEditor == null) return false;

        // 1. 清理现有实体
        terrainEditor.ClearAllMapEntities();

        // 2. 调整地图大小
        gridMgr.ResizeMap(mapData.width, mapData.height);
        gridMgr.Init();

        // 3. 加载地形
        int terrainIdx = 0;
        for (int y = 0; y < mapData.height; y++)
        {
            for (int x = 0; x < mapData.width; x++)
            {
                if (terrainIdx < mapData.terrainIds.Count)
                {
                    var grid = gridMgr.map[x, y];
                    if (grid != null)
                    {
                        grid.gridType = (GridType)mapData.terrainIds[terrainIdx];
                        terrainEditor.UpdateGridVisual(grid);
                    }
                }
                terrainIdx++;
            }
        }

        // 4. 加载设施
        foreach (var fData in mapData.facilities)
        {
            if (string.IsNullOrEmpty(fData.scenePath)) continue;
            var grid = FindGridByIndex(gridMgr, fData.fieldValues.GetValueOrDefault("gridX", "0"), fData.fieldValues.GetValueOrDefault("gridY", "0"));
            if (grid == null) continue;
            terrainEditor.SpawnSavedFacilityAtGrid(grid, fData);
        }

        // 5. 加载兵器
        foreach (var wData in mapData.weapons)
        {
            if (string.IsNullOrEmpty(wData.scenePath)) continue;
            var grid = FindGridByIndex(gridMgr, wData.fieldValues.GetValueOrDefault("gridX", "0"), wData.fieldValues.GetValueOrDefault("gridY", "0"));
            if (grid == null) continue;
            terrainEditor.SpawnSavedWeaponAtGrid(grid, wData);
        }

        // 6. 加载单位
        foreach (var uData in mapData.units)
        {
            if (string.IsNullOrEmpty(uData.scenePath)) continue;
            var grid = FindGridByIndex(gridMgr, uData.fieldValues.GetValueOrDefault("gridX", "0"), uData.fieldValues.GetValueOrDefault("gridY", "0"));
            if (grid == null) continue;
            terrainEditor.SpawnSavedUnitAtGrid(grid, uData);
        }

        // 7. 加载资金
        gm.p1Funds = mapData.p1Funds;
        gm.p2Funds = mapData.p2Funds;
        gm.fogOfWarManager?.RefreshFog();
        gm.UpdateUnitLists();
        gm.RefreshSpecializedUnitLists();

        return true;
    }

    private Grids FindGridByIndex(GridManager gridMgr, string xStr, string yStr)
    {
        if (int.TryParse(xStr, out int x) && int.TryParse(yStr, out int y))
        {
            if (x >= 0 && x < gridMgr.map.GetLength(0) && y >= 0 && y < gridMgr.map.GetLength(1))
                return gridMgr.map[x, y];
        }
        return null;
    }

    public SavedMapData GetMapData(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= Data.savedMaps.Count) return null;
        return Data.savedMaps[slotIndex];
    }

    // Godot 路径拼接（统一使用正斜杠）
    private string GodotPathCombine(params string[] parts)
    {
        return string.Join("/", parts).Replace("\\", "/");
    }

    // ==================== 序列化辅助方法 ====================

    private Dictionary<string, string> SerializeObjectFields(object obj)
    {
        var result = new Dictionary<string, string>();
        if (obj == null) return result;

        var type = obj.GetType();
        var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(f => f.GetCustomAttribute<ExportAttribute>() != null);

        foreach (var field in fields)
        {
            try
            {
                var value = field.GetValue(obj);
                if (value != null)
                {
                    result[field.Name] = ValueToString(value);
                }
            }
            catch (System.Exception ex) { GD.PushWarning($"[ExtraSave] {ex.GetType().Name}: {ex.Message}"); }
        }

        var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ExportAttribute>() != null && p.CanRead && p.CanWrite);

        foreach (var prop in props)
        {
            try
            {
                var value = prop.GetValue(obj);
                if (value != null)
                {
                    result[prop.Name] = ValueToString(value);
                }
            }
            catch (System.Exception ex) { GD.PushWarning($"[ExtraSave] {ex.GetType().Name}: {ex.Message}"); }
        }

        return result;
    }

    private string ValueToString(object value)
    {
        if (value == null) return "null";
        if (value is bool b) return b ? "true" : "false";
        if (value is int i) return i.ToString();
        if (value is uint ui) return ui.ToString();
        if (value is long l) return l.ToString();
        if (value is float f) return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (value is double d) return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (value is string s) return s;
        if (value is Color c) return c.ToHtml();
        if (value is Vector2 v2) return $"({v2.X},{v2.Y})";
        if (value is Vector2I v2i) return $"({v2i.X},{v2i.Y})";
        if (value.GetType().IsEnum) return ((int)value).ToString();
        return value.ToString();
    }

    private string GetScenePathForInstance(Node instance)
    {
        if (instance == null) return "";
        // 尝试从场景文件路径推断
        var sceneFile = instance.SceneFilePath;
        if (!string.IsNullOrEmpty(sceneFile)) return sceneFile;

        //  fallback：根据类型名推断
        return $"res://Prefabs/{instance.GetType().Name.ToLower()}.tscn";
    }

    private Dictionary<string, int> ToSystemDict(Godot.Collections.Dictionary<string, int> godotDict)
    {
        var dict = new Dictionary<string, int>();
        if (godotDict == null) return dict;
        foreach (var kvp in godotDict) dict[kvp.Key] = kvp.Value;
        return dict;
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

    // ==================== 获取保存数据（供外部读取） ====================
    public SavedUnitData GetSavedUnit(int slotIndex) => (slotIndex >= 0 && slotIndex < Data.savedUnits.Count) ? Data.savedUnits[slotIndex] : null;
    public SavedWeaponData GetSavedWeapon(int slotIndex) => (slotIndex >= 0 && slotIndex < Data.savedWeapons.Count) ? Data.savedWeapons[slotIndex] : null;
    public SavedFacilityData GetSavedFacility(int slotIndex) => (slotIndex >= 0 && slotIndex < Data.savedFacilities.Count) ? Data.savedFacilities[slotIndex] : null;
    public SavedGridTerrainData GetSavedGridTerrain(int slotIndex) => (slotIndex >= 0 && slotIndex < Data.savedGridTerrains.Count) ? Data.savedGridTerrains[slotIndex] : null;
    public SavedMapData GetSavedMap(int slotIndex) => (slotIndex >= 0 && slotIndex < Data.savedMaps.Count) ? Data.savedMaps[slotIndex] : null;

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

    // ==================== 保存数据直接获取（供TerrainEditor使用） ====================
    /// <summary>
    /// 直接获取单位保存数据（用于TerrainEditor生成）
    /// </summary>
    public SavedUnitData GetSavedUnitData(int slotIndex) => GetSavedUnit(slotIndex);
    public SavedWeaponData GetSavedWeaponData(int slotIndex) => GetSavedWeapon(slotIndex);
    public SavedFacilityData GetSavedFacilityData(int slotIndex) => GetSavedFacility(slotIndex);
    public SavedGridTerrainData GetSavedGridData(int slotIndex) => GetSavedGridTerrain(slotIndex);

    // ==================== 清空槽位 ====================
    public void ClearUnitSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Data.savedUnits.Count)
        {
            Data.savedUnits[slotIndex] = new SavedUnitData { slotName = $"{TranslateGroup.T("单位")}{slotIndex + 1}" };
            SaveAllData();
        }
    }
    public void ClearWeaponSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Data.savedWeapons.Count)
        {
            Data.savedWeapons[slotIndex] = new SavedWeaponData { slotName = $"{TranslateGroup.T("兵器")}{slotIndex + 1}" };
            SaveAllData();
        }
    }
    public void ClearFacilitySlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Data.savedFacilities.Count)
        {
            Data.savedFacilities[slotIndex] = new SavedFacilityData { slotName = $"{TranslateGroup.T("设施")}{slotIndex + 1}" };
            SaveAllData();
        }
    }
    public void ClearGridSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Data.savedGridTerrains.Count)
        {
            Data.savedGridTerrains[slotIndex] = new SavedGridTerrainData { slotName = $"{TranslateGroup.T("地形")}{slotIndex + 1}" };
            SaveAllData();
        }
    }
    public void ClearMapSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Data.savedMaps.Count)
        {
            Data.savedMaps[slotIndex] = new SavedMapData { slotName = $"{TranslateGroup.T("地图")}{slotIndex + 1}" };
            SaveAllData();
        }
    }
}
