// UnitRegistry.cs —— 兵种中央注册表
using Godot;
using System.Collections.Generic;

// ✅ 防御加成类型（避免编译顺序依赖）
public enum DefenseBonusType { NoDefenseBonus, CanDefenseBonus }

/// <summary>
/// 兵种完整数据配置。新增兵种只需要在这里加一行。
/// </summary>
public class UnitConfig
{
    // 基础
    public string UnitId;           // 唯一标识，如 "Infantry"
    public string DisplayName;      // 显示名，如 "步兵"
    public MoveType MoveType;       // 移动方式
    public int MovePoints;          // 移动力
    public int Cost;                // 造价

    // 视觉
    public string AnimPrefix;       // 动画前缀，如 "infantry" → 实际找 "infantry1"/"infantry2"

    // 武器 - 主武器
    public bool HasPrimaryWeapon;
    public int MaxPrimaryAmmo;
    public bool PrimaryHasLimitedAmmo = true;
    public bool PrimaryAntiArmor;
    public bool PrimaryAntiInfantry;
    public int BaseAttack = 0;

    // 武器 - 副武器
    public bool HasSecondaryWeapon;
    public int SecondaryAttack;
    public bool SecondaryAntiArmor;
    public bool SecondaryAntiInfantry;

    // 射程
    public int MinAttackRange = 1;
    public int MaxAttackRange = 1;
    public bool UseMinMaxAttackRange = false;
    public bool CanAttackAfterMoving = true;
    public bool CannotCounterWhenAttacked = false;
    public bool CanCounterWhenDefending = true;
    public bool CanCounterAtRange = false;

    // 燃料
    public int MaxFuel = 99;
    public bool ConsumeFuel = false;
    public int DailyFuelConsumption = 0;
    public bool DestroyOnOutOfFuel = false;
    public int LowFuelThreshold = 15;

    // 防御
    public int BaseDefense = 0;
    public float CounterMul = 0.5f;

    // 占领
    public CaptureAbility CaptureAbility = CaptureAbility.CannotCapture;
    public int CapturePower = 10;
    public bool AllowCaptureAfterMove = true;

    // 搭载
    public bool CanTransportUnits = false;
    public bool CanSupplyUnits = false;
    public int MaxTransportCapacity = 0;
    public string[] TransportUnitTypes = System.Array.Empty<string>();

    // 特殊能力
    public bool CanDevour = false;
    public bool CanExplode = false;
    public bool CanIlluminate = false;

    // 重叠规则
    public UnitOverlapType OverlapType = UnitOverlapType.NonOverlapping;
    public AttackType AttackType = AttackType.CanAttack;
    public bool IsArmoredUnit = false;
    public bool IsAirUnit = false;
    public DefenseBonusType DefenseBonusType = DefenseBonusType.CanDefenseBonus;

    // 视野
    public int VisionRange = -1;  // -1 = 用配置表默认值
}

public static class UnitRegistry
{
    /// <summary>
    /// 所有兵种配置集中在这里。新增兵种 = 往字典里加一行。
    /// </summary>
    public static readonly Dictionary<string, UnitConfig> Configs = new()
    {
        // ==================== 步兵类 ====================
        ["Infantry"] = new UnitConfig
        {
            UnitId = "Infantry",
            DisplayName = "步兵",
            MoveType = MoveType.Infantry,
            MovePoints = 3,
            Cost = 1000,
            AnimPrefix = "infantry",
            HasPrimaryWeapon = false,
            HasSecondaryWeapon = true,
            SecondaryAttack = 100,
            SecondaryAntiInfantry = true,
            SecondaryAntiArmor = false,
            CaptureAbility = CaptureAbility.CanCapture,
            CapturePower = 10,
            OverlapType = UnitOverlapType.NonOverlapping,
        },

        ["Mech"] = new UnitConfig
        {
            UnitId = "Mech",
            DisplayName = "机甲",
            MoveType = MoveType.Mech,
            MovePoints = 2,
            Cost = 3000,
            AnimPrefix = "mech",
            HasPrimaryWeapon = true,
            MaxPrimaryAmmo = 3,
            PrimaryAntiArmor = true,
            PrimaryAntiInfantry = false,
            HasSecondaryWeapon = true,
            SecondaryAttack = 80,
            SecondaryAntiArmor = true,
            SecondaryAntiInfantry = true,
            CaptureAbility = CaptureAbility.CanCapture,
            CapturePower = 10,
            MaxFuel = 70,
            ConsumeFuel = true,
            LowFuelThreshold = 30,
            CounterMul = 0.4f,
            CanTransportUnits = true,
            MaxTransportCapacity = 1,
            TransportUnitTypes = new[] { "Infantry" },
        },

        ["Bike"] = new UnitConfig
        {
            UnitId = "Bike",
            DisplayName = "摩托兵",
            MoveType = MoveType.Tire,
            MovePoints = 5,
            Cost = 2500,
            AnimPrefix = "bike",
            HasPrimaryWeapon = false,
            HasSecondaryWeapon = true,
            SecondaryAttack = 100,
            SecondaryAntiArmor = true,
            SecondaryAntiInfantry = true,
            CaptureAbility = CaptureAbility.CanCapture,
            CapturePower = 10,
            MaxFuel = 70,
            ConsumeFuel = true,
            LowFuelThreshold = 30,
            BaseAttack = 10,
        },

        // ==================== 坦克类 ====================
        ["LightTank"] = new UnitConfig
        {
            UnitId = "LightTank",
            DisplayName = "轻型坦克",
            MoveType = MoveType.Treads,
            MovePoints = 6,
            Cost = 7000,
            AnimPrefix = "lighttank",
            HasPrimaryWeapon = true,
            MaxPrimaryAmmo = 9,
            PrimaryAntiArmor = true,
            PrimaryAntiInfantry = true,
            HasSecondaryWeapon = true,
            SecondaryAttack = 100,
            SecondaryAntiArmor = true,
            SecondaryAntiInfantry = true,
            MaxFuel = 70,
            ConsumeFuel = true,
            LowFuelThreshold = 30,
            IsArmoredUnit = true,
            CanTransportUnits = true,
            MaxTransportCapacity = 2,
            TransportUnitTypes = new[] { "Infantry", "Mech" },
        },

        ["MdTank"] = new UnitConfig
        {
            UnitId = "MdTank",
            DisplayName = "重型坦克",
            MoveType = MoveType.Treads,
            MovePoints = 5,
            Cost = 16000,
            AnimPrefix = "mdtank",
            HasPrimaryWeapon = true,
            MaxPrimaryAmmo = 8,
            PrimaryAntiArmor = true,
            PrimaryAntiInfantry = true,
            HasSecondaryWeapon = true,
            SecondaryAttack = 100,
            SecondaryAntiInfantry = true,
            SecondaryAntiArmor = false,
            MaxFuel = 50,
            ConsumeFuel = true,
            IsArmoredUnit = true,
        },

        // ==================== 支援/间接 ====================
        ["Recon"] = new UnitConfig
        {
            UnitId = "Recon",
            DisplayName = "侦察车",
            MoveType = MoveType.Tire,
            MovePoints = 8,
            Cost = 4000,
            AnimPrefix = "recon",
            HasPrimaryWeapon = false,
            HasSecondaryWeapon = true,
            SecondaryAttack = 100,
            MaxFuel = 80,
            ConsumeFuel = true,
            LowFuelThreshold = 30,
        },

        ["APC"] = new UnitConfig
        {
            UnitId = "APC",
            DisplayName = "装甲运兵车",
            MoveType = MoveType.Treads,
            MovePoints = 6,
            Cost = 5000,
            AnimPrefix = "apc",
            AttackType = AttackType.NoAttack,
            HasPrimaryWeapon = false,
            HasSecondaryWeapon = false,
            MaxFuel = 70,
            ConsumeFuel = true,
            CanTransportUnits = true,
            CanSupplyUnits = true,
            MaxTransportCapacity = 1,
            TransportUnitTypes = new[] { "Infantry", "Mech" },
            CounterMul = 0f,
        },

        ["Artillery"] = new UnitConfig
        {
            UnitId = "Artillery",
            DisplayName = "自行火炮",
            MoveType = MoveType.Treads,
            MovePoints = 5,
            Cost = 6000,
            AnimPrefix = "artillery",
            HasPrimaryWeapon = true,
            MaxPrimaryAmmo = 9,
            PrimaryAntiArmor = true,
            PrimaryAntiInfantry = true,
            HasSecondaryWeapon = false,
            MinAttackRange = 2,
            MaxAttackRange = 3,
            UseMinMaxAttackRange = true,
            CanAttackAfterMoving = false,
            CannotCounterWhenAttacked = true,
            CanCounterWhenDefending = false,
            MaxFuel = 50,
            ConsumeFuel = true,
            CounterMul = 0.3f,
        },

        ["Rocket"] = new UnitConfig
        {
            UnitId = "Rocket",
            DisplayName = "火箭炮",
            MoveType = MoveType.Tire,
            MovePoints = 4,
            Cost = 15000,
            AnimPrefix = "rocket",
            HasPrimaryWeapon = true,
            MaxPrimaryAmmo = 6,
            PrimaryAntiArmor = true,
            PrimaryAntiInfantry = true,
            HasSecondaryWeapon = false,
            MinAttackRange = 3,
            MaxAttackRange = 5,
            UseMinMaxAttackRange = true,
            CanAttackAfterMoving = false,
            CannotCounterWhenAttacked = true,
            CanCounterWhenDefending = false,
            MaxFuel = 50,
            ConsumeFuel = true,
            CounterMul = 0.3f,
        },

        ["AntiAir"] = new UnitConfig
        {
            UnitId = "AntiAir",
            DisplayName = "防空高炮",
            MoveType = MoveType.Treads,
            MovePoints = 6,
            Cost = 8000,
            AnimPrefix = "antiair",
            HasPrimaryWeapon = true,
            MaxPrimaryAmmo = 9,
            PrimaryAntiArmor = true,
            PrimaryAntiInfantry = true,
            HasSecondaryWeapon = false,
            MaxFuel = 60,
            ConsumeFuel = true,
        },

        ["AntiTank"] = new UnitConfig
        {
            UnitId = "AntiTank",
            DisplayName = "反坦克炮",
            MoveType = MoveType.Tire,
            MovePoints = 4,
            Cost = 11000,
            AnimPrefix = "antitank",
            HasPrimaryWeapon = true,
            MaxPrimaryAmmo = 6,
            PrimaryAntiArmor = true,
            PrimaryAntiInfantry = true,
            HasSecondaryWeapon = false,
            MinAttackRange = 1,
            MaxAttackRange = 3,
            UseMinMaxAttackRange = true,
            CanAttackAfterMoving = false,
            CannotCounterWhenAttacked = true,
            CanCounterWhenDefending = true,
            CanCounterAtRange = false,
            MaxFuel = 50,
            ConsumeFuel = true,
            CounterMul = 0.3f,
            BaseAttack = 550,
        },

        ["Flare"] = new UnitConfig
        {
            UnitId = "Flare",
            DisplayName = "照明车",
            MoveType = MoveType.Treads,
            MovePoints = 5,
            Cost = 5000,
            AnimPrefix = "flare",
            HasPrimaryWeapon = false,
            HasSecondaryWeapon = true,
            SecondaryAttack = 100,
            SecondaryAntiArmor = true,
            SecondaryAntiInfantry = true,
            MaxFuel = 60,
            ConsumeFuel = true,
            LowFuelThreshold = 30,
            CanIlluminate = true,
        },

        ["PipeRunner"] = new UnitConfig
        {
            UnitId = "PipeRunner",
            DisplayName = "管道炮",
            MoveType = MoveType.PipeRunner,
            MovePoints = 5,
            Cost = 20000,
            AnimPrefix = "piperunner",
            HasPrimaryWeapon = true,
            MaxPrimaryAmmo = 99,
            PrimaryHasLimitedAmmo = false,
            PrimaryAntiArmor = true,
            PrimaryAntiInfantry = true,
            HasSecondaryWeapon = false,
            MinAttackRange = 2,
            MaxAttackRange = 5,
            UseMinMaxAttackRange = true,
            CanAttackAfterMoving = false,
            CannotCounterWhenAttacked = true,
            CanCounterWhenDefending = false,
            IsArmoredUnit = true,
        },

        // ==================== 特殊 ====================
        ["Oozium"] = new UnitConfig
        {
            UnitId = "Oozium",
            DisplayName = "史莱姆",
            MoveType = MoveType.Oozium,
            MovePoints = 1,
            Cost = 20000,
            AnimPrefix = "oozium",
            HasPrimaryWeapon = false,
            HasSecondaryWeapon = false,
            CanDevour = true,
            OverlapType = UnitOverlapType.Oozium,
        },

        ["FlyBomb"] = new UnitConfig
        {
            UnitId = "FlyBomb",
            DisplayName = "飞弹",
            MoveType = MoveType.AirPlane,
            MovePoints = 9,
            Cost = 25000,
            AnimPrefix = "flybomb",
            HasPrimaryWeapon = false,
            HasSecondaryWeapon = false,
            AttackType = AttackType.NoAttack,
            CanExplode = true,
            IsAirUnit = true,
            MaxFuel = 45,
            ConsumeFuel = true,
            DailyFuelConsumption = 5,
            DestroyOnOutOfFuel = true,
        },

        // ==================== 空军（预留，你加贴图就行）====================
        /*
        ["Fighter"] = new UnitConfig
        {
            UnitId = "Fighter",
            DisplayName = "战斗机",
            MoveType = MoveType.AirPlane,
            MovePoints = 9,
            Cost = 20000,
            AnimPrefix = "fighter",
            HasPrimaryWeapon = true,
            MaxPrimaryAmmo = 99,
            PrimaryHasLimitedAmmo = false,
            PrimaryAntiAir = true,
            IsAirUnit = true,
            MaxFuel = 99,
            ConsumeFuel = true,
            DailyFuelConsumption = 5,
            DestroyOnOutOfFuel = true,
        },
        */
    };

    /// <summary>
    /// 获取兵种配置，找不到返回null
    /// </summary>
    public static UnitConfig Get(string unitId)
    {
        if (Configs.TryGetValue(unitId, out var config))
            return config;
        return null;
    }

    /// <summary>
    /// 获取所有兵种ID列表
    /// </summary>
    public static List<string> GetAllUnitIds() => new List<string>(Configs.Keys);

    /// <summary>
    /// 判断是否存在该兵种
    /// </summary>
    public static bool Has(string unitId) => Configs.ContainsKey(unitId);
}
