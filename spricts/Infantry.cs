using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public enum MoveType
{
    Infantry,
    Mech,
    Oozium,
    Treads,

    Naval,          // 海军
    AirPlane,       // 战机
    AirShip,        // 飞艇
    Drone,          // 无人机（不能过LAVA和SEA）
    AeroSpacer,     // 空天战机（全部地形=1，无任何限制）
    HeliCopter,     // 直升机（不能过LAVA，可以过SEA）
    SpaceShiper,    // 战舰（全部地形=1，无任何限制）
    Hover,          // 气垫（水陆两用）
    PipeRunner,    // 管道行者（仅限管道地形）
    Tire,

    LAVARUNNER,     // 岩浆行者 - 仅通过LAVA(1)和TP(0)
    LAVAHOVER,      // 岩浆登陆者 - 通过LAVA(1), LAVASIDE(1), TP(0)

    Train,          // 火车 - 仅铁轨类地形
    GasTrain,       // 蒸汽车 - 仅铁轨类地形
    FASTER,         // 高铁 - 仅铁轨类地形，部分地形消耗为0
    Missile,        // 洲际导弹 - 高空飞行，常规地形=1，特殊障碍消耗大
}

public enum UnitOverlapType {
    NonOverlapping,
    Overlapping,
    Oozium
}

public enum AttackOverlapPolicy {
    AllowAttackEnemyOnSameTile,
    ForbidAttackEnemyOnSameTile
}

public enum UnitState {
    Idle,
    Moved,
    Acted
}
public enum CaptureAbility
{
    CanCapture,      // 可以占领
    CannotCapture    // 不能占领
}

public enum AttackType
{
    CanAttack,
    NoAttack
}

public enum WeaponType
{
    None,
    Primary,
    Secondary
}

public partial class Infantry : Node2D
{
    // ========== ✅ 究极自由系统：所有单位类型特性都可通过Inspector控制 ==========

    // ========== ✅ 兵种身份：不写代码造新兵种 ==========
    // 新兵种 = 复制一个现有单位场景 → 填 unitTypeId（唯一）→ 在检查器里调参数 → 放进 res://Prefabs/CustomUnits/ 即自动进入生产名单
    [ExportGroup("兵种身份（自定义兵种）")]
    [Export] public string unitTypeId = "";          // 兵种ID：留空=脚本类名（原有兵种不用填）；攻防表/过滤名单/运输/补给/AI 全部按它识别
    [Export] public string unitDisplayName = "";     // 生产菜单显示名（留空=兵种ID）
    [Export] public string templateType = "";        // 参照兵种：攻防表查不到本ID条目、视野/默认配置时按此兵种（留空=脚本类名）
    /// <summary>兵种身份ID（替代 GetType().Name 的所有身份判定）</summary>
    public string UnitTypeId => string.IsNullOrEmpty(unitTypeId) ? GetType().Name : unitTypeId;
    /// <summary>参照兵种ID（静态配置表/攻防表兜底）</summary>
    public string TemplateId => string.IsNullOrEmpty(templateType) ? GetType().Name : templateType;
    /// <summary>显示名：unitDisplayName → 兵种配置显示名 → 兵种ID</summary>
    public string DisplayLabel => !string.IsNullOrEmpty(unitDisplayName) ? unitDisplayName : (UnitRegistry.Get(UnitTypeId)?.DisplayName ?? UnitTypeId);

    [ExportGroup("究极自由：单位类型特性")]
    [Export] public bool cannotCounterWhenAttacked = false; // 攻击时不被反击（如Artillery/Rocket/AntiTank等间接攻击单位）
    [Export] public bool canCounterWhenDefending = true;   // 被攻击时能否反击（Artillery/Rocket=false，默认true）
    [Export] public bool canCounterAtRange = false;        // 能否远程反击（false=仅近战距离1可反击，true=在攻击范围内可反击）
    [Export] public bool canMerge = true;
    [Export] public bool canActAfterMerge = false;

    [ExportGroup("隐身模块")]
    [Export] public bool canStealth = false;
    [Export] public bool stealthToggleCostsAction = true;    // 隐身/显形是否算独立操作：true=切换后待机；false=切换后仍可攻击移动                            // 可否隐身
    [Export] public bool isStealthed = false;                           // 运行时：当前是否隐身
    // —— 每日消耗（维持隐身时扣，0=该项不扣）——
    [Export] public int stealthFuelCostPerDay = 0;                      // 燃油
    [Export] public int stealthHpCostPerDay = 0;                        // 血量
    [Export] public int stealthPrimaryAmmoCostPerDay = 0;             // 普通弹药
    [Export] public int stealthExplodeAmmoCostPerDay = 0;             // 自爆弹
    [Export] public int stealthFlareAmmoCostPerDay = 0;               // 照明弹
    [Export] public bool stealthForceOneShot = false;    // 阈值效果一次性：true=触发一次后永久锁存；false=持续模式(条件成立期间强制且玩家不可改)
    [Export] public bool stealthFormLocked = false;      // 形态锁定：true=玩家不可隐/现切换（阈值强制不受限，照常在条件满足时改写形态）
    bool stealthForceLatched = false;                    // 运行时：一次性锁存标记
    public bool stealthThresholdLocked = false;          // 运行时：持续模式阈值锁（与手动锁分开存，修复阈值判定覆盖编辑器手动锁）
    public bool IsStealthFormLocked => stealthFormLocked || stealthThresholdLocked;   // ✅ 有效锁定 = 手动锁 或 阈值锁
    [Export] public bool stealthThresholdRevealWins = true;    // 强制隐/强制显同时命中：true=显形优先(原行为) false=隐身优先
    // —— 阈值矩阵：双方向(强制隐/强制显) × 双阈值(高/低) × 八资源 ——
    // 语义=手机电量：高于等于高阈值触发(≥80%性能模式)，低于等于低阈值触发(≤20%省电模式)，两侧持续判定
    [Export] public bool stealthForceHideOneShot = false;      // 强制隐阈值：true=只触发一次(终身)
    [Export] public bool stealthForceRevealOneShot = false;    // 强制显阈值：true=只触发一次(终身)
    [Export] public int stealthForceHideHpHigh = 0;        [Export] public int stealthForceHideHpLow = 0;
    [Export] public int stealthForceRevealHpHigh = 0;      [Export] public int stealthForceRevealHpLow = 0;
    [Export] public int stealthForceHideFuelHigh = 0;      [Export] public int stealthForceHideFuelLow = 0;
    [Export] public int stealthForceRevealFuelHigh = 0;    [Export] public int stealthForceRevealFuelLow = 0;
    [Export] public int stealthForceHidePrimaryHigh = 0;   [Export] public int stealthForceHidePrimaryLow = 0;
    [Export] public int stealthForceRevealPrimaryHigh = 0; [Export] public int stealthForceRevealPrimaryLow = 0;
    [Export] public int stealthForceHideExplodeHigh = 0;   [Export] public int stealthForceHideExplodeLow = 0;
    [Export] public int stealthForceRevealExplodeHigh = 0; [Export] public int stealthForceRevealExplodeLow = 0;
    [Export] public int stealthForceHideFlareHigh = 0;     [Export] public int stealthForceHideFlareLow = 0;
    [Export] public int stealthForceRevealFlareHigh = 0;   [Export] public int stealthForceRevealFlareLow = 0;
    [Export] public int stealthForceHideStealthBulletHigh = 0;   [Export] public int stealthForceHideStealthBulletLow = 0;
    [Export] public int stealthForceRevealStealthBulletHigh = 0; [Export] public int stealthForceRevealStealthBulletLow = 0;
    [Export] public int stealthForceHideRevealBulletHigh = 0;   [Export] public int stealthForceHideRevealBulletLow = 0;
    [Export] public int stealthForceRevealRevealBulletHigh = 0; [Export] public int stealthForceRevealRevealBulletLow = 0;
    [Export] public int stealthForceHideCamoBulletHigh = 0;   [Export] public int stealthForceHideCamoBulletLow = 0;
    [Export] public int stealthForceRevealCamoBulletHigh = 0; [Export] public int stealthForceRevealCamoBulletLow = 0;
    // —— 感知规则 ——
    [Export] public bool stealthVisibleWhenAdjacent = true;           // 贴身可见（X-Y范围）
    [Export] public int stealthVisibleMinRange = 1;                   // 贴身范围X
    [Export] public int stealthVisibleMaxRange = 1;                   // 贴身范围Y
    [Export] public bool stealthVisibleOnTile = true;                 // 同格单位可见（建筑判定见②）
    [Export] public string[] stealthAttackableBy = System.Array.Empty<string>();  // 攻击白名单(空=隐身时无敌)
    // —— 现形规则 ——
    [Export] public bool stealthRevealOnAttack = false;             // 攻击后现形
    [Export] public bool stealthRevealOnDamage = true;              // 受伤现形
    [Export(PropertyHint.Enum, "仅单位攻击,任何掉血")] public int stealthRevealDamageScope = 1;   // 受伤现形的"受伤"范围：0=狭义(只有单位攻击/反击)，1=广义(自爆/兵器/地格/修理扣血等任何掉血)
    [Export] public bool stealthRevealOnFlare = true;               // 照明弹使其现形
    // —— 隐身中行为锁 ——
    [Export] public bool canAttackWhileStealthed = true;            // 隐身时可攻击（AW3=true）
    [Export] public bool canMoveWhileStealthed = true;              // 隐身时可移动
    [Export] public bool canLoadUnloadWhileStealthed = false;         // 隐身时可搭载/卸载
    [Export] public bool canCustomActionWhileStealthed = false;       // 隐身时可自爆等其他自定义动作
    [Export] public bool canCaptureWhileStealthed = false;          // 隐身时可占点
    // —— 照明弹显形 ——
    [Export] public bool stealthFlareRevealPermanent = false;      // true=永久现形；false=仅照明生效期间显形
    [Export] public bool stealthFlareRevealed = false;            // 运行时：照明弹临时显形标记
    // —— 受伤现形阈值 ——
    [Export] public bool stealthRevealOnDamageUseThreshold = false; // true=受伤后血量≤阈值才现形
    [Export] public int stealthRevealOnDamageHpThreshold = 0;       // 受伤现形血量阈值
    // —— 隐身弹系统（每单位三选一，不能通吃！）——
    [Export] public int stealthAmmoMode = 0;                      // 0=无弹免费切换 / 1=双弹种(隐身弹+显形弹) / 2=迷彩弹(通用)
    [Export] public int maxStealthBullets = 0;                    // 隐身弹上限
    [Export] public int currentStealthBullets = 0;                // 隐身弹现有
    [Export] public int maxRevealBullets = 0;                     // 显形弹上限
    [Export] public int currentRevealBullets = 0;                 // 显形弹现有
    [Export] public int maxCamoBullets = 0;                         // 迷彩弹上限
    [Export] public int currentCamoBullets = 0;                     // 迷彩弹现有
    [Export] public int stealthBulletCostPerDay = 0;              // 隐身弹日耗
    [Export] public int revealBulletCostPerDay = 0;             // 显形弹日耗
    [Export] public int camoBulletCostPerDay = 0;                   // 迷彩弹日耗
    // —— 隐身中改写（②提交） ——
    [Export] public int stealthMoveTypeOverride = -1;                 // 隐身移动方式改写(-1=不改)
    [Export] public int stealthOverlapMode = 0;
    [Export] public Godot.Collections.Dictionary<string, int> stealthMatrixOverride = new();  // 隐身时替换的攻防表（键"攻击方_目标"，空=用默认表）                       // 0=不改 1=可重叠 2=不可重叠   // 🔁 合流后再行动：true=合流完成后可继续操作一次（默认关=AW原作）
    [Export] public string[] excludedWeaponTargets = System.Array.Empty<string>();  // ⚔ 兵器攻击准入(攻击方)：不能攻击的兵器类型，"Weapon"=全部兵器，留空=均可攻击              // ✅ 合流模块：是否允许参与 Join 合流（false=该单位不可合流）
    [Export] public bool useDefaultConfig = true;           // ✅ 是否使用代码默认值（false=完全由Inspector控制，不执行ApplyUnitSpecificDefaults）
    [Export] public bool canDevour = false;                // 是否能吞噬敌人（Oozium特性）
    [Export] public bool canCapture = true;                // 是否能占领城市
    [Export] public UnitCategory unitCategory = UnitCategory.Other;  // 手动指定分类

    [Export] public int cost = 1000;  // 单位造价（可在Inspector中修改，非硬编码）

// ========== 攻防表系统 ==========

// ========== ✅ 战争迷雾视野系统 ==========
[ExportGroup("战争迷雾视野")]
[Export] public int visionRange = -1;  // -1=使用配置表默认值，>=0=覆盖配置表
[Export] public bool useConfigVision = true;  // 是否使用VisionConfig配置表

// ✅ 新增：单位独立的视野加成矩阵（覆盖全局默认值）
// 格式：地形类型 → 加成值（可正可负）
[ExportGroup("单位独立地形视野加成")]
[Export] public Godot.Collections.Dictionary<GridType, int> unitTerrainVisionBonus = new();
[Export] public bool overrideGlobalTerrainBonus = false;  // 是否覆盖全局地形加成

// ✅ 新增：P-1 单位视野共享标记（Inspector可配置）
[ExportGroup("P-1 视野共享")]
[Export] public bool isPublicVisionUnit = false;  // 是否将视野共享给所有势力（P-1单位默认true）

// 运行时实际视野（由VisionConfig或Inspector覆盖计算得出）
public int ActualVisionRange 
{
    get 
    {
        if (!useConfigVision && visionRange >= 0) return visionRange;
        return VisionConfig.GetUnitVisionRange(TemplateId);
    }
}

// ✅ 获取该单位在指定地形上的视野加成（优先使用单位专属矩阵）
public int GetTerrainVisionBonus(GridType gridType)
{
    if (overrideGlobalTerrainBonus && unitTerrainVisionBonus != null && unitTerrainVisionBonus.Count > 0)
    {
        if (unitTerrainVisionBonus.TryGetValue(gridType, out int bonus))
            return bonus;
        return 0; // 单位矩阵中未定义的地形=0加成
    }
    // 使用全局配置
    return VisionConfig.GetUnitTerrainVisionBonus(TemplateId, gridType);
}

// ✅ AI 档案（提案 docs/proposals/ai-brain.md）：全部默认=自动，AI 按单位实际能力字段推断行为；填写后覆盖推断
[ExportGroup("AI 档案")]
[Export(PropertyHint.Enum, "自动,突击,占点,守备,刺客")] public int aiRole = 0;   // 自动=按能力推断；突击=只找仗打；占点=优先占领；守备=守己方据点；刺客=打完就隐身/撤离
[Export] public Godot.Collections.Dictionary<string, float> aiTargetWeights = new(); // 目标兵种名→价值倍率（"*"=其余全部）；空=纯伤害预估。例：战斗机 {"*":0}+空军各1
[Export] public int aiRetreatHpThreshold = -1;     // 血≤N 撤退修整；-1=自动(最大血量30%)；0=死战不退
[Export] public bool aiPreferKill = true;           // 能击杀的目标额外加分
[Export] public bool aiFocusFire = true;           // 集火：残血目标额外加分
[Export] public float aiRiskMultiplier = 1f;       // 威胁规避倍率：0=无畏，1=默认，>1=更怕死

[ExportGroup("攻防表")]
[Export] public bool counterUsePreDamageHp = false;   // 反击按挨打前血量计算（旧行为）；默认 false=AW 原作按挨打后血量
[Export] public Godot.Collections.Dictionary<string, int> attackMatrix = new();      // 主武器
[Export] public Godot.Collections.Dictionary<string, int> secondaryAttackMatrix = new(); // 副武器

// 默认副武器表（通用低伤害）
private static Dictionary<string, int> defaultSecondaryMatrix = null;
    private static Dictionary<string, int> defaultMatrix = null;
public void InitializeDefaultMatrix()
{

    if (defaultMatrix == null)
    {
        defaultMatrix = new Dictionary<string, int>
        {
            ["Infantry_Infantry"] = 55,   ["Infantry_Mech"] = 45,   ["Infantry_LightTank"] = 15,
            ["Infantry_Artillery"] = 25,  ["Infantry_Rocket"] = 25, ["Infantry_APC"] = 15,
            ["Infantry_Oozium"] = 35,       ["Infantry_MdTank"] = 4,
            ["Mech_Infantry"] = 65,       ["Mech_Mech"] = 55,       ["Mech_LightTank"] = 85,
            ["Mech_Artillery"] = 55,      ["Mech_Rocket"] = 65,     ["Mech_APC"] = 65,
            ["Mech_Oozium"] = 55,       ["Mech_MdTank"] = 15,
            ["LightTank_Infantry"] = 75,  ["LightTank_Mech"] = 70,  ["LightTank_LightTank"] = 55,
            ["LightTank_Artillery"] = 45, ["LightTank_Rocket"] = 55,["LightTank_APC"] = 45,
            ["LightTank_Oozium"] = 25,      ["LightTank_MdTank"] = 17,
            ["Artillery_Infantry"] = 90,  ["Artillery_Mech"] = 85,  ["Artillery_LightTank"] = 80,
            ["Artillery_Artillery"] = 70, ["Artillery_Rocket"] = 75,["Artillery_APC"] = 70,
            ["Artillery_Oozium"] = 15,      ["Artillery_MdTank"] = 45,
            ["Rocket_Infantry"] = 95,     ["Rocket_Mech"] = 90,     ["Rocket_LightTank"] = 90,
            ["Rocket_Artillery"] = 80,    ["Rocket_Rocket"] = 85,   ["Rocket_APC"] = 80,
            ["Rocket_Oozium"] = 20,         ["Rocket_MdTank"] = 55,
            ["Oozium_Infantry"] = 999,   ["Oozium_Mech"] = 999,    ["Oozium_LightTank"] = 999,
            ["Oozium_Artillery"] = 999,   ["Oozium_Rocket"] = 999,  ["Oozium_APC"] = 999,
            ["Oozium_Oozium"] = 999,       ["Oozium_MdTank"] = 999,
            ["APC_Infantry"] = 0,         ["APC_Mech"] = 0,         ["APC_LightTank"] = 0,
            ["APC_Artillery"] = 0,        ["APC_Rocket"] = 0,       ["APC_APC"] = 0,
            ["APC_Oozium"] = 0,         ["APC_MdTank"] = 0,
            ["MdTank_Infantry"] = 110,         ["MdTank_Mech"] = 95,         ["MdTank_LightTank"] = 85,
            ["MdTank_Artillery"] = 105,        ["MdTank_Rocket"] = 105,       ["MdTank_APC"] = 105,
            ["MdTank_Oozium"] = 45,         ["MdTank_MdTank"] = 55,
            ["AntiAir_Infantry"] = 105,     ["AntiAir_Mech"] = 105,         ["AntiAir_LightTank"] = 25,         
            ["AntiAir_Artillery"] = 50,     ["AntiAir_Rocket"] = 55,        ["AntiAir_APC"] = 50,           
            ["AntiAir_Oozium"] = 15,        ["AntiAir_MdTank"] = 10,        ["AntiAir_AntiAir"] = 45,
            ["Recon_Infantry"] = 70,        ["Recon_Mech"] = 65,           ["Recon_LightTank"] = 6,
            ["Recon_Artillery"] = 45,       ["Recon_Rocket"] = 55,         ["Recon_APC"] = 45,
            ["Recon_Oozium"] = 11,          ["Recon_MdTank"] = 1,          ["Recon_AntiAir"] = 4,
            ["Recon_Recon"] = 35,
            ["Infantry_Recon"] = 12,        ["Mech_Recon"] = 85,           ["LightTank_Recon"] = 85,
            ["Artillery_Recon"] = 80,       ["Rocket_Recon"] = 90,         ["APC_Recon"] = 0,
            ["Oozium_Recon"] = 999,         ["MdTank_Recon"] = 105,        ["AntiAir_Recon"] = 60,
            // ✅ FlyBomb：无武器，攻击值为0
            ["FlyBomb_Infantry"] = 0,     ["FlyBomb_Mech"] = 0,     ["FlyBomb_LightTank"] = 0,
            ["FlyBomb_Artillery"] = 0,    ["FlyBomb_Rocket"] = 0,   ["FlyBomb_APC"] = 0,
            ["FlyBomb_Oozium"] = 0,       ["FlyBomb_MdTank"] = 0,
            ["FlyBomb_AntiAir"] = 0,      ["FlyBomb_Recon"] = 0,    ["FlyBomb_FlyBomb"] = 0,
            // 其他单位打 FlyBomb：仅AntiAir有120，其他为0
            ["Infantry_FlyBomb"] = 0,     ["Mech_FlyBomb"] = 0,     ["LightTank_FlyBomb"] = 0,
            ["Artillery_FlyBomb"] = 0,    ["Rocket_FlyBomb"] = 0,   ["APC_FlyBomb"] = 0,
            ["Oozium_FlyBomb"] = 0,       ["MdTank_FlyBomb"] = 0,   ["Recon_FlyBomb"] = 0,
            ["AntiAir_FlyBomb"] = 120,
            // ✅ AntiTank：反坦克炮主武器数据
            ["AntiTank_Infantry"] = 75,   ["AntiTank_Mech"] = 65,    ["AntiTank_Recon"] = 75,
            ["AntiTank_AntiAir"] = 75,    ["AntiTank_LightTank"] = 75, ["AntiTank_MdTank"] = 65,
            ["AntiTank_Artillery"] = 65,  ["AntiTank_AntiTank"] = 55,  ["AntiTank_Rocket"] = 70,
            ["AntiTank_APC"] = 65,      ["AntiTank_FlyBomb"] = 0,
            // 其他单位打 AntiTank（主武器）
            ["Infantry_AntiTank"] = 0,    ["Mech_AntiTank"] = 55,    ["Recon_AntiTank"] = 0,
            ["AntiAir_AntiTank"] = 25,    ["LightTank_AntiTank"] = 30, ["MdTank_AntiTank"] = 35,
            ["Artillery_AntiTank"] = 55,  ["AntiTank_AntiTank"] = 75,  ["Rocket_AntiTank"] = 65,
            ["APC_AntiTank"] = 0,       ["FlyBomb_AntiTank"] = 0,
            // ✅ Flare / Bike 主武器数据
            ["Mech_Flare"] = 80,          ["Mech_Bike"] = 55,
            ["LightTank_Flare"] = 80,     ["LightTank_Bike"] = 70,
            ["MdTank_Flare"] = 90,        ["MdTank_Bike"] = 80,
            ["Artillery_Flare"] = 75,     ["Artillery_Bike"] = 85,
            ["Rocket_Flare"] = 85,        ["Rocket_Bike"] = 90,
            ["Oozium_Flare"] = 999,       ["Oozium_Bike"] = 999,
            ["AntiAir_Flare"] = 50,       ["AntiAir_Bike"] = 105,
            ["AntiTank_Flare"] = 75,      ["AntiTank_Bike"] = 65,
            ["PipeRunner_Infantry"] = 95,   ["PipeRunner_Mech"] = 90,
["PipeRunner_LightTank"] = 80,  ["PipeRunner_MdTank"] = 55,
["PipeRunner_Rocket"] = 85,     ["PipeRunner_Recon"] = 90,
["PipeRunner_APC"] = 80,        ["PipeRunner_AntiAir"] = 85,
["PipeRunner_Artillery"] = 80,  ["PipeRunner_FlyBomb"] = 120,
            ["Fighter_FlyBomb"] = 100,
            ["Fighter_Fighter"] = 55,
            ["AntiAir_Fighter"] = 65,
            ["PipeRunner_Fighter"] = 120,
            // 轰炸机：对地/对海毁灭打击（AW官方值），对空完全无助（无对空行=不能攻击）
            ["Bomber_Infantry"] = 110,
            ["Bomber_Mech"] = 110,
            ["Bomber_Bike"] = 105,
            ["Bomber_Oozium"] = 35,
            ["Bomber_Recon"] = 105,
            ["Bomber_APC"] = 105,
            ["Bomber_LightTank"] = 105,
            ["Bomber_MdTank"] = 95,
            ["Bomber_Artillery"] = 105,
            ["Bomber_Rocket"] = 105,
            ["Bomber_AntiAir"] = 95,
            ["Bomber_AntiTank"] = 105,
            ["Bomber_PipeRunner"] = 105,
            ["Bomber_Flare"] = 105,
            ["Bomber_RepairBoat"] = 75,
            // 轰炸机挨揍表
            ["Fighter_Bomber"] = 100,
            ["AntiAir_Bomber"] = 75,
            // 武装直升机主武器(导弹6发)：对车辆/兵器，对空无一格=打不了飞机
            ["BCopter_Recon"] = 55,      ["BCopter_APC"] = 60,
            ["BCopter_LightTank"] = 55,  ["BCopter_MdTank"] = 25,
            ["BCopter_Artillery"] = 65,  ["BCopter_Rocket"] = 65,
            ["BCopter_AntiAir"] = 25,    ["BCopter_AntiTank"] = 55,
            ["BCopter_PipeRunner"] = 55, ["BCopter_Flare"] = 55,
            ["BCopter_Bike"] = 55,       ["BCopter_Oozium"] = 25,
            ["BCopter_RepairBoat"] = 25,
            // 武装直升机副武器(机枪∞)：对步兵/直升机
            // 挨揍表：直升机系
            ["AntiAir_BCopter"] = 120,   ["Fighter_BCopter"] = 100,   ["PipeRunner_BCopter"] = 120,
            ["AntiAir_TCopter"] = 120, ["Fighter_TCopter"] = 100,   ["PipeRunner_TCopter"] = 120,
            // 防空导弹车(Anti-air Missile)：只对空，射程3-5（AW官方值）
            ["AntiAirMissile_Fighter"] = 100,  ["AntiAirMissile_Bomber"] = 100,
            ["AntiAirMissile_BCopter"] = 100,  ["AntiAirMissile_TCopter"] = 100,
            ["AntiAirMissile_FlyBomb"] = 120,
            // 防空导弹车挨揍表（AWDS矩阵Missiles列）：轻坦85 中坦105 轰105 武直65 步25 甲85
            ["Infantry_AntiAirMissile"] = 25,  ["Mech_AntiAirMissile"] = 85,
            ["Recon_AntiAirMissile"] = 28,     ["LightTank_AntiAirMissile"] = 85,
            ["MdTank_AntiAirMissile"] = 105,   ["Artillery_AntiAirMissile"] = 80,
            ["Rocket_AntiAirMissile"] = 90,    ["AntiAir_AntiAirMissile"] = 55,
            ["AntiTank_AntiAirMissile"] = 85,  ["PipeRunner_AntiAirMissile"] = 90,
            ["Flare_AntiAirMissile"] = 28,     ["Bike_AntiAirMissile"] = 10,
            ["Bomber_AntiAirMissile"] = 105,   ["BCopter_AntiAirMissile"] = 65,
            // 隐身战机(AWDS全量对空对地)：全能打，唯一例外水下潜艇
            ["Stealth_Infantry"] = 95,  ["Stealth_Mech"] = 90,    ["Stealth_Bike"] = 90,
            ["Stealth_Oozium"] = 15,    ["Stealth_Recon"] = 90,   ["Stealth_APC"] = 80,
            ["Stealth_LightTank"] = 80, ["Stealth_MdTank"] = 55,  ["Stealth_Artillery"] = 80,
            ["Stealth_Rocket"] = 85,    ["Stealth_AntiAir"] = 85, ["Stealth_AntiTank"] = 80,
            ["Stealth_PipeRunner"] = 80,["Stealth_Flare"] = 80,   ["Stealth_RepairBoat"] = 60,
            ["Stealth_Fighter"] = 65,   ["Stealth_Bomber"] = 75,  ["Stealth_BCopter"] = 105,
            ["Stealth_TCopter"] = 105,  ["Stealth_Stealth"] = 75, ["Stealth_FlyBomb"] = 120,
            // 隐身战机挨揍表（AWDS）：战斗机85 防空车75 导弹车100 管道炮75（打它必须先破隐）
            ["Fighter_Stealth"] = 85,   ["AntiAir_Stealth"] = 75,
            ["AntiAirMissile_Stealth"] = 100, ["PipeRunner_Stealth"] = 75,
["PipeRunner_PipeRunner"] = 80,["PipeRunner_Flare"] = 75,
["PipeRunner_AntiTank"] = 60,   ["PipeRunner_Bike"] = 90,
// ✅ 其他单位主武器攻击 PipeRunner
["AntiAir_PipeRunner"] = 85,    ["Artillery_PipeRunner"] = 70,
["MdTank_PipeRunner"] = 85,    ["Mech_PipeRunner"] = 55,
["PipeRunner_PipeRunner"] = 80,["Rocket_PipeRunner"] = 80,
["LightTank_PipeRunner"] = 55, ["AntiTank_PipeRunner"] = 85,
["Flare_PipeRunner"] = 12,      ["Bike_PipeRunner"] = 4,
            // ✅ RepairBoat：无武器，攻击值为0
            ["RepairBoat_Infantry"] = 0,  ["RepairBoat_Mech"] = 0,  ["RepairBoat_LightTank"] = 0,
            ["RepairBoat_Artillery"] = 0, ["RepairBoat_Rocket"] = 0,["RepairBoat_APC"] = 0,
            ["RepairBoat_Oozium"] = 0,    ["RepairBoat_MdTank"] = 0,["RepairBoat_FlyBomb"] = 0,
            ["RepairBoat_AntiAir"] = 0,   ["RepairBoat_Recon"] = 0, ["RepairBoat_Flare"] = 0,
            ["RepairBoat_Bike"] = 0,      ["RepairBoat_PipeRunner"] = 0,["RepairBoat_RepairBoat"] = 0,
            // 其他单位打 RepairBoat
            ["MdTank_RepairBoat"] = 35,   ["PipeRunner_RepairBoat"] = 60,
            ["Rocket_RepairBoat"] = 60,   ["LightTank_RepairBoat"] = 10,
            ["Artillery_RepairBoat"] = 55,["Infantry_RepairBoat"] = 0,
            ["Mech_RepairBoat"] = 0,      ["AntiAir_RepairBoat"] = 0,
            ["Recon_RepairBoat"] = 0,     ["Flare_RepairBoat"] = 0,
            ["Bike_RepairBoat"] = 0,      ["AntiTank_RepairBoat"] = 0,
            ["APC_RepairBoat"] = 0,       ["FlyBomb_RepairBoat"] = 0,
        };
    }

    if (defaultSecondaryMatrix == null)
    {
        defaultSecondaryMatrix = new Dictionary<string, int>
        {
            ["BCopter_Infantry"] = 75,  ["BCopter_Mech"] = 75,
            ["BCopter_BCopter"] = 65,   ["BCopter_TCopter"] = 95,
            // 步兵副武器（机枪）——通用但低
            ["Infantry_Infantry"] = 45,   ["Infantry_Mech"] = 35,   ["Infantry_LightTank"] = 5,
            ["Infantry_Artillery"] = 15,  ["Infantry_Rocket"] = 15, ["Infantry_APC"] = 5,
            ["Infantry_Oozium"] = 30,       ["Infantry_MdTank"] = 0,
            
            // 机甲副武器（机枪）——通用
            ["Mech_Infantry"] = 55,       ["Mech_Mech"] = 45,       ["Mech_LightTank"] = 15,
            ["Mech_Artillery"] = 25,      ["Mech_Rocket"] = 35,     ["Mech_APC"] = 25,
            ["Mech_Oozium"] = 25,       ["Mech_MdTank"] = 10,
            
            // 坦克副武器（同轴机枪）——只能打步兵
            ["LightTank_Infantry"] = 35,  ["LightTank_Mech"] = 25,  ["LightTank_LightTank"] = 5,
            ["LightTank_Artillery"] = 5,  ["LightTank_Rocket"] = 5,   ["LightTank_APC"] = 5,
            ["LightTank_Oozium"] = 10,      ["APC_MdTank"] = 2,
            ["Infantry_AntiAir"] = 5,     ["Infantry_PipeRunner"] = 5,
            ["Mech_AntiAir"] = 55,       ["Mech_PipeRunner"] = 6,
            ["LightTank_MdTank"] = 2,    ["LightTank_AntiAir"] = 5,  ["LightTank_PipeRunner"] = 6,
            ["MdTank_AntiAir"] = 1,      ["MdTank_PipeRunner"] = 8,
            ["Artillery_AntiAir"] = 0,   ["Artillery_PipeRunner"] = 0,
            ["Rocket_AntiAir"] = 0,      ["Rocket_PipeRunner"] = 0,
            ["APC_AntiAir"] = 0,         ["APC_PipeRunner"] = 0,
            ["Oozium_AntiAir"] = 0,      ["Oozium_AntiTank"] = 0,    ["Oozium_PipeRunner"] = 0,
            ["Recon_PipeRunner"] = 30,
            ["AntiAir_PipeRunner"] = 15,
            ["AntiTank_Oozium"] = 0,     ["AntiTank_PipeRunner"] = 0,
            ["Flare_PipeRunner"] = 25,
            ["Bike_PipeRunner"] = 4,
            ["FlyBomb_PipeRunner"] = 0,
            ["PipeRunner_Infantry"] = 0, ["PipeRunner_Mech"] = 0,     ["PipeRunner_LightTank"] = 0,["PipeRunner_MdTank"] = 0,
            ["PipeRunner_Artillery"] = 0,["PipeRunner_Rocket"] = 0,   ["PipeRunner_APC"] = 0,   ["PipeRunner_Oozium"] = 0,
            ["PipeRunner_Recon"] = 0,    ["PipeRunner_AntiAir"] = 0, ["PipeRunner_AntiTank"] = 0,["PipeRunner_Flare"] = 0,
            ["PipeRunner_Bike"] = 0,     ["PipeRunner_FlyBomb"] = 0, ["PipeRunner_PipeRunner"] = 0,

            
            // 火炮/火箭——无副武器（或极低自卫）
            ["Artillery_Infantry"] = 0,  ["Artillery_Mech"] = 0,  ["Artillery_LightTank"] = 0,
            ["Artillery_Artillery"] = 0,  ["Artillery_Rocket"] = 0,   ["Artillery_APC"] = 0,
            ["Artillery_Oozium"] = 0,       ["Artillery_MdTank"] = 0,
            
            ["Rocket_Infantry"] = 0,     ["Rocket_Mech"] = 0,     ["Rocket_LightTank"] = 0,
            ["Rocket_Artillery"] = 0,     ["Rocket_Rocket"] = 0,    ["Rocket_APC"] = 0,
            ["Rocket_Oozium"] = 0,      ["Rocket_MdTank"] = 0,
            
            // Oozium——无副武器
            ["Oozium_Infantry"] = 0,      ["Oozium_Mech"] = 0,      ["Oozium_LightTank"] = 0,
            ["Oozium_Artillery"] = 0,     ["Oozium_Rocket"] = 0,    ["Oozium_APC"] = 0,
            ["Oozium_Oozium"] = 0,      ["Oozium_MdTank"] = 0,
            
            // APC——无
            ["APC_Infantry"] = 0,         ["APC_Mech"] = 0,         ["APC_LightTank"] = 0,
            ["APC_Artillery"] = 0,        ["APC_Rocket"] = 0,       ["APC_APC"] = 0,
            ["APC_Oozium"] = 0,           ["APC_MdTank"] = 0,
            // ✅ RepairBoat 副武器数据（无副武器）
            ["RepairBoat_Infantry"] = 0,  ["RepairBoat_Mech"] = 0,  ["RepairBoat_LightTank"] = 0,
            ["RepairBoat_Artillery"] = 0, ["RepairBoat_Rocket"] = 0,["RepairBoat_APC"] = 0,
            ["RepairBoat_Oozium"] = 0,    ["RepairBoat_MdTank"] = 0,["RepairBoat_FlyBomb"] = 0,
            ["RepairBoat_AntiAir"] = 0,   ["RepairBoat_Recon"] = 0, ["RepairBoat_Flare"] = 0,
            ["RepairBoat_Bike"] = 0,      ["RepairBoat_PipeRunner"] = 0,["RepairBoat_RepairBoat"] = 0,
            // 其他单位副武器打 RepairBoat
            ["MdTank_RepairBoat"] = 10,   ["PipeRunner_RepairBoat"] = 0,
            ["Rocket_RepairBoat"] = 0,    ["LightTank_RepairBoat"] = 1,
            ["Artillery_RepairBoat"] = 0, ["Infantry_RepairBoat"] = 0,
            ["Mech_RepairBoat"] = 0,      ["AntiAir_RepairBoat"] = 0,
            ["Recon_RepairBoat"] = 0,     ["Flare_RepairBoat"] = 0,
            ["Bike_RepairBoat"] = 0,      ["AntiTank_RepairBoat"] = 0,
            ["APC_RepairBoat"] = 0,       ["FlyBomb_RepairBoat"] = 0,

            ["MdTank_Infantry"] = 55,         ["MdTank_Mech"] = 45,         ["MdTank_LightTank"] = 45,
            ["MdTank_Artillery"] = 55,        ["MdTank_Rocket"] = 45,       ["MdTank_APC"] = 55,
            ["MdTank_Oozium"] = 35,         ["MdTank_MdTank"] = 25,

            ["AntiAir_Infantry"] = 55,     ["AntiAir_Mech"] = 54,         ["AntiAir_LightTank"] = 5,         
            ["AntiAir_Artillery"] = 20,     ["AntiAir_Rocket"] = 25,        ["AntiAir_APC"] = 20,           
            ["AntiAir_Oozium"] = 10,        ["AntiAir_MdTank"] = 1,        ["AntiAir_AntiAir"] = 15,

            ["Recon_Infantry"] = 70,        ["Recon_Mech"] = 65,           ["Recon_LightTank"] = 6,
            ["Recon_Artillery"] = 45,       ["Recon_Rocket"] = 55,         ["Recon_APC"] = 45,
            ["Recon_Oozium"] = 11,          ["Recon_MdTank"] = 1,          ["Recon_AntiAir"] = 4,
            ["Recon_Recon"] = 35,

            ["Infantry_Recon"] = 12,        ["Mech_Recon"] = 18,           ["LightTank_Recon"] = 40,
            ["Artillery_Recon"] = 80,       ["Rocket_Recon"] = 90,         ["APC_Recon"] = 0,
            ["Oozium_Recon"] = 0,           ["MdTank_Recon"] = 45,         ["AntiAir_Recon"] = 60,
            // ✅ FlyBomb：无副武器
            ["FlyBomb_Infantry"] = 0,     ["FlyBomb_Mech"] = 0,     ["FlyBomb_LightTank"] = 0,
            ["FlyBomb_Artillery"] = 0,    ["FlyBomb_Rocket"] = 0,   ["FlyBomb_APC"] = 0,
            ["FlyBomb_Oozium"] = 0,       ["FlyBomb_MdTank"] = 0,
            ["FlyBomb_AntiAir"] = 0,      ["FlyBomb_Recon"] = 0,    ["FlyBomb_FlyBomb"] = 0,
            // 其他单位副武器打 FlyBomb：均为0
            ["Infantry_FlyBomb"] = 0,     ["Mech_FlyBomb"] = 0,     ["LightTank_FlyBomb"] = 0,
            ["Artillery_FlyBomb"] = 0,    ["Rocket_FlyBomb"] = 0,   ["APC_FlyBomb"] = 0,
            ["Oozium_FlyBomb"] = 0,       ["MdTank_FlyBomb"] = 0,   ["Recon_FlyBomb"] = 0,
            ["AntiAir_FlyBomb"] = 0,
            // ✅ AntiTank：反坦克炮无副武器 / 其他单位副武器打AntiTank
            ["AntiTank_Infantry"] = 0,  ["AntiTank_Mech"] = 0,   ["AntiTank_Recon"] = 0,
            ["AntiTank_AntiAir"] = 0,   ["AntiTank_LightTank"] = 0, ["AntiTank_MdTank"] = 0,
            ["AntiTank_Artillery"] = 0, ["AntiTank_AntiTank"] = 0,  ["AntiTank_Rocket"] = 0,
            ["AntiTank_APC"] = 0,      ["AntiTank_FlyBomb"] = 0,
            // 其他单位副武器打 AntiTank
            ["Infantry_AntiTank"] = 30, ["Mech_AntiTank"] = 30,  ["Recon_AntiTank"] = 25,
            ["AntiAir_AntiTank"] = 0,   ["LightTank_AntiTank"] = 1, ["MdTank_AntiTank"] = 1,
            ["Artillery_AntiTank"] = 0, ["AntiTank_AntiTank"] = 0,  ["Rocket_AntiTank"] = 0,
            ["APC_AntiTank"] = 0,      ["FlyBomb_AntiTank"] = 0,
            // ✅ Flare / Bike 副武器数据
            // Flare 副武器（机枪）
            ["Flare_Infantry"] = 80,      ["Flare_Mech"] = 70,          ["Flare_Bike"] = 70,
            ["Flare_Recon"] = 60,         ["Flare_Flare"] = 50,         ["Flare_AntiAir"] = 45,
            ["Flare_LightTank"] = 10,     ["Flare_MdTank"] = 5,         ["Flare_Artillery"] = 45,
            ["Flare_AntiTank"] = 25,      ["Flare_Rocket"] = 55,        ["Flare_APC"] = 45,
            ["Flare_Oozium"] = 20,        ["Flare_FlyBomb"] = 0,
            // Bike 副武器（机枪）
            ["Bike_Infantry"] = 65,       ["Bike_Mech"] = 55,           ["Bike_Bike"] = 55,
            ["Bike_Recon"] = 35,          ["Bike_Flare"] = 15,          ["Bike_AntiAir"] = 35,
            ["Bike_LightTank"] = 1,       ["Bike_MdTank"] = 1,          ["Bike_Artillery"] = 15,
            ["Bike_AntiTank"] = 35,       ["Bike_Rocket"] = 20,         ["Bike_APC"] = 15,
            ["Bike_Oozium"] = 35,         ["Bike_FlyBomb"] = 0,
            // 其他单位副武器攻击 Flare
            ["Infantry_Flare"] = 10,      ["Mech_Flare"] = 15,          ["Recon_Flare"] = 30,
            ["AntiAir_Flare"] = 0,        ["LightTank_Flare"] = 35,     ["MdTank_Flare"] = 35,
            ["Artillery_Flare"] = 0,      ["Rocket_Flare"] = 0,         ["APC_Flare"] = 0,
            ["Oozium_Flare"] = 0,         ["AntiTank_Flare"] = 0,       ["FlyBomb_Flare"] = 0,
            // 其他单位副武器攻击 Bike
            ["Infantry_Bike"] = 45,       ["Mech_Bike"] = 55,           ["Recon_Bike"] = 30,
            ["AntiAir_Bike"] = 55,        ["LightTank_Bike"] = 25,      ["MdTank_Bike"] = 45,
            ["Artillery_Bike"] = 0,       ["Rocket_Bike"] = 0,          ["APC_Bike"] = 0,
            ["Oozium_Bike"] = 0,          ["AntiTank_Bike"] = 0,        ["FlyBomb_Bike"] = 0,
            // ✅ 副武器攻击 PipeRunner
            ["Mech_PipeRunner"] = 6,        ["MdTank_PipeRunner"] = 8,
            ["LightTank_PipeRunner"] = 6,
        };
    }
}
public virtual void OnTurnEnd()
{
    originalGrid = null;
    state = UnitState.Idle;
    isMoved = false;
    isAttacked = false;
    movedThisTurnForAutoRepair = false; autoRepairAfterMoveDone = false;
    hasActed = false;   // ✅ 修复：此前从不重置，生产/合流/补给/修理过的单位永久失去补给/修理/自爆等按钮
    movePoints = defaultMovePoints;
    SetWaitVisual(false);
    // ✅ 恢复 idle 动画后再启动呼吸（animSprite 单位需要）
    string animName = GetIdleAnimName();
    ShowAnimState(animName);
    StartBreath();
}
// 查主武器表
public virtual int GetPrimaryDamageFromMatrix(Infantry target) => GetMatrixDamage(target, false);

/// <summary>
/// ✅ 统一查表入口：隐身专属表（仅隐身中）→ 实例表 → 默认表。
/// stealthMatrixOverride 此前只写在从未被调用的 GetBaseDamageFromMatrix 里，实际攻击/反击都不生效。
/// assumeStealthed：AI 估算用，假设"此刻是否隐身"（null=按当前状态）。
/// </summary>
public int GetMatrixDamage(Infantry target, bool secondary, bool? assumeStealthed = null)
{
    if (target == null) return 0;
    // 键优先级：本ID_目标ID → 参照_目标ID → 本ID_目标参照 → 参照_目标参照（原有兵种四个键相同）
    var keys = MatrixKeys(UnitTypeId, TemplateId, target.UnitTypeId, target.TemplateId);
    // 隐身攻防表替换：隐身时同一张表同时替换主/副武器（填 0 = 隐身状态下打不了该目标）
    if ((assumeStealthed ?? isStealthed) && stealthMatrixOverride != null && stealthMatrixOverride.Count > 0)
        foreach (var k in keys) if (stealthMatrixOverride.TryGetValue(k, out int sval)) return sval;
    var inst = secondary ? secondaryAttackMatrix : attackMatrix;
    var def = secondary ? defaultSecondaryMatrix : defaultMatrix;
    if (inst != null) foreach (var k in keys) if (inst.TryGetValue(k, out int v)) return v;
    if (def != null) foreach (var k in keys) if (def.TryGetValue(k, out int v)) return v;
    return 0;
}

private static List<string> MatrixKeys(string att, string attTpl, string def, string defTpl)
{
    var r = new List<string>(4);
    foreach (var k in new[] { $"{att}_{def}", $"{attTpl}_{def}", $"{att}_{defTpl}", $"{attTpl}_{defTpl}" })
        if (!r.Contains(k)) r.Add(k);
    return r;
}

/// <summary>AI 生产评估：按兵种ID+参照兵种查默认攻防表</summary>
public static int LookupDefaultMatrix(string att, string attTpl, string def, string defTpl, bool secondary = false)
{
    var m = secondary ? defaultSecondaryMatrix : defaultMatrix;
    if (m == null) return 0;
    foreach (var k in MatrixKeys(att, attTpl ?? att, def, defTpl ?? def)) if (m.TryGetValue(k, out int v)) return v;
    return 0;
}

// ✅ AI 生产评估用：按类型名查默认攻防表（无实例时使用）
public static int LookupDefaultMatrix(string attackerType, string defenderType, bool secondary = false)
{
    var m = secondary ? defaultSecondaryMatrix : defaultMatrix;
    if (m == null) return 0;
    return m.TryGetValue($"{attackerType}_{defenderType}", out int v) ? v : 0;
}

// 查副武器表
public virtual int GetSecondaryDamageFromMatrix(Infantry target) => GetMatrixDamage(target, true);

public virtual WeaponType SelectWeaponByMatrix(Infantry target)
{
    // 有主武器弹药 → 优先主武器
    if (hasPrimaryWeapon && CanUsePrimaryWeapon())
    {
        int primaryDamage = GetPrimaryDamageFromMatrix(target);
        if (primaryDamage > 0) return WeaponType.Primary;
    }
    
    // 没弹药或主武器打不了 → 副武器
    if (hasSecondaryWeapon)
    {
        int secondaryDamage = GetSecondaryDamageFromMatrix(target);
        if (secondaryDamage > 0) return WeaponType.Secondary;
    }
    
    return WeaponType.None;
}




// 防御力（动态衰减：baseDefense × 血量% + 地形加成）
public int GetEffectiveDefense(Grids targetGrid)
{
    float healthPercent = (float)health / maxHealth;
    int dynamicDefense = Mathf.RoundToInt(baseDefense * healthPercent);
    float terrainBonus = (defenseBonusType == DefenseBonusType.CanDefenseBonus && targetGrid != null) 
        ? targetGrid.TerrainDefenseBonus 
        : 0f;
    return Mathf.RoundToInt(dynamicDefense * (1.0f + terrainBonus));
}

public virtual int CalculateFinalDamage(Infantry target, WeaponType weaponType, bool isCounter = false)
{
    int baseDamage = 0;
    
    if (weaponType == WeaponType.Primary)
        baseDamage = GetPrimaryDamageFromMatrix(target);
    else if (weaponType == WeaponType.Secondary)
        baseDamage = GetSecondaryDamageFromMatrix(target);
    
    if (baseDamage <= 0) return 0;
    
    // 攻击方血量系数
    float attackerHealthPercent = (float)health / maxHealth;
    float actualAttack = baseDamage * attackerHealthPercent;
    
    // 地形
    float terrainDefense = (target.defenseBonusType == DefenseBonusType.CanDefenseBonus && target.grid != null)
        ? target.grid.TerrainDefenseBonus
        : 0f;
    float terrainMultiplier = 1.0f - terrainDefense;
    float afterTerrain = actualAttack * terrainMultiplier;
    
    // 防御阈值
    int effectiveDefense = target.GetEffectiveDefense(target.grid);
    float rawDamage = afterTerrain - effectiveDefense;
    
    if (rawDamage <= 0)
    {
        return 0;
    }
    
    // 反击乘系数
    if (isCounter)
        rawDamage *= counterMul;
    
    return Mathf.Max(1, Mathf.RoundToInt(rawDamage));
}

// 反击
public virtual int CalculateCounterDamage(Infantry attacker)
{
    WeaponType weapon = SelectWeaponByMatrix(attacker);
    return CalculateFinalDamage(attacker, weapon, isCounter: true);
}

// 添加到 Infantry.cs 的字段区域（大约在 line 30-40 附近，[ExportGroup("究极自由：单位类型特性")] 之后）

[ExportGroup("单位特性判定")]

[Export] public bool isArmoredUnit = false;           // 是否是装甲单位（影响武器选择）
[Export] public bool IsAirUnit = false;           // 是否为空军单位（影响地形加成判定，不影响移动方式）

public enum DefenseBonusType { NoDefenseBonus, CanDefenseBonus }
[Export] public DefenseBonusType defenseBonusType = DefenseBonusType.CanDefenseBonus;  // 能否享有地形防御加成

[ExportGroup("搭载系统")]
[Export] public bool canTransportUnits = false;
[Export] public bool canSupplyUnits = false;  // 能否补给其他单位（独立于搭载系统）

[Export] public int maxTransportCapacity = 0;
[Export] public Godot.Collections.Array<string> canTransportUnitTypes = new();
[Export] public int maxLoadCapacity { get => maxTransportCapacity; set => maxTransportCapacity = value; }
[Export] public int minSupplyRange = 1;
[Export] public int maxSupplyRange = 1;
[Export] public bool canSupplyAfterMove = true;

// ✅ 新增：按单位类别过滤（在Inspector中勾选可搭载的类别）
[Export] public Godot.Collections.Array<UnitCategory> canTransportUnitCategories = new();

// ✅ 新增：按属性过滤（勾选即可生效，零代码适配新单位）
[Export] public bool transportExcludeAirUnits = false;      // 排除空军单位
[Export] public bool transportExcludeArmored = false;      // 排除装甲单位
[Export] public bool transportRequireCanCapture = false;   // 只能运输可占领单位
[Export] public int transportMaxMovePoints = 0;            // 最大移动力限制（0=无限制）

[Export] public AnimatedSprite2D loadedIcon;
[Export] public AnimatedSprite2D noFuelIcon;

// 状态
public bool isUnloading = false;
public bool hasActed = false;

// 卸下后不能移动的单位记录（静态，所有运输单位共享）
public static Dictionary<Infantry, int> unitsCannotMove = new Dictionary<Infantry, int>();

// 私有
private Tween loadedIconTween;


// ========== 新增：核心伤害公式 ==========




public List<Infantry> transportedUnits = new List<Infantry>();
	[Export]
	public bool isTransported = false;
    public Tween breathTween;

    [Export]
    public AttackOverlapPolicy attackOverlap = AttackOverlapPolicy.AllowAttackEnemyOnSameTile;
    [Export]
    public UnitOverlapType overlapType = UnitOverlapType.NonOverlapping;
    [Export]
    public MoveType moveType = MoveType.Infantry;  
    [Export]
    public UnitState state = UnitState.Idle;
    public ActionMenu actionMenu;
    public Grids grid;
    [Export]
    public GridManager gridManager;

    [Export]
    public AttackType attackType = AttackType.CanAttack;
    public Action<Infantry> OnClickPiece;

    [Export]
    public int attackRange = 1;
    [Export] public int defaultMovePoints = 3;  // 默认移动力，Inspector可改
    [Export] public int movePoints = 3;  // 当前剩余移动力
    [Export]
    public int maxHealth = 100;
    [Export]
    public int health = 100;
    [Export]
    public int baseAttack = 100;
    [Export]
    public int baseDefense = 0;
    [Export] public int minAttackRange = 1;        // 最小攻击范围（盲区）
    [Export] public int maxAttackRange = 1;        // 最大攻击范围
    [Export] public bool useMinMaxAttackRange = false;  // 是否使用环形攻击范围
    [Export] public bool canAttackAfterMoving = true;   // 移动后能否攻击

    [Export] public AnimatedSprite2D noAmmoIcon;
    // ✅ 新增：通用弹药系统（所有单位可用）




    [Export] public int maxFuel = 99;
    [Export] public int fuel = 99;
    [Export] public bool consumeFuel = false;
    [Export] public int lowFuelThreshold = 15;               // 低燃料阈值，低于此值闪烁 NoFuelIcon
    [Export] public int dailyFuelConsumption = 0;          // 每回合开始自动消耗的燃料
    [Export] public bool destroyOnOutOfFuel = false;       // 燃料耗尽后是否自毁
    [ExportGroup("主武器配置")]
    [Export] public bool useDefaultAmmoConfig = true;
    [Export] public bool hasPrimaryWeapon = false;           // 是否有主武器
    [Export] public bool primaryHasLimitedAmmo = true;       // 主武器是否有限弹药（false=无限）
    [Export] public int maxPrimaryAmmo = 0;                  // 最大弹药（0=无武器，>0=有限，>=99=无限）
    [Export] public int currentPrimaryAmmo = 0;              // 当前弹药

// ========== 副武器配置 ==========
    [ExportGroup("副武器配置")]
    [Export] public bool hasSecondaryWeapon = true;          // 是否有副武器
    [Export] public int secondaryAttack = 100;               // 副武器攻击力

// ========== 武器效果配置 ==========
    [ExportGroup("武器效果")]
[Export] public bool primaryAntiArmor = false;           // 主武器对装甲有效
[Export] public bool primaryAntiInfantry = true;         // 主武器对步兵有效
[Export] public bool secondaryAntiArmor = false;         // 副武器对装甲有效
[Export] public bool secondaryAntiInfantry = true;       // 副武器对步兵有效

[ExportGroup("远程搭载")]
[Export] public bool CanFarMounting = false;
[Export] public int minFarMountingDistance = 0;    // 远程搭载最小距离（0=无限制，可贴身上船）
[Export] public int maxFarMountingDistance = 1;    // 远程搭载最大距离（原 FarMountingDistance） 
public virtual bool HasPrimaryWeapon => hasPrimaryWeapon;
public virtual bool HasSecondaryWeapon => hasSecondaryWeapon;
public virtual bool HasLimitedPrimaryAmmo => hasPrimaryWeapon && primaryHasLimitedAmmo && maxPrimaryAmmo < 99;
public virtual bool HasInfinitePrimaryAmmo => hasPrimaryWeapon && (!primaryHasLimitedAmmo || maxPrimaryAmmo >= 99);

public virtual string GetPrimaryAmmoString()
{
    if (!hasPrimaryWeapon) return TranslateGroup.T("无");
    if (HasInfinitePrimaryAmmo) return "∞";
    return $"{currentPrimaryAmmo}/{maxPrimaryAmmo}";
}
public virtual bool CanUsePrimaryWeapon()
{
    if (!hasPrimaryWeapon) return false;
    if (HasInfinitePrimaryAmmo) return true;
    return currentPrimaryAmmo > 0;
}
public virtual bool CanUseSecondaryWeapon()
{
    return hasSecondaryWeapon;
}
// 修改：泛化获取需要弹药的兵器
public virtual List<Weapon> GetWeaponsNeedingAmmo()
{
    var result = new List<Weapon>();
    var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
    if (gm?.weaponManager == null) return result;

    var supplyRange = CalculateSupplyRange();

    foreach (var weapon in gm.weaponManager.AllWeapons)
    {
        if (!IsInstanceValid(weapon)) continue;
        
        // ✅ 模块化：通过 Weapon 基类属性判断，零硬编码适配新武器
        if (weapon.useAmmoSystem && weapon.currentAmmo < weapon.maxAmmo)
        {
            if (weapon.grid != null && supplyRange.Contains(weapon.grid))
                result.Add(weapon);
        }
    }
    return result;
}

public virtual bool ResupplyWeapon(Weapon weapon)
{
    if (weapon == null || !IsInstanceValid(weapon)) return false;

    // ✅ 模块化：直接调用 Weapon 基类的虚方法，零硬编码适配新武器
    bool success = weapon.ResupplyAmmo();
    
    if (success)
    {
    }
    
    return success;
}

public virtual bool ConsumePrimaryAmmo()
{
    if (!hasPrimaryWeapon) return false;
    if (HasInfinitePrimaryAmmo) return true;
    if (currentPrimaryAmmo <= 0) return false;
    currentPrimaryAmmo--;
    return true;
}

    [Export]
    public CaptureAbility captureAbility = CaptureAbility.CannotCapture;  // 默认不能占领
    
    [Export]
    public int capturePower = 10;  // 占领力，默认10（两回合）

    [Export]
    public bool allowCaptureAfterMove = true;  // 移动后是否允许占领（AW标准=true）

    // ========== ✅ 通用自爆系统（任何单位勾选即可自爆）==========
    [ExportGroup("自爆系统")]
    [Export] public bool canExplode = false;                     // 是否可自爆
    [Export] public int explosionMinRange = 0;                   // 爆炸最小范围（盲区）
    [Export] public int explosionMaxRange = 1;                   // 爆炸最大范围
    [Export] public int explosionDamageMode = 0;                 // 0=固定值 1=百分比 2=攻击公式
    [Export] public int explosionFixedValue = 5;                 // 固定伤害值（可为负=加血）
    [Export] public float explosionPercentValue = 0.5f;          // 百分比伤害（0.5=50%）
    [Export] public int explosionTargetMode = 0;                  // 0=所有 1=仅敌方 2=仅友方
    [Export] public bool explosionDestroysSelf = true;           // 自爆是否摧毁自身
    [Export] public bool explosionSelfDamageEnabled = true;      // 自爆是否对自身造成伤害（内伤开关）
    [Export] public bool explosionCanKill = false;               // 是否能击杀其他单位（否则最低留1HP）
    [Export] public bool explosionAffectsWeapons = false;        // 爆炸是否影响兵器
    [Export] public bool explosionCanExceedMaxHealth = false;    // 回血/加血时能否超过最大HP

    // ========== ✅ 通用照明模块（任何单位勾选即可发射照明弹）==========
    [ExportGroup("照明模块")]
    [Export] public bool canIlluminate = false;                     // 是否可以发射照明弹
    [Export] public int maxFlareAmmo = 0;                            // 最大照明弹数
    [Export] public int currentFlareAmmo = 0;                        // 当前照明弹数
    [Export] public int minLaunchRange = 0;                          // 投射最小射程（绿色预览）
    [Export] public int maxLaunchRange = 5;                          // 投射最大射程（绿色预览）
    [Export] public int minIlluminationRange = 0;                    // 照明覆盖最小射程（黄色预览）
    [Export] public int maxIlluminationRange = 2;                    // 照明覆盖最大射程（黄色预览）
    [Export] public int flareDurationTurns = 1;                      // 照明效果持续大回合数
    [Export] public bool canIlluminateAfterMove = false;             // 移动后是否可以照明


    // ========== ✅ 通用修复模块（RepairBoat / APC 等维修单位可用）==========
    [ExportGroup("修复模块")]
    [Export] public bool canRepair = false;                          // 是否可以修复
    public virtual bool CanPerformRepair => canRepair;               // ✅ 虚属性：子类可覆盖（解决字段隐藏问题）
    [Export] public int minRepairRange = 1;                          // 最小修复范围
    [Export] public int maxRepairRange = 1;                          // 最大修复范围
    [Export] public bool repairCostsFunds = false;                   // 修复是否消耗资金
    [Export] public int repairCostPerHp = 0;                         // 每点血量消耗资金
    [Export] public bool canExceedMaxHealth = false;                 // 是否能超越最大血量
    [Export] public int exceededMaxHealth = 100;                     // 超越后的新最大值
    [Export] public bool canRepairAfterMove = true;                  // 是否可以移动后修复
    [Export] public int repairAmountPerAction = 1;                   // 每次修复多少血量（可为负=扣血，可为0）
    [Export] public bool isAreaRepair = false;                       // 能否群体修复
    [Export] public int repairTargetMode = 1;                        // 0=所有 1=仅友方 2=仅敌方
    [Export] public bool autoRepairOnTurnStart = false;              // 每回合自动修复（不占用操作）
    [Export] public bool autoRepairAfterMove = false;                // 移动后自动修复（不占用操作）
    [Export] public bool autoSupplyOnTurnStart = false;              // 每回合自动补给（不占用操作）
    [Export] public bool supplyCostsFunds = false;                   // 补给是否消耗资金

    // 当前占领进度（运行时）
    [ExportGroup("自爆弹模块")]
    [Export] public int maxExplodeAmmo = 0;                          // 最大自爆弹数
    [Export] public int currentExplodeAmmo = 0;                      // 当前自爆弹数


    // 当前占领进度（运行时）
    public int currentCaptureProgress = 0;
    public Grids capturingGrid = null;  // 正在占领的格子
    private CaptureEffect currentCaptureEffect;
    // ========== ✅ 通用修理模块（任何 canRepair 单位可用；原 RepairBoat 专属逻辑上移） ==========
    /// <summary>修理范围：[min,max] 环；repairTargetMode=3（仅自己）时只含自身格</summary>
    public virtual List<Grids> CalculateRepairRange()
    {
        var result = new List<Grids>();
        var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.gridManager == null || grid == null) return result;
        if (repairTargetMode == 3) { result.Add(grid); return result; }
        var allRange = gm.gridManager.FindRange(grid, maxRepairRange, false);
        if (minRepairRange > 1)
        {
            var blindZone = gm.gridManager.FindRange(grid, minRepairRange - 1, false);
            allRange = allRange.Where(g => !blindZone.Contains(g)).ToList();
        }
        return allRange.Where(g => g != grid).ToList();
    }

    /// <summary>目标是否符合修理模式：0=所有 1=仅友方 2=仅敌方 3=仅自己</summary>
    public bool IsRepairTargetAllowed(Infantry unit)
    {
        if (unit == null || !IsInstanceValid(unit) || unit.isTransported) return false;
        bool isAlly = unit.team == team;
        switch (repairTargetMode)
        {
            case 1: if (!isAlly || unit == this) return false; break;
            case 2: if (isAlly) return false; break;
            case 3: if (unit != this) return false; break;
            default: if (unit == this) return false; break;
        }
        // 加血模式：满血（且不能超上限）无意义
        if (repairAmountPerAction > 0 && !canExceedMaxHealth && unit.health >= unit.maxHealth) return false;
        if (repairAmountPerAction == 0) return false;
        return true;
    }

    public virtual List<Infantry> GetRepairableTargets()
    {
        var result = new List<Infantry>();
        foreach (var g in CalculateRepairRange())
        {
            if (g == null) continue;
            foreach (var unit in g.infantries)
                if (IsRepairTargetAllowed(unit)) result.Add(unit);
        }
        return result;
    }

    /// <summary>执行修理：正值加血（可耗资），负值扣血（受伤现形按广义规则处理）</summary>
    public virtual bool PerformRepair(Infantry target)
    {
        if (target == null || !IsInstanceValid(target) || !CanPerformRepair) return false;
        if (!CalculateRepairRange().Contains(target.grid)) return false;
        if (!IsRepairTargetAllowed(target)) return false;

        int actualRepair = repairAmountPerAction;
        var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
        if (actualRepair > 0)
        {
            int oldHealth = target.health;
            int healCap = canExceedMaxHealth ? exceededMaxHealth : target.maxHealth;
            int newHealth = Mathf.Min(healCap, target.health + actualRepair);
            int actualHealed = newHealth - oldHealth;
            if (actualHealed <= 0) return false;
            if (repairCostsFunds && gm != null)
            {
                int cost = actualHealed * repairCostPerHp;
                int currentFunds = gm.GetTeamFunds(team);
                if (currentFunds < cost) return false; // 资金不足
                gm.SetTeamFunds(team, currentFunds - cost);
            }
            target.health = newHealth;
            target.UpdateHpLabel();
            ShowRepairEffect(target, actualHealed);
        }
        else
        {
            int damage = Mathf.Abs(actualRepair);
            target.health -= damage;
            target.UpdateHpLabel();
            ShowRepairEffect(target, -damage);
            if (target.health <= 0)
            {
                target.OnDestroyed();
                if (target.grid != null)   // 与 Attack 的死亡处理一致：从格子移除，避免残留引用
                {
                    target.grid.infantries.Remove(target);
                    if (target.grid.infantry == target)
                        target.grid.infantry = target.grid.infantries.Count > 0 ? target.grid.infantries[0] : null;
                    target.grid = null;
                }
                gm?.RemovePiece(target);
                target.CallDeferred(Node.MethodName.QueueFree);
            }
        }
        return true;
    }

    public virtual void PerformAreaRepair()
    {
        if (!isAreaRepair) return;
        foreach (var target in GetRepairableTargets()) PerformRepair(target);
    }

    /// <summary>自动修理（回合开始 / 移动后），不占用操作</summary>
    public virtual void PerformAutoRepair()
    {
        if (!CanPerformRepair) return;
        if (isAreaRepair) PerformAreaRepair();
        else
        {
            var targets = GetRepairableTargets();
            if (targets.Count > 0) PerformRepair(targets.OrderBy(t => t.health).First());
        }
    }

    public virtual void OnTurnStartAutoRepair()
    {
        if (!autoRepairOnTurnStart) return;
        PerformAutoRepair();
    }

    // 移动后自动修理：移动标记 + 本回合行动结束（state=Acted）时触发一次，避免撤销移动刷血
    public bool movedThisTurnForAutoRepair = false;
    private bool autoRepairAfterMoveDone = false;

    protected void ShowRepairEffect(Infantry target, int amount)
    {
        var label = new Label();
        label.Text = amount > 0 ? $"+{amount}" : $"{amount}";
        label.Modulate = amount > 0 ? new Color(1, 0.4f, 0.7f) : new Color(0.8f, 0.2f, 0.2f);
        label.Position = target.GlobalPosition + new Godot.Vector2(0, -20);
        label.ZIndex = 300;
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeConstantOverride("outline_size", 3);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        GetTree()?.CurrentScene?.AddChild(label);
        var tween = CreateTween();
        tween.TweenProperty(label, "position", label.Position + new Godot.Vector2(0, -40), 1.2f);
        tween.TweenProperty(label, "modulate:a", 0.0f, 0.5f);
        tween.TweenCallback(Callable.From(() => label.QueueFree()));
    }

    public virtual bool CanMove()
    {
        if (!consumeFuel) return true;
        return fuel > 0;
    }

    public virtual void ConsumeFuel(int amount)
    {
        if (!consumeFuel) return;
        fuel -= amount;
    }

    // ✅ 每日回合开始时自动消耗燃料
    public virtual void ConsumeDailyFuel()
    {
        bool hasStealthCost = isStealthed && HasAnyStealthCost();
        if ((!consumeFuel || dailyFuelConsumption <= 0) && !hasStealthCost) return;
        if (consumeFuel && dailyFuelConsumption > 0) fuel -= dailyFuelConsumption;

        stealthFlareRevealed = false;   // 照明临时显形每回合刷新
        ApplyStealthDailyCost();   // ✅ 隐身日耗（独立于燃料开关）

        // ✅ 燃料耗尽坠毁：统一在日耗全部结算后判定一次（修复：此前与 ApplyStealthDailyCost 各判一次，隐身时重复销毁）
        if (fuel <= 0 && destroyOnOutOfFuel && !pendingFuelDestroy)
        {
            pendingFuelDestroy = true;
            // 延迟一帧销毁，避免在迭代中修改集合
            CallDeferred(nameof(DestroySelf));
        }
    }
    private bool pendingFuelDestroy = false;
    private bool stealthVisualApplied = false;   // 运行时：当前已应用的隐身外观   // 运行时：已排队坠毁，防重复

    private void DestroySelf()
    {
        if (!IsInstanceValid(this) || IsQueuedForDeletion()) return;
        var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.unitManager?.RemoveUnit(this);
    }

    // ✅ 自爆接口（GameManager 调用执行）
    public virtual void OnExplode()
    {
        if (isStealthed && !canCustomActionWhileStealthed) return; // ✅ 隐身行为锁
    }

    public Grids originalGrid;

    public virtual int attack
    {
        get 
        { 
            float healthPercent = (float)health / maxHealth;
            return Mathf.Max(0, Mathf.RoundToInt(baseAttack * healthPercent));
        }
    }

    public int defense
    {
        get 
        { 
            float healthPercent = (float)health / maxHealth;
            return Mathf.Max(0, Mathf.RoundToInt(baseDefense * healthPercent));
        }
    }

    /// <summary>
    /// 获取实际防御力（包含地形加成，如果允许）
    /// </summary>

    public virtual int secondaryAtk
    {
        get 
        { 
            float healthPercent = (float)health / maxHealth;
            return Mathf.Max(0, Mathf.RoundToInt(secondaryAttack * healthPercent));
        }
    }

    [Export]
    public float counterMul = 0.5f;

    [Export]
    public string team = "Player";

    [Export] public Sprite2D spriteRed;   // Osinfantry - 红方P1贴图
    [Export] public Sprite2D spriteBlue; 
    public bool isMoved;
    public bool isAttacked;
    public Label hpLabel;
    public Sprite2D sprite;
    [Export] public AnimatedSprite2D animSprite;   // ✅ 供子类使用（AnimatedSprite2D模式）
    [Export] public Sprite2D idleSprite;            // ✅ 供子类使用（静态贴图）
    [Export] public Sprite2D baseSprite;            // ✅ 双图层：灰白Base图（染色层）
    [Export] public Sprite2D normalSprite;          // ✅ 双图层：彩色Normal图（肤色/不变色层）
    [Export] public AnimatedSprite2D baseAnimSprite;    // ✅ 双图层动画：灰白Base动画（染色层）
    [Export] public AnimatedSprite2D normalAnimSprite;  // ✅ 双图层动画：彩色Normal动画（不变色层）

    public  Color normal = Colors.White;
    public  Color dim = new Color(0.7f, 0.7f, 0.7f, 1.0f);

    // ✅ 视觉组件原始 scale（用于呼吸动画基准，不覆盖场景中的设计 scale）
    private Vector2 _originalVisualScale = Vector2.One;

public override void _Ready()
{
    ZIndex = 10;
    AddToGroup("infantry");

    // 视觉组件获取
    spriteRed = GetNodeOrNull<Sprite2D>("Osinfantry");
    spriteBlue = GetNodeOrNull<Sprite2D>("Bminfantry");
    UpdateActiveSprite();
    // ✅ 有 animSprite 时不再兜底找 sprite（避免 FindChild 错误匹配空 Sprite2D 如 BaseSprite）
    if (sprite == null && animSprite == null)
    {
        sprite = GetNodeOrNull<Sprite2D>("Osinfantry");
        if (sprite == null)
            sprite = FindChild("*", true, false) as Sprite2D;
        if (sprite == null)
            sprite = GetChildOrNull<Sprite2D>(0);
    }

    // ✅ 动画组件自动获取（子类不再需要重复写）
    if (animSprite == null)
        animSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
    if (animSprite == null)
        animSprite = GetNodeOrNull<AnimatedSprite2D>("Base2D");
    if (idleSprite == null)
        idleSprite = GetNodeOrNull<Sprite2D>("Sprite2D");
    if (idleSprite == null)
        idleSprite = GetNodeOrNull<Sprite2D>("BaseSprite");
    
    // ✅ 双图层组件自动获取（Sprite2D 版）
    if (baseSprite == null)
        baseSprite = GetNodeOrNull<Sprite2D>("BaseSprite");
    if (normalSprite == null)
        normalSprite = GetNodeOrNull<Sprite2D>("NormalSprite");
    
    // ✅ 双图层组件自动获取（AnimatedSprite2D 版）
    if (baseAnimSprite == null)
        baseAnimSprite = GetNodeOrNull<AnimatedSprite2D>("BaseAnimSprite");
    if (baseAnimSprite == null)
        baseAnimSprite = GetNodeOrNull<AnimatedSprite2D>("BaseAnimatedSprite");
    if (normalAnimSprite == null)
        normalAnimSprite = GetNodeOrNull<AnimatedSprite2D>("NormalAnimSprite");
    if (normalAnimSprite == null)
        normalAnimSprite = GetNodeOrNull<AnimatedSprite2D>("NormalAnimatedSprite");
    
    // ✅ 双图层 Sprite2D：把 Normal 层挂到 Base 层下，继承所有变换
    if (baseSprite != null && normalSprite != null && normalSprite.GetParent() != baseSprite)
    {
        normalSprite.Reparent(baseSprite, true);
    }
    // ✅ 双图层 AnimatedSprite2D：同理
    if (baseAnimSprite != null && normalAnimSprite != null && normalAnimSprite.GetParent() != baseAnimSprite)
    {
        normalAnimSprite.Reparent(baseAnimSprite, true);
    }

    actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
    hpLabel = GetNodeOrNull<Label>("HpLabel");
    if (noAmmoIcon == null)
        noAmmoIcon = GetNodeOrNull<AnimatedSprite2D>("NoAmmoIcon");

    // ✅ 只有勾选 useDefaultConfig 时才应用代码默认值和 UnitRegistry
    if (useDefaultConfig)
    {
        // ========== 全局统一默认值 ==========
        // 血量：所有单位100，Oozium在子类覆盖为200
        if (maxHealth == 0) maxHealth = 100;
        if (health == 0) health = maxHealth;

        // 防御力：所有单位0，Oozium在子类覆盖为10
        if (baseDefense == 0) baseDefense = 0;

        // 移动力
        if (defaultMovePoints == 0 && movePoints == 0) 
        {
            defaultMovePoints = 3;
            movePoints = defaultMovePoints;
        }
        if (movePoints == 0) movePoints = defaultMovePoints;

        // 射程
        if (maxAttackRange == 0) 
        {
            if (attackRange > 0) maxAttackRange = attackRange;
            else maxAttackRange = 1;
        }
        if (minAttackRange == 0) minAttackRange = 1;
        if (attackRange == 0 && maxAttackRange > 0) attackRange = maxAttackRange;

        // 武器系统（保留，但伤害不走这里）
        if (!hasPrimaryWeapon && !hasSecondaryWeapon)
        {
            if (hasSecondaryWeapon == false) hasSecondaryWeapon = true;
            if (secondaryAttack == 0) secondaryAttack = 100;
            secondaryAntiArmor = false;
            secondaryAntiInfantry = true;
        }
        // 修复：主武器单位也必须有副武器兜底，防止主武器弹药耗尽后无法攻击
        if (hasPrimaryWeapon && !hasSecondaryWeapon)
        {
            hasSecondaryWeapon = true;
            if (secondaryAttack == 0) secondaryAttack = 100;
            secondaryAntiArmor = false;
            secondaryAntiInfantry = true;
        }
        if (hasPrimaryWeapon)
        {
            if (maxPrimaryAmmo == 0 && primaryHasLimitedAmmo) maxPrimaryAmmo = 99;
            if (currentPrimaryAmmo <= 0 && maxPrimaryAmmo > 0) currentPrimaryAmmo = maxPrimaryAmmo;
        }

        // 占领
        if (captureAbility == default) captureAbility = CaptureAbility.CanCapture;
        if (capturePower == 0) capturePower = 10;

        // 燃料
        if (maxFuel == 0) maxFuel = 99;
        if (fuel == 0) fuel = maxFuel;

        // 反击系数
        if (counterMul == 0) counterMul = 0.5f;

        // === 从 UnitRegistry 读取配置（优先）===
        ApplyUnitRegistryConfig();
        // === 子类差异化配置入口（覆盖 UnitRegistry）===
        ApplyUnitSpecificDefaults();
        // 修复：子类可能修改了 defaultMovePoints，需要同步到 movePoints
        if (movePoints == 0) movePoints = defaultMovePoints;
    }

    // 初始化攻防表（无论 useDefaultConfig 如何都需要，用于查询）
    InitializeDefaultMatrix();

    // ✅ 记录视觉组件原始 scale（必须在 StartBreath 之前！）
    if (baseAnimSprite != null && IsInstanceValid(baseAnimSprite))
        _originalVisualScale = baseAnimSprite.Scale;
    else if (baseSprite != null && IsInstanceValid(baseSprite))
        _originalVisualScale = baseSprite.Scale;
    else if (animSprite != null && IsInstanceValid(animSprite))
        _originalVisualScale = animSprite.Scale;
    else if (sprite != null && IsInstanceValid(sprite))
        _originalVisualScale = sprite.Scale;

    UpdateTeamVisual();
    UpdateHpLabel();
    // ✅ ShowAnimState / StartBreath 已统一在 UpdateTeamVisual() 中处理
    //    避免 team 改变后重复调用或状态不同步

        // ========== ✅ 战争迷雾视野初始化 ==========
        if (visionRange < 0 && useConfigVision)
        {
            visionRange = VisionConfig.GetUnitVisionRange(TemplateId);
        }
        else if (visionRange >= 0)
        {
        }

        // ✅ 初始化单位专属地形加成矩阵（如果为空则填充默认值）
        if (overrideGlobalTerrainBonus && (unitTerrainVisionBonus == null || unitTerrainVisionBonus.Count == 0))
        {
            var defaultTable = VisionConfig.GetUnitTerrainBonusTable(TemplateId);
            unitTerrainVisionBonus = new Godot.Collections.Dictionary<GridType, int>();
            foreach (var kvp in defaultTable)
                unitTerrainVisionBonus[kvp.Key] = kvp.Value;
        }

        // ✅ P-1 单位默认开启视野共享（如果team是Player且未手动设置）
        if (team == TeamHelper.Player && !isPublicVisionUnit)
        {
            isPublicVisionUnit = true;
        }

}

// ========== 子类覆盖：单位特有默认值 ==========
public virtual void ApplyUnitSpecificDefaults() { }

// ✅ 从 UnitRegistry 读取兵种配置
protected virtual void ApplyUnitRegistryConfig()
{
    string unitId = UnitTypeId;
    var config = UnitRegistry.Get(unitId) ?? UnitRegistry.Get(TemplateId);   // 自定义兵种无专属配置时用参照兵种
    if (config == null) return;

    // 基础属性
    if (config.MoveType != default) moveType = config.MoveType;
    if (config.MovePoints > 0) defaultMovePoints = config.MovePoints;
    if (config.Cost > 0) cost = config.Cost;
    if (config.CanMerge) canMerge = true;
    if (!string.IsNullOrEmpty(config.DisplayName) && string.IsNullOrEmpty(Name))
        Name = config.DisplayName;

    // 武器 - 主
    hasPrimaryWeapon = config.HasPrimaryWeapon;
    if (config.HasPrimaryWeapon)
    {
        maxPrimaryAmmo = config.MaxPrimaryAmmo;
        primaryHasLimitedAmmo = config.PrimaryHasLimitedAmmo;
        if (currentPrimaryAmmo <= 0) currentPrimaryAmmo = maxPrimaryAmmo;
        primaryAntiArmor = config.PrimaryAntiArmor;
        primaryAntiInfantry = config.PrimaryAntiInfantry;
    }
    if (config.BaseAttack > 0) baseAttack = config.BaseAttack;

    // 武器 - 副
    hasSecondaryWeapon = config.HasSecondaryWeapon;
    if (config.HasSecondaryWeapon && config.SecondaryAttack > 0)
        secondaryAttack = config.SecondaryAttack;
    secondaryAntiArmor = config.SecondaryAntiArmor;
    secondaryAntiInfantry = config.SecondaryAntiInfantry;

    // 射程
    if (config.UseMinMaxAttackRange)
    {
        useMinMaxAttackRange = true;
        minAttackRange = config.MinAttackRange;
        maxAttackRange = config.MaxAttackRange;
        attackRange = maxAttackRange;
    }
    canAttackAfterMoving = config.CanAttackAfterMoving;
    cannotCounterWhenAttacked = config.CannotCounterWhenAttacked;
    canCounterWhenDefending = config.CanCounterWhenDefending;
    canCounterAtRange = config.CanCounterAtRange;

    // 燃料
    if (config.MaxFuel != 99) maxFuel = config.MaxFuel;
    consumeFuel = config.ConsumeFuel;
    if (config.DailyFuelConsumption > 0) dailyFuelConsumption = config.DailyFuelConsumption;
    destroyOnOutOfFuel = config.DestroyOnOutOfFuel;
    if (config.LowFuelThreshold != 15) lowFuelThreshold = config.LowFuelThreshold;

    // 防御
    if (config.BaseDefense > 0) baseDefense = config.BaseDefense;
    if (config.CounterMul != 0.5f) counterMul = config.CounterMul;

    // 占领
    captureAbility = config.CaptureAbility;
    if (config.CapturePower > 0) capturePower = config.CapturePower;

    // 特殊能力
    canDevour = config.CanDevour;
    if (config.CanIlluminate) canIlluminate = true;

    // 重叠规则
    overlapType = config.OverlapType;
    attackType = config.AttackType;
    isArmoredUnit = config.IsArmoredUnit;
    IsAirUnit = config.IsAirUnit;

    // 搭载
    if (config.CanTransportUnits)
    {
        canTransportUnits = true;
        canSupplyUnits = config.CanSupplyUnits;
        maxTransportCapacity = config.MaxTransportCapacity;
        if (config.TransportUnitTypes != null && config.TransportUnitTypes.Length > 0)
            canTransportUnitTypes = new Godot.Collections.Array<string>(config.TransportUnitTypes);
    }

    // 视野
    if (config.VisionRange >= 0) visionRange = config.VisionRange;
}

// ========== 子类覆盖：动画名称映射 ==========
public virtual string GetIdleAnimName() => null;
public virtual string GetMoveAnimName() => null;

private void UpdateActiveSprite()
{
    // ✅ 调色板模式：不再根据team切换spriteRed/spriteBlue
    // 势力颜色由Shader根据team动态替换
    if (spriteRed != null && spriteBlue != null)
    {
        // 旧双贴图模式fallback：显示spriteRed作为中性模板
        spriteRed.Show();
        spriteBlue.Hide();
        sprite = spriteRed;
    }
}

// ✅ 新增：切换队伍时刷新视觉
public virtual void UpdateTeamVisual()
{
    UpdateActiveSprite();
    
    // ✅ normal/dim 用于 shader 的 tone（全局变暗/变亮），保持 White/Gray
    //    势力颜色由 ApplyPaletteMaterial() 通过 primary_color/secondary_color 设置
    normal = Colors.White;
    dim = new Color(0.6f, 0.6f, 0.6f, 1.0f);
    
    // ✅ 应用调色板Shader
    ApplyPaletteMaterial();
    
    // ✅ 刷新动画状态（team 改变后同步动画名和 tone）
    if (animSprite != null && IsInstanceValid(animSprite))
    {
        string animName = GetIdleAnimName();
        if (!string.IsNullOrEmpty(animName))
            ShowAnimState(animName);
        else
            ShowAnimState("idle");
    }
    else if (sprite != null && IsInstanceValid(sprite))
    {
        ShowAnimState(null);
    }
    
    // 确保呼吸动画作用到正确的 sprite
    if (breathTween != null && breathTween.IsValid())
    {
        breathTween.Kill();
        breathTween = null;
    }
    
    if (sprite != null && IsInstanceValid(sprite) && !isMoved && state != UnitState.Acted)
    {
        sprite.Scale = _originalVisualScale;
        SetWaitVisual(false);
        StartBreath();
    }
    else if (animSprite != null && IsInstanceValid(animSprite) && !isMoved && state != UnitState.Acted)
    {
        animSprite.Scale = _originalVisualScale;
        SetWaitVisual(false);
        StartBreath();
    }
}

        /// <summary>
    /// ✅ 为视觉组件应用调色板Shader（LUT方案C + 双图层方案）
    /// </summary>
    protected virtual void ApplyPaletteMaterial()
    {
        // ✅ 双图层 AnimatedSprite2D 方案（优先）
        if (baseAnimSprite != null && IsInstanceValid(baseAnimSprite) && 
            normalAnimSprite != null && IsInstanceValid(normalAnimSprite))
        {
            var baseMat = FactionPaletteManager.GetBaseLayerMaterial(team);
            if (baseMat != null)
            {
                baseAnimSprite.Material = (ShaderMaterial)baseMat.Duplicate();
                baseAnimSprite.Show();
            }
            normalAnimSprite.Material = null;
            normalAnimSprite.Show();
            // 隐藏旧版单图层组件，避免重叠
            if (animSprite != null) animSprite.Hide();
            if (sprite != null) sprite.Hide();
            if (spriteRed != null) spriteRed.Hide();
            if (spriteBlue != null) spriteBlue.Hide();
            return;
        }
        
        // ✅ 双图层 Sprite2D 方案
        if (baseSprite != null && IsInstanceValid(baseSprite) && 
            normalSprite != null && IsInstanceValid(normalSprite))
        {
            var baseMat = FactionPaletteManager.GetBaseLayerMaterial(team);
            if (baseMat != null)
            {
                baseSprite.Material = (ShaderMaterial)baseMat.Duplicate();
                baseSprite.Show();
            }
            normalSprite.Material = null;
            normalSprite.Show();
            // 隐藏旧版单图层组件
            if (animSprite != null) animSprite.Hide();
            if (sprite != null) sprite.Hide();
            if (spriteRed != null) spriteRed.Hide();
            if (spriteBlue != null) spriteBlue.Hide();
            return;
        }
        
        // Fallback：兼容模式（现有红蓝双色贴图）
        // 先隐藏双图层节点
        if (baseAnimSprite != null) baseAnimSprite.Hide();
        if (normalAnimSprite != null) normalAnimSprite.Hide();
        if (baseSprite != null) baseSprite.Hide();
        if (normalSprite != null) normalSprite.Hide();
        
        var mat = FactionPaletteManager.GetMaterial(team);
        if (mat == null) return;
        
        // 给animSprite应用调色板材质
        if (animSprite != null && IsInstanceValid(animSprite))
        {
            var animMat = (ShaderMaterial)mat.Duplicate();
            animSprite.Material = animMat;
            animSprite.Show();
        }
        
        // 给sprite应用调色板材质
        if (sprite != null && IsInstanceValid(sprite))
        {
            var spriteMat = (ShaderMaterial)mat.Duplicate();
            sprite.Material = spriteMat;
            sprite.Show();
        }
    }

public override void _Process(double delta)
{
    // ✅ 广义受伤现形
    if (damageRevealLastHp < 0) damageRevealLastHp = health;
    else if (health < damageRevealLastHp)
    {
        if (stealthRevealDamageScope == 1 && isStealthed && health > 0)
        {
            ApplyStealthRevealOnDamage(this);
            CheckStealthThresholds();
        }
        damageRevealLastHp = health;
    }
    else if (health > damageRevealLastHp) damageRevealLastHp = health;

    // ✅ 移动后自动修理：移动过且本回合行动已结束时触发一次（不占操作；撤销移动不会刷血）
    if (autoRepairAfterMove && movedThisTurnForAutoRepair && !autoRepairAfterMoveDone && state == UnitState.Acted && CanPerformRepair)
    {
        autoRepairAfterMoveDone = true;
        PerformAutoRepair();
    }

    // ✅ 隐身半透明与状态同步（修复：读档/编辑器载入 isStealthed=true 时不会重设半透明）
    if (isStealthed != stealthVisualApplied)
    {
        stealthVisualApplied = isStealthed;
        Modulate = isStealthed ? new Color(1, 1, 1, 0.45f) : new Color(1, 1, 1, 1);
    }
    // ✅ noFuelIcon 闪烁（从子类抽到基类统一处理）
    if (noFuelIcon != null)
    {
        if (!consumeFuel)
            noFuelIcon.Visible = false;
        else if (fuel <= 0)
            noFuelIcon.Visible = true;
        else if (fuel <= lowFuelThreshold)
            noFuelIcon.Visible = (Time.GetTicksMsec() / 500) % 2 == 0;
        else
            noFuelIcon.Visible = false;
    }

    // noAmmoIcon 闪烁
    if (noAmmoIcon != null)
    {
        if (!hasPrimaryWeapon)
            noAmmoIcon.Visible = false;
        else if (!CanUsePrimaryWeapon())
            noAmmoIcon.Visible = true;
        else if (currentPrimaryAmmo <= 3)
            noAmmoIcon.Visible = (Time.GetTicksMsec() / 500) % 2 == 0;
        else
            noAmmoIcon.Visible = false;
    }
}
    public virtual void OnDestroyed()
    {
        
        // 如果正在占领中，清理占领状态
        if (currentCaptureProgress > 0 && capturingGrid != null)
        {
            // 通知格子占领中断
            capturingGrid.city?.OnCaptureInterrupted(this);
            ShowCaptureInterruptedEffect();
            // 清理自己的占领状态
            currentCaptureProgress = 0;
            capturingGrid = null;
        }
        
        // 从格子中移除
        if (grid != null)
        {
            grid.infantries.Remove(this);
            if (grid.infantry == this)
            {
                grid.infantry = grid.infantries.Count > 0 ? grid.infantries[0] : null;
            }
            grid = null;
        }
    }
    public virtual int GetCapturePower()
{
    if (health <= maxHealth)
    {
        float healthPerBar = maxHealth / 10f;
        return Mathf.Clamp(Mathf.RoundToInt(health / healthPerBar), 1, 10);
    }
    else
    {
        float healthPerBar = maxHealth / 10f;
        return Mathf.RoundToInt(health / healthPerBar);
    }
}


public virtual int GetMoveCost(GridType gridType)
{
    MoveType moveType = (isStealthed && stealthMoveTypeOverride >= 0) ? (MoveType)stealthMoveTypeOverride : this.moveType;   // ✅ 隐身移动方式改写（局部遮蔽）
        var extraSave = ExtraSave.Instance;
    if (extraSave != null)
    {
        string terrainName = gridType.ToString();
        string moveTypeName = moveType.ToString();
        int customCost = extraSave.GetMoveCost(moveTypeName, terrainName);
        if (customCost >= 0) return customCost; // -1 表示走内置逻辑，继续原有代码
    }
    return moveType switch
    {
        // ========== Infantry（步兵）==========
        MoveType.Infantry => gridType switch
        {
            GridType.GROUND => 1,
            GridType.FOREST => 1,

            GridType.ROAD => 1,
            GridType.RIVER => 2,
            GridType.HILL => 2,
            GridType.BEACH => 1,
            GridType.TP => 0,
            GridType.REEF => 999,
            GridType.WHIRLPOOL => 999,
            GridType.LAVASIDE => 1,
            GridType.SEAFOG => 999,
            GridType.LANDFOG => 3,
            GridType.WATERFALL => 999,
            GridType.CLIFF => 999,
            GridType.SLOPE => 2,
            GridType.CAVE => 1,
            GridType.HOLE => 1,
            GridType.SEA => 999,
            GridType.METEORITE => 999,
            GridType.PIPE => 999,
            GridType.LAVA => 999,
            GridType.PIPESEAM => 999,  
            // 新增地形
            GridType.TRACK => 2,
            GridType.STATION => 1,
            GridType.BRIDGE => 1,
            GridType.LAVABRIDGE => 1,
            GridType.PASSABLEPIPE => 1,
            GridType.SHIPGATE => 1,
            GridType.OVERPASS => 1,
            GridType.BROKENPIPE => 3,
            GridType.RUINS => 2,
            GridType.BROKENTRACK => 2,
            GridType.LAVAFOG => 999, 
            _ => 999
        },

        // ========== Mech（机甲）==========
        MoveType.Mech => gridType switch
        {
            GridType.GROUND => 1,
            GridType.FOREST => 1,

            GridType.ROAD => 1,
            GridType.RIVER => 1,
            GridType.HILL => 1,
            GridType.BEACH => 1,
            GridType.TP => 0,
            GridType.REEF => 999,
            GridType.WHIRLPOOL => 999,
            GridType.LAVASIDE => 1,
            GridType.SEAFOG => 999,
            GridType.LANDFOG => 3,
            GridType.WATERFALL => 999,
            GridType.CLIFF => 999,
            GridType.SLOPE => 2,
            GridType.CAVE => 1,
            GridType.HOLE => 1,
            GridType.SEA => 999,
            GridType.METEORITE => 999,
            GridType.PIPE => 999,
            GridType.LAVA => 999,
            GridType.PIPESEAM => 999, 
            // 新增地形
            GridType.TRACK => 1,
            GridType.STATION => 1,
            GridType.BRIDGE => 1,
            GridType.LAVABRIDGE => 1,
            GridType.PASSABLEPIPE => 1,
            GridType.SHIPGATE => 1,
            GridType.OVERPASS => 1,
            GridType.BROKENPIPE => 2,
            GridType.RUINS => 1,
            GridType.BROKENTRACK => 1,
            GridType.LAVAFOG => 999, 
            _ => 999
        },

        // ========== Oozium（史莱姆）==========
        MoveType.Oozium => gridType switch
        {
            GridType.GROUND => 1,
            GridType.FOREST => 1,

            GridType.ROAD => 1,
            GridType.SEA => 1,
            GridType.RIVER => 1,
            GridType.HILL => 1,
            GridType.LAVA => 1,
            GridType.BEACH => 1,
            GridType.TP => 0,
            GridType.REEF => 1,
            GridType.WHIRLPOOL => 2,
            GridType.LAVASIDE => 1,
            GridType.SEAFOG => 2,
            GridType.LANDFOG => 2,
            GridType.WATERFALL => 999,
            GridType.CLIFF => 1,
            GridType.SLOPE => 1,
            GridType.CAVE => 1,
            GridType.HOLE => 1,
            GridType.METEORITE => 999,
            GridType.PIPE => 999,
            GridType.PIPESEAM => 999,  // Oozium不能通过管道接缝
            // 新增地形
            GridType.TRACK => 1,
            GridType.STATION => 1,
            GridType.BRIDGE => 1,
            GridType.LAVABRIDGE => 1,
            GridType.PASSABLEPIPE => 1,
            GridType.SHIPGATE => 1,
            GridType.OVERPASS => 1,
            GridType.BROKENPIPE => 1,
            GridType.RUINS => 1,
            GridType.BROKENTRACK => 1,
            GridType.LAVAFOG => 2,  
            _ => 999
        },

        // ========== Treads（履带）==========
        MoveType.Treads => gridType switch
        {
            GridType.GROUND => 1,

            GridType.ROAD => 1,
            GridType.FOREST => 2,
            GridType.BEACH => 1,
            GridType.TP => 0,
            GridType.REEF => 999,
            GridType.WHIRLPOOL => 999,
            GridType.LAVASIDE => 1,
            GridType.SEAFOG => 999,
            GridType.LANDFOG => 3,
            GridType.WATERFALL => 999,
            GridType.CLIFF => 999,
            GridType.SLOPE => 2,
            GridType.CAVE => 1,
            GridType.HOLE => 1,
            GridType.SEA => 999,
            GridType.RIVER => 999,
            GridType.HILL => 999,
            GridType.METEORITE => 999,
            GridType.PIPE => 999,
            GridType.LAVA => 999,
            GridType.PIPESEAM => 999,  // 履带不能通过管道接缝
            // 新增地形
            GridType.TRACK => 2,
            GridType.STATION => 1,
            GridType.BRIDGE => 1,
            GridType.LAVABRIDGE => 1,
            GridType.PASSABLEPIPE => 1,
            GridType.SHIPGATE => 1,
            GridType.OVERPASS => 1,
            GridType.BROKENPIPE => 3,
            GridType.RUINS => 3,
            GridType.BROKENTRACK => 3,
            GridType.LAVAFOG => 999,  
            _ => 999
        },

        // ========== Tire（轮胎）==========
        MoveType.Tire => gridType switch
        {

            GridType.ROAD => 1,
            GridType.GROUND => 2,
            GridType.FOREST => 3,
            GridType.BEACH => 1,
            GridType.TP => 0,
            GridType.REEF => 999,
            GridType.WHIRLPOOL => 999,
            GridType.LAVASIDE => 1,
            GridType.SEAFOG => 999,
            GridType.LANDFOG => 3,
            GridType.WATERFALL => 999,
            GridType.CLIFF => 999,
            GridType.SLOPE => 3,
            GridType.CAVE => 1,
            GridType.HOLE => 1,
            GridType.SEA => 999,
            GridType.RIVER => 999,
            GridType.HILL => 999,
            GridType.METEORITE => 999,
            GridType.PIPE => 999,
            GridType.LAVA => 999,
            GridType.PIPESEAM => 999, 
            // 新增地形
            GridType.TRACK => 3,
            GridType.STATION => 2,
            GridType.BRIDGE => 2,
            GridType.LAVABRIDGE => 2,
            GridType.PASSABLEPIPE => 2,
            GridType.SHIPGATE => 2,
            GridType.OVERPASS => 2,
            GridType.BROKENPIPE => 4,
            GridType.RUINS => 3,
            GridType.BROKENTRACK => 3,
            GridType.LAVAFOG => 999, 
            _ => 999
        },

        // ========== Naval（海军）==========
        MoveType.Naval => gridType switch
        {
            GridType.SEA => 1,
            GridType.TP => 0,
            GridType.REEF => 2,
            GridType.WHIRLPOOL => 3,
            GridType.SEAFOG => 3,
            GridType.CAVE => 1,
            GridType.HOLE => 1,
            GridType.PIPESEAM => 999, 
            GridType.GROUND => 999,
            GridType.FOREST => 999,

            GridType.ROAD => 999,
            GridType.RIVER => 999,
            GridType.HILL => 999,
            GridType.METEORITE => 999,
            GridType.PIPE => 999,
            GridType.LAVA => 999,
            GridType.BEACH => 999,
            GridType.LAVASIDE => 999,
            GridType.LANDFOG => 999,
            GridType.WATERFALL => 999,
            GridType.CLIFF => 999,
            GridType.SLOPE => 999,
            // 新增地形
            GridType.TRACK => 999,
            GridType.STATION => 999,
            GridType.BRIDGE => 2,
            GridType.LAVABRIDGE => 999,
            GridType.PASSABLEPIPE => 999,
            GridType.SHIPGATE => 2,
            GridType.OVERPASS => 2,
            GridType.BROKENPIPE => 999,
            GridType.RUINS => 999,
            GridType.BROKENTRACK => 999,
            GridType.LAVAFOG => 999, 
            _ => 999
        },

        // ========== AirPlane（战机）==========
        MoveType.AirPlane => gridType switch
        {
            GridType.GROUND => 1,
            GridType.FOREST => 1,

            GridType.ROAD => 1,
            GridType.SEA => 1,
            GridType.RIVER => 1,
            GridType.HILL => 1,
            GridType.LAVA => 1,
            GridType.BEACH => 1,
            GridType.TP => 0,
            GridType.REEF => 1,
            GridType.WHIRLPOOL => 1,
            GridType.LAVASIDE => 1,
            GridType.SEAFOG => 2,
            GridType.LANDFOG => 2,
            GridType.WATERFALL => 1,
            GridType.CLIFF => 1,
            GridType.SLOPE => 1,
            GridType.CAVE => 1,
            GridType.HOLE => 1,
            GridType.METEORITE => 999,
            GridType.PIPE => 999,
            GridType.PIPESEAM => 999,  // 战机不能通过管道接缝
            // 新增地形
            GridType.TRACK => 1,
            GridType.STATION => 1,
            GridType.BRIDGE => 1,
            GridType.LAVABRIDGE => 1,
            GridType.PASSABLEPIPE => 1,
            GridType.SHIPGATE => 1,
            GridType.OVERPASS => 1,
            GridType.BROKENPIPE => 1,
            GridType.RUINS => 1,
            GridType.BROKENTRACK => 1,
            GridType.LAVAFOG => 2,
            _ => 999
        },

        // ========== AirShip（飞艇）==========
        MoveType.AirShip => gridType switch
        {
            GridType.GROUND => 1,
            GridType.FOREST => 1,

            GridType.ROAD => 1,
            GridType.SEA => 1,
            GridType.RIVER => 1,
            GridType.HILL => 1,
            GridType.LAVA => 1,
            GridType.BEACH => 1,
            GridType.TP => 0,
            GridType.REEF => 1,
            GridType.WHIRLPOOL => 1,
            GridType.LAVASIDE => 1,
            GridType.SEAFOG => 2,
            GridType.LANDFOG => 2,
            GridType.WATERFALL => 1,
            GridType.CLIFF => 1,
            GridType.SLOPE => 1,
            GridType.CAVE => 1,
            GridType.HOLE => 1,
            GridType.METEORITE => 999,
            GridType.PIPE => 999,
            GridType.PIPESEAM => 999,  // 飞艇不能通过管道接缝
            // 新增地形
            GridType.TRACK => 1,
            GridType.STATION => 1,
            GridType.BRIDGE => 1,
            GridType.LAVABRIDGE => 1,
            GridType.PASSABLEPIPE => 1,
            GridType.SHIPGATE => 1,
            GridType.OVERPASS => 1,
            GridType.BROKENPIPE => 1,
            GridType.RUINS => 1,
            GridType.BROKENTRACK => 1,
            GridType.LAVAFOG => 2,
            _ => 999
        },

        // ========== Drone（无人机）==========
        MoveType.Drone => gridType switch
        {
            GridType.GROUND => 1,
            GridType.FOREST => 1,

            GridType.ROAD => 1,
            GridType.RIVER => 1,
            GridType.HILL => 1,
            GridType.BEACH => 1,
            GridType.TP => 0,
            GridType.REEF => 1,
            GridType.WHIRLPOOL => 1,
            GridType.LAVASIDE => 1,
            GridType.SEAFOG => 2,
            GridType.LANDFOG => 2,
            GridType.WATERFALL => 1,
            GridType.CLIFF => 1,
            GridType.SLOPE => 1,
            GridType.CAVE => 1,
            GridType.HOLE => 1,
            GridType.SEA => 999,
            GridType.LAVA => 999,
            GridType.METEORITE => 999,
            GridType.PIPE => 999,
            GridType.PIPESEAM => 999,  // 无人机不能通过管道接缝
            // 新增地形
            GridType.TRACK => 1,
            GridType.STATION => 1,
            GridType.BRIDGE => 1,
            GridType.LAVABRIDGE => 1,
            GridType.PASSABLEPIPE => 1,
            GridType.SHIPGATE => 1,
            GridType.OVERPASS => 1,
            GridType.BROKENPIPE => 3,
            GridType.RUINS => 2,
            GridType.BROKENTRACK => 2,
            GridType.LAVAFOG => 999,  
            _ => 999
        },

        // ========== AeroSpacer（空天战机）==========
        MoveType.AeroSpacer => gridType switch
        {
            GridType.GROUND => 1,
            GridType.FOREST => 1,

            GridType.ROAD => 1,
            GridType.SEA => 1,
            GridType.RIVER => 1,
            GridType.HILL => 1,
            GridType.LAVA => 1,
            GridType.BEACH => 1,
            GridType.TP => 0,
            GridType.REEF => 1,
            GridType.WHIRLPOOL => 1,
            GridType.LAVASIDE => 1,
            GridType.SEAFOG => 1,
            GridType.LANDFOG => 1,
            GridType.WATERFALL => 1,
            GridType.CLIFF => 1,
            GridType.SLOPE => 1,
            GridType.CAVE => 1,
            GridType.HOLE => 1,
            GridType.METEORITE => 999,
            GridType.PIPE => 999,
            GridType.PIPESEAM => 999,  
            GridType.TRACK => 1,
            GridType.STATION => 1,
            GridType.BRIDGE => 1,
            GridType.LAVABRIDGE => 1,
            GridType.PASSABLEPIPE => 1,
            GridType.SHIPGATE => 1,
            GridType.OVERPASS => 1,
            GridType.BROKENPIPE => 1,
            GridType.RUINS => 1,
            GridType.BROKENTRACK => 1,
            GridType.LAVAFOG => 1,
            _ => 999
        },

        // ========== HeliCopter（直升机）==========
        MoveType.HeliCopter => gridType switch
        {
            GridType.GROUND => 1,
            GridType.FOREST => 1,

            GridType.ROAD => 1,
            GridType.SEA => 1,
            GridType.RIVER => 1,
            GridType.HILL => 1,
            GridType.BEACH => 1,
            GridType.TP => 0,
            GridType.REEF => 1,
            GridType.WHIRLPOOL => 1,
            GridType.LAVASIDE => 1,
            GridType.SEAFOG => 2,
            GridType.LANDFOG => 2,
            GridType.WATERFALL => 1,
            GridType.CLIFF => 1,
            GridType.SLOPE => 1,
            GridType.CAVE => 1,
            GridType.HOLE => 1,
            GridType.LAVA => 999,
            GridType.METEORITE => 999,
            GridType.PIPE => 999,
            GridType.PIPESEAM => 999,  // 直升机不能通过管道接缝
            // 新增地形
            GridType.TRACK => 1,
            GridType.STATION => 1,
            GridType.BRIDGE => 1,
            GridType.LAVABRIDGE => 1,
            GridType.PASSABLEPIPE => 1,
            GridType.SHIPGATE => 1,
            GridType.OVERPASS => 1,
            GridType.BROKENPIPE => 2,
            GridType.RUINS => 1,
            GridType.BROKENTRACK => 1,
            GridType.LAVAFOG => 999, 
            _ => 999
        },

        // ========== SpaceShiper（战舰）==========
        MoveType.SpaceShiper => gridType switch
        {
            GridType.GROUND => 1,
            GridType.FOREST => 1,

            GridType.ROAD => 1,
            GridType.SEA => 1,
            GridType.RIVER => 1,
            GridType.HILL => 1,
            GridType.LAVA => 1,
            GridType.BEACH => 1,
            GridType.TP => 0,
            GridType.REEF => 1,
            GridType.WHIRLPOOL => 1,
            GridType.LAVASIDE => 1,
            GridType.SEAFOG => 1,
            GridType.LANDFOG => 1,
            GridType.WATERFALL => 1,
            GridType.CLIFF => 1,
            GridType.SLOPE => 1,
            GridType.CAVE => 1,
            GridType.HOLE => 1,
            GridType.METEORITE => 1,
            GridType.PIPE => 1,
            GridType.PIPESEAM => 1,    // 战舰可通过管道接缝
            // 新增地形
            GridType.TRACK => 1,
            GridType.STATION => 1,
            GridType.BRIDGE => 1,
            GridType.LAVABRIDGE => 1,
            GridType.PASSABLEPIPE => 1,
            GridType.SHIPGATE => 1,
            GridType.OVERPASS => 0,     // 战舰在立交桥消耗为0！
            GridType.BROKENPIPE => 1,
            GridType.RUINS => 1,
            GridType.BROKENTRACK => 1,
            GridType.LAVAFOG => 1,
            _ => 1
        },

        // ========== Hover（气垫）==========
        MoveType.Hover => gridType switch
        {
            GridType.SEA => 1,
            GridType.RIVER => 2,
            GridType.BEACH => 1,
            GridType.TP => 0,
            GridType.REEF => 2,
            GridType.WHIRLPOOL => 3,
            GridType.LAVASIDE => 999,
            GridType.SEAFOG => 3,
            GridType.LANDFOG => 999,
            GridType.WATERFALL => 999,
            GridType.CLIFF => 999,
            GridType.SLOPE => 999,
            GridType.CAVE => 1,
            GridType.HOLE => 1,
            GridType.PIPESEAM => 999,  // 气垫不能通过管道接缝
            GridType.GROUND => 999,
            GridType.FOREST => 999,

            GridType.ROAD => 999,
            GridType.HILL => 999,
            GridType.METEORITE => 999,
            GridType.PIPE => 999,
            GridType.LAVA => 999,
            // 新增地形
            GridType.TRACK => 999,
            GridType.STATION => 999,
            GridType.BRIDGE => 1,
            GridType.LAVABRIDGE => 999,
            GridType.PASSABLEPIPE => 999,
            GridType.SHIPGATE => 1,
            GridType.OVERPASS => 1,
            GridType.BROKENPIPE => 999,
            GridType.RUINS => 999,
            GridType.BROKENTRACK => 999,
            GridType.LAVAFOG => 999, 
            _ => 999
        },

        // ========== PipeRunner（管道行者）==========
        MoveType.PipeRunner => gridType switch
        {
            GridType.PIPE => 1,
            GridType.PIPESEAM => 1,    // 管道行者可通过管道接缝
            GridType.TP => 0,
            GridType.GROUND => 999,
            GridType.FOREST => 999,

            GridType.ROAD => 999,
            GridType.SEA => 999,
            GridType.RIVER => 999,
            GridType.HILL => 999,
            GridType.METEORITE => 999,
            GridType.LAVA => 999,
            GridType.BEACH => 999,
            GridType.REEF => 999,
            GridType.WHIRLPOOL => 999,
            GridType.LAVASIDE => 999,
            GridType.SEAFOG => 999,
            GridType.LANDFOG => 999,
            GridType.WATERFALL => 999,
            GridType.CLIFF => 999,
            GridType.SLOPE => 999,
            GridType.CAVE => 999,
            GridType.HOLE => 999,
            // 新增地形
            GridType.TRACK => 999,
            GridType.STATION => 999,
            GridType.BRIDGE => 999,
            GridType.LAVABRIDGE => 999,
            GridType.PASSABLEPIPE => 1,
            GridType.SHIPGATE => 999,
            GridType.OVERPASS => 2,
            GridType.BROKENPIPE => 4,
            GridType.RUINS => 999,
            GridType.BROKENTRACK => 999,
            GridType.LAVAFOG => 999, 
            _ => 999
        },

        // ========== LAVARUNNER（岩浆行者）==========
        MoveType.LAVARUNNER => gridType switch
        {
            GridType.LAVA => 1,
            GridType.TP => 0,
            GridType.LAVABRIDGE => 2,
            GridType.SHIPGATE => 2,
            GridType.OVERPASS => 2,
            GridType.PIPESEAM => 999,  // 岩浆行者不能通过管道接缝
            // 新增地形
            GridType.TRACK => 999,
            GridType.STATION => 999,
            GridType.BRIDGE => 999,
            GridType.PASSABLEPIPE => 999,
            GridType.BROKENPIPE => 999,
            GridType.RUINS => 999,
            GridType.BROKENTRACK => 999,
            GridType.LAVAFOG => 3,
            _ => 999
        },

        // ========== LAVAHOVER（岩浆登陆者）==========
        MoveType.LAVAHOVER => gridType switch
        {
            GridType.LAVA => 1,
            GridType.LAVASIDE => 1,
            GridType.TP => 0,
            GridType.LAVABRIDGE => 1,
            GridType.SHIPGATE => 1,
            GridType.OVERPASS => 1,
            GridType.LAVAFOG => 3,
            GridType.PIPESEAM => 999,  // 岩浆登陆者不能通过管道接缝
            _ => 999
        },

        // ========== Train（火车）==========
        MoveType.Train => gridType switch
        {
            GridType.TRACK => 2,
            GridType.STATION => 1,
            GridType.OVERPASS => 2,
            GridType.BROKENTRACK => 2,
            GridType.TP => 0,
            _ => 999
        },

        // ========== GasTrain（蒸汽车）==========
        MoveType.GasTrain => gridType switch
        {
            GridType.TRACK => 3,
            GridType.STATION => 2,
            GridType.OVERPASS => 3,
            GridType.BROKENTRACK => 4,
            GridType.TP => 0,
            _ => 999
        },

        // ========== FASTER（高铁）==========
        MoveType.FASTER => gridType switch
        {
            GridType.TRACK => 1,
            GridType.STATION => 0,
            GridType.OVERPASS => 0,
            GridType.BROKENTRACK => 2,
            GridType.TP => 0,
            _ => 999
        },

        // ========== Missile（洲际导弹）==========
        MoveType.Missile => gridType switch
        {
            GridType.GROUND => 1,
            GridType.FOREST => 1,

            GridType.ROAD => 1,
            GridType.SEA => 1,
            GridType.RIVER => 1,
            GridType.HILL => 1,
            GridType.LAVA => 1,
            GridType.BEACH => 1,
            GridType.TP => 0,
            GridType.REEF => 1,
            GridType.WHIRLPOOL => 1,
            GridType.LAVASIDE => 1,
            GridType.SEAFOG => 5,
            GridType.LANDFOG => 4,
            GridType.WATERFALL => 1,
            GridType.CLIFF => 1,
            GridType.SLOPE => 1,
            GridType.CAVE => 1,
            GridType.HOLE => 1,
            GridType.METEORITE => 5,
            GridType.PIPE => 5,
            GridType.PIPESEAM => 5,
            GridType.TRACK => 1,
            GridType.STATION => 1,
            GridType.BRIDGE => 1,
            GridType.LAVABRIDGE => 1,
            GridType.PASSABLEPIPE => 1,
            GridType.SHIPGATE => 1,
            GridType.OVERPASS => 0,
            GridType.BROKENPIPE => 1,
            GridType.RUINS => 1,
            GridType.BROKENTRACK => 1,
            GridType.LAVAFOG => 6,
            _ => 999
        },

        _ => 1
    };
}

public void StartBreath()
{
    UpdateActiveSprite(); // 确保呼吸作用到正确的 sprite
    
    breathTween?.Kill();

    // ✅ 双图层 AnimatedSprite2D：BaseAnimSprite 做呼吸，NormalAnimSprite 自动跟随
    if (baseAnimSprite != null && IsInstanceValid(baseAnimSprite) &&
        normalAnimSprite != null && IsInstanceValid(normalAnimSprite))
    {
        baseAnimSprite.Scale = _originalVisualScale;
        
        breathTween = CreateTween();
        if (breathTween == null) return;
        breathTween.SetLoops();
        breathTween.SetProcessMode(Tween.TweenProcessMode.Idle);
        
        breathTween.TweenProperty(baseAnimSprite, "scale", _originalVisualScale * 1.05f, 1.2f)
                   .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        breathTween.TweenProperty(baseAnimSprite, "scale", _originalVisualScale, 1.2f)
                   .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        return;
    }

    // ✅ 双图层 Sprite2D：BaseSprite 做呼吸，NormalSprite 自动跟随
    if (baseSprite != null && IsInstanceValid(baseSprite) &&
        normalSprite != null && IsInstanceValid(normalSprite))
    {
        baseSprite.Scale = _originalVisualScale;
        
        breathTween = CreateTween();
        if (breathTween == null) return;
        breathTween.SetLoops();
        breathTween.SetProcessMode(Tween.TweenProcessMode.Idle);
        
        breathTween.TweenProperty(baseSprite, "scale", _originalVisualScale * 1.05f, 1.2f)
                   .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        breathTween.TweenProperty(baseSprite, "scale", _originalVisualScale, 1.2f)
                   .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        return;
    }

    // ✅ 支持 animSprite 和 sprite 两种模式（单图层）
    Node target = animSprite != null && IsInstanceValid(animSprite) ? animSprite : sprite;
    if (target == null || !IsInstanceValid(target))
        return;

    if (target is Node2D node2d)
        node2d.Scale = _originalVisualScale;

    breathTween = CreateTween();
    if (breathTween == null) return;

    breathTween.SetLoops();
    breathTween.SetProcessMode(Tween.TweenProcessMode.Idle);

    breathTween.TweenProperty(target, "scale", _originalVisualScale * 1.05f, 1.2f)
               .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    breathTween.TweenProperty(target, "scale", _originalVisualScale, 1.2f)
               .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
}

public void StopBreath()
{
    UpdateActiveSprite();
    
    breathTween?.Kill();
    
    // ✅ 双图层 AnimatedSprite2D：重置 BaseAnimSprite
    if (baseAnimSprite != null && IsInstanceValid(baseAnimSprite))
        baseAnimSprite.Scale = _originalVisualScale;
    
    // ✅ 双图层 Sprite2D：重置 BaseSprite
    if (baseSprite != null && IsInstanceValid(baseSprite))
        baseSprite.Scale = _originalVisualScale;
    
    // ✅ 单图层模式
    Node target = animSprite != null && IsInstanceValid(animSprite) ? animSprite : sprite;
    if (target is Node2D node2d)
        node2d.Scale = _originalVisualScale;
}

public virtual void UpdateHpLabel()
{
    if (hpLabel == null) return;
    
    int bars;
    
    if (health <= maxHealth)
    {
        // 正常情况：按 maxHealth 的 1/10 为基准，缩放到 1-10
        float healthPerBar = maxHealth / 10f;
        bars = Mathf.Clamp(Mathf.RoundToInt(health / healthPerBar), 1, 10);
    }
    else
    {
        // 超出血量：基础10格 + 超出部分按比例增加
        float healthPerBar = maxHealth / 10f;  // 每格代表的血量
        bars = Mathf.RoundToInt(health / healthPerBar);
    }
    
    hpLabel.Text = bars.ToString();
}

// Infantry.cs - 替换 SetWaitVisual
public virtual void SetWaitVisual(bool waiting)
{
    // 确保 sprite 指向正确的阵营节点
    UpdateActiveSprite();
    
    // ✅ 双图层 AnimatedSprite2D：tone + Modulate 双重保险
    if (baseAnimSprite != null && IsInstanceValid(baseAnimSprite))
    {
        if (baseAnimSprite.Material is ShaderMaterial baseMat)
            baseMat.SetShaderParameter("tone", waiting ? dim : normal);
        baseAnimSprite.Modulate = waiting ? dim : normal;
    }
    
    // ✅ 双图层 Sprite2D：tone + Modulate 双重保险
    if (baseSprite != null && IsInstanceValid(baseSprite))
    {
        if (baseSprite.Material is ShaderMaterial baseMat)
            baseMat.SetShaderParameter("tone", waiting ? dim : normal);
        baseSprite.Modulate = waiting ? dim : normal;
    }
    
    // ✅ LUT模式 / 旧模式：同时处理 animSprite 和 sprite
    Node2D target = animSprite != null && IsInstanceValid(animSprite) ? animSprite : sprite;
    if (target != null && IsInstanceValid(target))
    {
        if (target.Material is ShaderMaterial shaderMat)
            shaderMat.SetShaderParameter("tone", waiting ? dim : normal);
        target.Modulate = waiting ? dim : normal;
    }

    if (hpLabel != null)
        hpLabel.Modulate = Colors.White;
}

       public virtual bool CanCaptureCurrentGrid()
{
    if (captureAbility == CaptureAbility.CannotCapture)
        return false;
    
    if (grid == null)
        return false;
    
    if (grid.city == null)
        return false;
    
    // ✅ 关键：如果城市已被己方占领，不能再次占领
    if (grid.city.facilityTeam == team)
        return false;
    
    // ✅ 如果正在占领中，但占领的是当前格子，允许继续
    if (currentCaptureProgress > 0 && capturingGrid == grid)
        return true;
    
    // ✅ 如果其他己方单位正在占领，检查是否可以协助
    if (grid.city.capturingUnits.Count > 0)
    {
        var allyCapturing = grid.city.capturingUnits
            .FirstOrDefault(u => u.team == this.team && u != this);
        if (allyCapturing != null)
        {
            // 同队伍可以协助占领
            return true;
        }
    }
    
    return true;
}

    private bool isCapturingInProgress = false;
    public virtual void PerformCapture()
{
    if (isStealthed && !canCaptureWhileStealthed) return;   // ✅ 隐身占点锁

    if (isCapturingInProgress) return;
    
    if (!CanCaptureCurrentGrid())
        return;

    isCapturingInProgress = true;  // 设置标志
    
    try  // ✅ 添加 try-finally
    {
        int currentCapturePower = GetCapturePower();
        
        // 第一次占领这个格子
        if (capturingGrid != grid)
        {
            if (capturingGrid != null)
            {
                capturingGrid.city?.OnCaptureInterrupted(this);
            }
            
            currentCaptureProgress = 0;
            capturingGrid = grid;
            grid.city?.OnCaptureStarted(this);
        }
        
        var contestUnits = grid.infantries
            .Where(u => u != this 
                        && u != null
                        && IsInstanceValid(u)
                        && u.currentCaptureProgress > 0 
                        && u.capturingGrid == grid
                        && u.team != this.team)
            .ToList();
            
        if (contestUnits.Count > 0)
        {
            grid.city?.ShowCaptureContestVisual();
            currentCaptureProgress = 0;
            foreach (var enemy in contestUnits)
            {
                enemy.currentCaptureProgress = 0;
            }
            
            MarkAsCapturing();
            return;  // 这里return，但finally会执行！
        }
        
        currentCaptureProgress += currentCapturePower;
        
        ShowCaptureEffect();
        
        if (currentCaptureProgress >= (grid.city?.capturePointsRequired ?? 20))
        {
            CompleteCapture();
        }
        else
        {
            MarkAsCapturing();
        }
    }
    finally  // ✅ 确保标志总是重置
    {
        isCapturingInProgress = false;
    }
}


    protected virtual void CompleteCapture()
{
    if (grid == null) return;
    
    string oldOwner = grid.city?.facilityTeam ?? "";
    
    // ✅ 关键修复：实际改变城市归属！
    if (grid.city != null)
        grid.city.facilityTeam = team;
    
    // 显示占领完成特效
    ShowCaptureCompleteEffect();
    
    // 重置占领状态
    currentCaptureProgress = 0;
    capturingGrid = null;
    
    // 标记为已完全行动
    isMoved = true;
    isAttacked = true;
    state = UnitState.Acted;
    originalGrid = null;
    
    // 视觉变暗
    SetWaitVisual(true);
    
    // 通知刷新城市视觉
    grid.city?.UpdateCityVisual();
    
    var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
    gm?.CheckVictoryCondition();
}
        protected virtual void ShowCaptureInterruptedEffect()
    {
        var effect = new CaptureInterruptedEffect
        {
            Position = GlobalPosition
        };
        GetTree().CurrentScene.AddChild(effect);
    }

    protected virtual void MarkAsCapturing()
    {
        // 关键：占领中也算已行动，不能移动/攻击
        isMoved = true;
        isAttacked = true;
        state = UnitState.Acted;
        originalGrid = null;
        
        // 视觉变暗（但可能用不同颜色表示占领中？）
        FreezeToIdleDark();
    }

    protected virtual void ShowCaptureEffect()
    {
        // 清理旧特效
        if (currentCaptureEffect != null && IsInstanceValid(currentCaptureEffect))
        {
            currentCaptureEffect.QueueFree();
            currentCaptureEffect = null;
        }
        
        // ✅ 计算进度百分比（0-100）
        float progressPercent = Mathf.Min(100f, (currentCaptureProgress / (float)(grid.city?.capturePointsRequired ?? 20)) * 100f);
        
        currentCaptureEffect = new CaptureEffect
        {
            CapturePower = GetCapturePower(), 
            CaptureProgress = progressPercent,  // 传递正确的进度
            TeamColor = GetTeamColor(),
            IsCapturing = true,
            Position = new Vector2(0, -20)
        };
        AddChild(currentCaptureEffect);
    }

    protected virtual void ShowCaptureCompleteEffect()
    {
        if (currentCaptureEffect != null && IsInstanceValid(currentCaptureEffect))
        {
            currentCaptureEffect.QueueFree();
            currentCaptureEffect = null;
        }
        
        var effect = new CaptureEffect
        {
            CaptureProgress = 100f,
            TeamColor = GetTeamColor(),
            IsCapturing = false,  // 完成状态
            Position = new Vector2(0, -20)
        };
        AddChild(effect);
        // 完成特效不记录引用，让它自己消失
    }

    protected Color GetTeamColor()
    {
        return TeamHelper.GetTeamColor(team);
    }

// ✅ 新增：计算对兵器的伤害
    // ✅ 照明弹显形：本格是否处于照明弹亮格内
    public bool IsFlareLit()
    {
        if (grid == null || !stealthRevealOnFlare) return false;
        var gmx = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
        return gmx?.fogOfWarManager?.IsGridIlluminated(grid) == true;
    }

    // ==================== 隐身模块 ====================
    public void ToggleStealth()
    {
        if (!canStealth) return;
        if (IsStealthFormLocked) return;   // ✅ 形态锁定（手动锁/阈值锁）：玩家不可操作（阈值强制改写不受此限）
        // ✅ 弹系统（三选一模式，不能通吃）：切换扣弹，无弹拒绝
        if (stealthAmmoMode == 1)
        {
            if (!isStealthed) { if (currentStealthBullets <= 0) return; currentStealthBullets--; }
            else { if (currentRevealBullets <= 0) return; currentRevealBullets--; }
        }
        else if (stealthAmmoMode == 2)
        {
            if (currentCamoBullets <= 0) return;
            currentCamoBullets--;
        }
        isStealthed = !isStealthed;
        if (stealthToggleCostsAction)
        {
            SetWaitVisual(true);
            state = UnitState.Acted;   // 算独立操作：切换即待机
        }
        // 不算独立操作：state 保持，切换后仍可攻击/移动
        Modulate = isStealthed ? new Color(1, 1, 1, 0.45f) : new Color(1, 1, 1, 1);
        CheckStealthThresholds();
    }

    public void RevealStealth()
    {
        if (!isStealthed && !stealthFlareRevealed) return;
        isStealthed = false;
        stealthFlareRevealed = false;
        Modulate = new Color(1, 1, 1, 1);
    }

    public bool HasAnyStealthCost()
    {
        return stealthFuelCostPerDay > 0 || stealthHpCostPerDay > 0
            || stealthPrimaryAmmoCostPerDay > 0 || stealthExplodeAmmoCostPerDay > 0
            || stealthFlareAmmoCostPerDay > 0
            || stealthBulletCostPerDay > 0 || revealBulletCostPerDay > 0 || camoBulletCostPerDay > 0;
    }

    public void ApplyStealthDailyCost()   // 由 ConsumeDailyFuel 末尾调用
    {
        if (!isStealthed) return;
        fuel -= stealthFuelCostPerDay;
        if (stealthHpCostPerDay > 0) { health = Math.Max(1, health - stealthHpCostPerDay); }  // 标签刷新走接线阶段
        if (stealthPrimaryAmmoCostPerDay > 0) currentPrimaryAmmo = Math.Max(0, currentPrimaryAmmo - stealthPrimaryAmmoCostPerDay);
        if (stealthExplodeAmmoCostPerDay > 0) currentExplodeAmmo = Math.Max(0, currentExplodeAmmo - stealthExplodeAmmoCostPerDay);
        if (stealthFlareAmmoCostPerDay > 0) currentFlareAmmo = Math.Max(0, currentFlareAmmo - stealthFlareAmmoCostPerDay);
        if (stealthBulletCostPerDay > 0) currentStealthBullets = Math.Max(0, currentStealthBullets - stealthBulletCostPerDay);
        if (revealBulletCostPerDay > 0) currentRevealBullets = Math.Max(0, currentRevealBullets - revealBulletCostPerDay);
        if (camoBulletCostPerDay > 0) currentCamoBullets = Math.Max(0, currentCamoBullets - camoBulletCostPerDay);
        CheckStealthThresholds();
        // 坠毁判定统一在 ConsumeDailyFuel 末尾（防重复销毁）
    }

    private bool forceHideTriggered = false;    // 运行时：强制隐已触发(OneShot用)
    private bool forceRevealTriggered = false;  // 运行时：强制显已触发(OneShot用)

    // ✅ 阈值矩阵统一判定：双方向(强制隐/强制显)×双阈值(高/低)×八资源，0=关，到达即触发
    private void ForceStealthNow()   // 阈值强制隐：被动效果，不耗行动/不扣弹
    {
        if (isStealthed || !canStealth) return;
        isStealthed = true;
        Modulate = new Color(1, 1, 1, 0.45f);
    }

    private static bool ThresholdHit(int value, int high, int low)
        => (high > 0 && value >= high) || (low > 0 && value <= low);

    public void CheckStealthThresholds()
    {
        // 一次性模式：已锁存则永不重判
        if (stealthForceOneShot && stealthForceLatched) return;

        bool hideHit = false, revealHit = false;

        // —— Hp：高阈≥触发 / 低阈≤触发，中间不触发 ——
        if (stealthForceHideHpHigh > 0 && health >= stealthForceHideHpHigh) hideHit = true;
        if (stealthForceHideHpLow > 0 && health <= stealthForceHideHpLow) hideHit = true;
        if (stealthForceRevealHpHigh > 0 && health >= stealthForceRevealHpHigh) revealHit = true;
        if (stealthForceRevealHpLow > 0 && health <= stealthForceRevealHpLow) revealHit = true;
        // —— Fuel：高阈≥触发 / 低阈≤触发，中间不触发 ——
        if (stealthForceHideFuelHigh > 0 && fuel >= stealthForceHideFuelHigh) hideHit = true;
        if (stealthForceHideFuelLow > 0 && fuel <= stealthForceHideFuelLow) hideHit = true;
        if (stealthForceRevealFuelHigh > 0 && fuel >= stealthForceRevealFuelHigh) revealHit = true;
        if (stealthForceRevealFuelLow > 0 && fuel <= stealthForceRevealFuelLow) revealHit = true;
        // —— Primary：高阈≥触发 / 低阈≤触发，中间不触发 ——
        if (stealthForceHidePrimaryHigh > 0 && currentPrimaryAmmo >= stealthForceHidePrimaryHigh) hideHit = true;
        if (stealthForceHidePrimaryLow > 0 && currentPrimaryAmmo <= stealthForceHidePrimaryLow) hideHit = true;
        if (stealthForceRevealPrimaryHigh > 0 && currentPrimaryAmmo >= stealthForceRevealPrimaryHigh) revealHit = true;
        if (stealthForceRevealPrimaryLow > 0 && currentPrimaryAmmo <= stealthForceRevealPrimaryLow) revealHit = true;
        // —— Explode：高阈≥触发 / 低阈≤触发，中间不触发 ——
        if (stealthForceHideExplodeHigh > 0 && currentExplodeAmmo >= stealthForceHideExplodeHigh) hideHit = true;
        if (stealthForceHideExplodeLow > 0 && currentExplodeAmmo <= stealthForceHideExplodeLow) hideHit = true;
        if (stealthForceRevealExplodeHigh > 0 && currentExplodeAmmo >= stealthForceRevealExplodeHigh) revealHit = true;
        if (stealthForceRevealExplodeLow > 0 && currentExplodeAmmo <= stealthForceRevealExplodeLow) revealHit = true;
        // —— Flare：高阈≥触发 / 低阈≤触发，中间不触发 ——
        if (stealthForceHideFlareHigh > 0 && currentFlareAmmo >= stealthForceHideFlareHigh) hideHit = true;
        if (stealthForceHideFlareLow > 0 && currentFlareAmmo <= stealthForceHideFlareLow) hideHit = true;
        if (stealthForceRevealFlareHigh > 0 && currentFlareAmmo >= stealthForceRevealFlareHigh) revealHit = true;
        if (stealthForceRevealFlareLow > 0 && currentFlareAmmo <= stealthForceRevealFlareLow) revealHit = true;
        // —— StealthBullet：高阈≥触发 / 低阈≤触发，中间不触发 ——
        if (stealthForceHideStealthBulletHigh > 0 && currentStealthBullets >= stealthForceHideStealthBulletHigh) hideHit = true;
        if (stealthForceHideStealthBulletLow > 0 && currentStealthBullets <= stealthForceHideStealthBulletLow) hideHit = true;
        if (stealthForceRevealStealthBulletHigh > 0 && currentStealthBullets >= stealthForceRevealStealthBulletHigh) revealHit = true;
        if (stealthForceRevealStealthBulletLow > 0 && currentStealthBullets <= stealthForceRevealStealthBulletLow) revealHit = true;
        // —— RevealBullet：高阈≥触发 / 低阈≤触发，中间不触发 ——
        if (stealthForceHideRevealBulletHigh > 0 && currentRevealBullets >= stealthForceHideRevealBulletHigh) hideHit = true;
        if (stealthForceHideRevealBulletLow > 0 && currentRevealBullets <= stealthForceHideRevealBulletLow) hideHit = true;
        if (stealthForceRevealRevealBulletHigh > 0 && currentRevealBullets >= stealthForceRevealRevealBulletHigh) revealHit = true;
        if (stealthForceRevealRevealBulletLow > 0 && currentRevealBullets <= stealthForceRevealRevealBulletLow) revealHit = true;
        // —— CamoBullet：高阈≥触发 / 低阈≤触发，中间不触发 ——
        if (stealthForceHideCamoBulletHigh > 0 && currentCamoBullets >= stealthForceHideCamoBulletHigh) hideHit = true;
        if (stealthForceHideCamoBulletLow > 0 && currentCamoBullets <= stealthForceHideCamoBulletLow) hideHit = true;
        if (stealthForceRevealCamoBulletHigh > 0 && currentCamoBullets >= stealthForceRevealCamoBulletHigh) revealHit = true;
        if (stealthForceRevealCamoBulletLow > 0 && currentCamoBullets <= stealthForceRevealCamoBulletLow) revealHit = true;

        // ✅ 分方向一次性：该方向已触发过则不再生效（此前这两个开关未接线）
        if (stealthForceHideOneShot && forceHideTriggered) hideHit = false;
        if (stealthForceRevealOneShot && forceRevealTriggered) revealHit = false;

        // ✅ 冲突裁决：同时命中只执行一个（此前两者都执行，后执行的显形静默覆盖）
        if (hideHit && revealHit)
        {
            if (stealthThresholdRevealWins) hideHit = false; else revealHit = false;
        }
        if (hideHit) { ForceStealthNow(); forceHideTriggered = true; }
        if (revealHit) { RevealStealth(); forceRevealTriggered = true; }
        bool anyActive = hideHit || revealHit;

        // ✅ 模式结算
        if (stealthForceOneShot)
        {
            if (anyActive) stealthForceLatched = true;   // 一次性：触发即锁存，永不重判
        }
        else
        {
            stealthThresholdLocked = anyActive;   // 持续模式：写运行时阈值锁，不再覆盖编辑器手动锁
        }
    }

    // 感知：viewerTeam 是否"看见"该单位（视觉层）
    public static bool IsVisibleTo(Infantry unit, string viewerTeam, System.Collections.Generic.List<Grids> allGrids)
    {
        if (unit == null) return false;
        if (unit.team == viewerTeam) return true;
        if (!unit.isStealthed || unit.stealthFlareRevealed) return true;   // 没隐身/被照明弹照出的照常
        if (unit.stealthVisibleOnTile && unit.grid != null)
        {
            foreach (var u in unit.grid.infantries)
                if (u != null && u != unit && u.team == viewerTeam) return true;   // 同格单位看得见
            var fac = unit.grid.city;   // ✅ 脚下建筑：所属方看得见（AW3规则，接线完成）
            if (fac != null && fac.facilityTeam == viewerTeam) return true;
        }
        if (unit.stealthVisibleWhenAdjacent && allGrids != null && unit.grid != null)
            foreach (var g in allGrids)
            {
                if (g == null || g == unit.grid) continue;
                int dist = Math.Abs(g.GridIndex.X - unit.grid.GridIndex.X) + Math.Abs(g.GridIndex.Y - unit.grid.GridIndex.Y);
                if (dist < unit.stealthVisibleMinRange || dist > unit.stealthVisibleMaxRange) continue;
                foreach (var u in g.infantries)
                    if (u != null && u.team == viewerTeam) return true;
            }
        if (unit.stealthRevealOnFlare && unit.IsFlareLit()) return true;   // ✅ 照明弹照出（实时查询，离开亮格自动恢复）
        return false;
    }

    // 攻击准入：attacker 能否打 target（隐身白名单）
    public static bool CanTargetStealth(Infantry attacker, Infantry target)
    {
        if (target == null || !target.isStealthed) return true;
        if (target.stealthRevealOnFlare && target.IsFlareLit()) return true;   // ✅ 照明弹照出：可攻击
        if (target.stealthAttackableBy == null || target.stealthAttackableBy.Length == 0) return false;
        return System.Array.IndexOf(target.stealthAttackableBy, attacker.UnitTypeId) >= 0;
    }

    // 现形三触发器之"受伤"（在战斗伤害点调用）
    // ✅ 广义受伤现形：监听血量下降（覆盖自爆/兵器/激光/死光/地格伤害/修理扣血等所有来源）
    private int damageRevealLastHp = -1;
    public void SyncDamageRevealTracker() => damageRevealLastHp = health;

    public static void ApplyStealthRevealOnDamage(Infantry damaged)
    {
        if (damaged == null || !damaged.stealthRevealOnDamage) return;
        // 阈值模式：血量已在此调用前扣减，≤阈值才现形；非阈值模式受任何伤即现形
        if (!damaged.stealthRevealOnDamageUseThreshold || damaged.health <= damaged.stealthRevealOnDamageHpThreshold)
            damaged.RevealStealth();
    }

    // ✅ 兵器攻击准入：双向名单判定（攻击方排除表 + 兵器拒绝表），空表=不限制
    public bool CanTargetWeapon(Weapon w)
    {
        if (w == null || w.isDestroyed) return false;
        string myName = UnitTypeId;
        if (w.excludedAttackerTypes != null && w.excludedAttackerTypes.Length > 0
            && System.Array.IndexOf(w.excludedAttackerTypes, myName) >= 0)
            return false;
        if (excludedWeaponTargets != null && excludedWeaponTargets.Length > 0)
        {
            Type t = w.GetType();
            while (t != null && t != typeof(object))
            {
                if (System.Array.IndexOf(excludedWeaponTargets, t.Name) >= 0) return false;
                t = t.BaseType;
            }
        }
        return true;
    }

    public virtual int CalculateDamageAgainstWeapon(Weapon target, WeaponType weaponType)
{
    if (!CanTargetWeapon(target)) return 0;   // ✅ 兵器攻击准入闸门
    // 兵器没有类型名在攻防表中，使用基础攻击力的百分比
    float healthPercent = (float)health / maxHealth;
    
    int baseDamage = 0;
    if (weaponType == WeaponType.Primary && hasPrimaryWeapon)
    {
        // 使用主武器基础攻击力
        baseDamage = baseAttack;
    }
    else if (weaponType == WeaponType.Secondary && hasSecondaryWeapon)
    {
        // 使用副武器攻击力
        baseDamage = secondaryAttack;
    }
    
    // 应用血量系数
    int actualDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * healthPercent));
    
    // 兵器没有防御力，直接扣血
    return actualDamage;
}
    public virtual void OnWaitSelected()
    {
        StopBreath();
        isMoved = true;
        isAttacked = true;
        state = UnitState.Acted;
        movePoints = defaultMovePoints;
        FreezeToIdleDark();
        originalGrid = null;

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.ClearSelectedInfantry();
    }

    public virtual void OnMoveSelected()
    {
        if (isStealthed && !canMoveWhileStealthed) return; // ✅ 隐身行为锁
        if (consumeFuel && fuel <= 0) return;
        EnsureGridBound();
        ShowAnimState("move");
        StopBreath();
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm == null)
        {
            return;
        }
        
        // ✅ 清理之前的状态
        gm.gridManager.CloseRange();
        gm.gridManager.HideAttackRange();
        gm.gridManager.ClearWeaponRange();
        
        gm.gridManager.ShowMoveRange(this);
    }

    public virtual void OnAttackSelected()
    {
        if (attackType == AttackType.NoAttack)
            return;

        // ✅ 修复：检查弹药和移动后攻击权限
        if (hasPrimaryWeapon && !CanUsePrimaryWeapon() && !hasSecondaryWeapon)
        {
            return;
        }
        if (!canAttackAfterMoving && state == UnitState.Moved)
        {
            return;
        }

        EnsureGridBound();
        ShowAnimState("attack");
        StopBreath();
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.gridManager.ShowAttackRange(this);
    }

    public void AttackTarget(Node target)
    {
        if (target is Infantry infantry)
        {
            Attack(infantry);
        }
        else if (target is Weapon weapon)
        {
            // ✅ 修复1：使用统一的武器检查
            if (!CanUsePrimaryWeapon() && !CanUseSecondaryWeapon()) return;
            if (!canAttackAfterMoving && state == UnitState.Moved) return;
            if (state == UnitState.Acted) return;

            // ✅ 修复2：选择武器（虽然兵器没有类型名，但可以用默认伤害）
            WeaponType selectedWeapon = WeaponType.Primary;
            if (hasPrimaryWeapon && CanUsePrimaryWeapon())
            {
                selectedWeapon = WeaponType.Primary;
                ConsumePrimaryAmmo();
            }
            else if (hasSecondaryWeapon && CanUseSecondaryWeapon())
            {
                selectedWeapon = WeaponType.Secondary;
            }
            else
            {
                return; // 没有可用武器
            }

            isAttacked = true;
            isMoved = true;
            originalGrid = null;

            // ✅ 修复3：使用攻防表计算伤害（兵器没有对应条目，需要处理）
                        if (!CanTargetWeapon(weapon)) return;   // ✅ 准入拒绝
            int damage = CalculateDamageAgainstWeapon(weapon, selectedWeapon);
            weapon.TakeDamage(damage);

            state = UnitState.Acted;
            FreezeToIdleDark();

            // 清理UI
            var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
            gm?.gridManager?.HideAttackRange();
            gm?.ClearSelectedInfantry();
        }
    }

    // ✅ 修改 InputClick - 移动后点击任何单位都触发回退
// Infantry.cs - 修改 InputClick 方法
public virtual void InputClick(Node viewport, InputEvent inputs, int shape_index)
{
    // ✅ 被迷雾/隐身隐藏的单位不响应点击（Area2D 拾取不受 Visible 影响，否则会暴露位置/信息）
    if (!IsVisibleInTree()) return;


    if (inputs is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
    {
        if (mouseEvent.ButtonIndex == MouseButton.Left)
        {
            var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
            if (gm == null) return;

            // ✅ 关键修复：如果当前有其他单位正在移动范围模式下，且该单位不是我自己
            // 说明这是移动范围点击，不是单位点击，忽略此次输入
            if (gm.selectedInfantry != null && 
                gm.selectedInfantry != this && 
                gm.gridManager != null &&
                gm.gridManager.moveRange.Count > 0)
            {
                return;
            }


            if (this.state == UnitState.Moved && this.originalGrid != null)
            {
                
                // 检查点击的是否是其他可移动单位
                if (this.overlapType == UnitOverlapType.Overlapping && 
                    grid != null && 
                    grid.infantries.Count > 1)
                {
                    var otherUnit = grid.infantries.FirstOrDefault(u => u != this && u != null && IsInstanceValid(u) && !u.isMoved);
                    if (otherUnit != null)
                    {
                        var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
                        actionMenu?.ShowMoveChoiceDialog(this, otherUnit, grid);
                        return;
                    }
                }
                
                // gm?.RollbackMove(); // 不自动回退，让用户选择
                return;
            }


                        if (this.state == UnitState.Moved && this.originalGrid != null)
            {

            }
                    if (currentCaptureProgress > 0 && capturingGrid == grid && !isMoved)
        {
            gm?.OnSelectPiece(this);
            return;
        }

            // 检查是否是点击其他已移动的单位
if (gm.selectedInfantry != null && gm.selectedInfantry != this && 
    gm.selectedInfantry.state == UnitState.Moved && 
    gm.selectedInfantry.originalGrid != null)
{
    if (gm.isSelectingAttackTarget && this.team != gm.selectedInfantry.team)
                    {
                        return;
                    }
    else
    {
        gm.RollbackMove();
        return;
    }
}


if (Input.IsKeyPressed(Key.Ctrl))
{
    gm.gridManager.CloseRange();
    gm.gridManager.HideAttackRange();
    gm.gridManager.ClearWeaponRange();
    var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
    actionMenu?.Hide();
    ShowUnitInfo();
    GetViewport().SetInputAsHandled();
    return;
}


            // ✅ 究极自由：同格多单位选择逻辑
            if (grid != null && grid.infantries.Count > 1)
            {
                string currentTeam = gm.turnPhase == 1 ? "Player1" : "Player2";

                // 优先选择：未移动的、可搭载的单位（如果当前单位需要搭载）
                var transportUnits = grid.infantries
                    .Where(u => u.team == currentTeam 
                        && u.canTransportUnits 
                        && u.maxTransportCapacity > 0
                        && u.transportedUnits.Count < u.maxTransportCapacity
                        && IsInstanceValid(u))
                    .ToList();

                // 如果有可搭载单位且当前单位可以被搭载，显示搭载菜单
                if (transportUnits.Count > 0 && !this.isMoved && this.state != UnitState.Acted)
                {
                    var transport = transportUnits[0];
                    if (transport.CanTransportUnit(this))
                    {
                        var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
                        actionMenu?.ShowTransportMenu(transport, afterMove: false);
                        return;
                    }
                }

                // 否则按原逻辑选择
                var myTeamUnits = grid.infantries
                    .Where(u => u.team == currentTeam && !u.isMoved && IsInstanceValid(u))
                    .OrderByDescending(u => u.health)
                    .ToList();

                if (myTeamUnits.Count > 0)
                {
                    var targetUnit = myTeamUnits[0];
                    if (targetUnit != this)
                    {
                        gm.OnSelectPiece(targetUnit);
                        return;
                    }
                }
            }

            bool isMyTurn = gm.IsTurnPhaseValid(team);

            if (isMyTurn && !isMoved)
            {
                // 取消之前的选中
                if (gm.selectedInfantry != null && gm.selectedInfantry != this)
                {
                    var prevUnit = gm.selectedInfantry;
                    gm.gridManager.CloseRange();
                    gm.gridManager.HideAttackRange();
                    var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
                    actionMenu?.Hide();

                    if (prevUnit.state == UnitState.Moved && 
                        prevUnit.overlapType != UnitOverlapType.Oozium &&
                        prevUnit.originalGrid != null)
                    {
                        gm.RollbackMove();
                        return;  // ✅ 添加 return，避免继续执行选中逻辑
                    }
                    else
                    {
                        gm.ClearSelectedInfantry();
                    }
                }

                gm.OnSelectPiece(this);
            }
        }
    }
}
 
    protected void ShowUnitInfo()
    {
        var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
        if (actionMenu == null) return;

        var originalUnit = actionMenu.currentUnit;
        actionMenu.currentUnit = this;
        actionMenu.OnInfoPressed();
    }

    public virtual bool IsArmoredTarget(Infantry target)
    {
        return false;
    }
public virtual (int damage, string info) CalculateDamagePreview(Infantry target)
{
    WeaponType weapon = SelectWeaponByMatrix(target);
    int damage = CalculateFinalDamage(target, weapon);
    int baseVal = weapon == WeaponType.Primary ? GetPrimaryDamageFromMatrix(target) : GetSecondaryDamageFromMatrix(target);
    
    string weaponName = weapon == WeaponType.Primary ? TranslateGroup.T("主") : TranslateGroup.T("副");
    string info = damage > 0 
        ? $"{weaponName}武:{damage}(基础{baseVal})" 
        : TranslateGroup.T("无法攻击");
    
    return (damage, info);
}

public virtual (int damage, string info) CalculateCounterPreview(Infantry attacker)
{
    if (attackType == AttackType.NoAttack || health <= 0)
        return (0, TranslateGroup.T("无法反击"));
    
    WeaponType counterWeapon = SelectWeaponByMatrix(attacker);
    int damage = CalculateFinalDamage(attacker, counterWeapon, isCounter: true);
    int baseVal = counterWeapon == WeaponType.Primary ? GetPrimaryDamageFromMatrix(attacker) : GetSecondaryDamageFromMatrix(attacker);
    
    return (damage, $"反击({counterWeapon}):{damage}");
}

public virtual void Attack(Infantry target)
{
        // ✅ 隐身攻击门：白名单外的单位打不了隐身目标（所有攻击路径的唯一闸门）
        if (!CanTargetStealth(this, target)) return;
        if (isStealthed && !canAttackWhileStealthed) return;   // ✅ 隐身行为锁：攻击
    if (attackType == AttackType.NoAttack) return;
    if (isAttacked) return;
    if (!canAttackAfterMoving && state == UnitState.Moved) return;
    if (state == UnitState.Acted) return;
    
    // ✅ 禁止隔空伤害：必须确认目标在攻击范围内
    if (!IsTargetInAttackRange(target))
    {
        return;
    }
    
    // 选择武器（新：攻防表系统）
    WeaponType weapon = SelectWeaponByMatrix(target);
    if (weapon == WeaponType.None) return;
    
    // 消耗弹药（仅限主武器）
    if (weapon == WeaponType.Primary)
        ConsumePrimaryAmmo();
    
    isAttacked = true;
    isMoved = true;
    originalGrid = null;

    // 计算伤害（新：攻防表公式）
    int attackDamage = CalculateFinalDamage(target, weapon);
    int counterDamage = 0;
    
    // 反击
    bool targetWillDie = (target.health - attackDamage) <= 0;
    bool canTargetCounter = !targetWillDie 
        && target.attackType != AttackType.NoAttack
        && target.IsTargetInCounterRange(this)
        && TeamHelper.CanCounterAttack(target.team);
    
    // 旧行为（可选）：按挨打前血量算反击
    if (canTargetCounter && target.counterUsePreDamageHp)
    {
        WeaponType counterWeapon = target.SelectWeaponByMatrix(this);
        counterDamage = target.CalculateFinalDamage(this, counterWeapon, isCounter: true);
    }
    
    // 应用伤害
    target.health -= attackDamage;
        ApplyStealthRevealOnDamage(target);   // ✅ 受伤现形（含阈值模式）
        target.CheckStealthThresholds();   // ✅ 受伤后阈值矩阵重判
        target.SyncDamageRevealTracker();     // ✅ 已按"单位攻击"处理过，广义监听不重复触发

    // ✅ AW 原作：反击按挨打后的血量计算（先手优势）
    if (canTargetCounter && !target.counterUsePreDamageHp)
    {
        WeaponType counterWeapon = target.SelectWeaponByMatrix(this);
        counterDamage = target.CalculateFinalDamage(this, counterWeapon, isCounter: true);
    }
        if (stealthRevealOnAttack) RevealStealth();   // ✅ 攻击后现形（先于反击结算：现形即暴露给反击）
        if (!CanTargetStealth(target, this)) counterDamage = 0;   // ✅ 隐身反击门：防守方打不了隐身攻击者则不能反击（AW3规则）
    this.health -= counterDamage;
        if (counterDamage > 0) ApplyStealthRevealOnDamage(this);   // ✅ 反击也是单位攻击：攻击方受伤同样走受伤现形
        CheckStealthThresholds();   // ✅ 反击掉血后阈值矩阵重判
        SyncDamageRevealTracker();
    
    target.UpdateHpLabel();
    this.UpdateHpLabel();
    
    // 日志
    string weaponName = weapon == WeaponType.Primary ? TranslateGroup.T("主") : TranslateGroup.T("副");
    int baseVal = weapon == WeaponType.Primary ? GetPrimaryDamageFromMatrix(target) : GetSecondaryDamageFromMatrix(target);

    // ========== 死亡处理 ==========
    if (target.health <= 0)
    {
        target.OnDestroyed();
        if (target.grid != null)
        {
            target.grid.infantries.Remove(target);
            if (target.grid.infantry == target)
                target.grid.infantry = target.grid.infantries.Count > 0 ? target.grid.infantries[0] : null;
            target.grid = null;
        }
        target.CallDeferred("queue_free");
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.RemovePiece(target);
    }
    
    if (this.health <= 0)
    {
        this.OnDestroyed();
        if (this.grid != null)
        {
            this.grid.infantries.Remove(this);
            if (this.grid.infantry == this)
                this.grid.infantry = this.grid.infantries.Count > 0 ? this.grid.infantries[0] : null;
            this.grid = null;
        }
        this.CallDeferred("queue_free");
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.RemovePiece(this);
        return;
    }
    
    state = UnitState.Acted;
    FreezeToIdleDark();
}


public virtual bool IsTargetInCounterRange(Infantry attacker)
{

    if (attacker.cannotCounterWhenAttacked)
        return false;
    
    if (!canCounterWhenDefending)
        return false;
    
    if (!canCounterAtRange)
    {
        if (this.grid == null || attacker.grid == null) return false;
        int distance = Mathf.Abs(this.grid.GridIndex.X - attacker.grid.GridIndex.X) 
                     + Mathf.Abs(this.grid.GridIndex.Y - attacker.grid.GridIndex.Y);
        return distance == 1;
    }
    

    return this.IsTargetInAttackRange(attacker);
}
    public virtual bool IsTargetInAttackRange(Infantry target)
    {
        if (target?.grid == null || this.grid == null) return false;
        
        int distance = Mathf.Abs(target.grid.GridIndex.X - this.grid.GridIndex.X) 
                     + Mathf.Abs(target.grid.GridIndex.Y - this.grid.GridIndex.Y);
        
        if (useMinMaxAttackRange)
            return distance >= minAttackRange && distance <= maxAttackRange;
        else
            return distance <= attackRange;
    }
    




public virtual bool CanAttackAfterMove()
    {
        if (state == UnitState.Idle) return true;
        return canAttackAfterMoving;
    }




public virtual void ReloadAmmo()
{
    if (hasPrimaryWeapon && primaryHasLimitedAmmo && maxPrimaryAmmo < 99)
    {
        currentPrimaryAmmo = maxPrimaryAmmo;
    }
}

    // ========== ✅ 究极自由：搭载系统方法 ==========

    /// <summary>
    /// 检查是否可以搭载指定单位
    /// </summary>
    /// <summary>
    /// 检查是否可以搭载指定单位（模块化：通过Inspector配置，零代码适配新单位）
    /// </summary>
    public virtual bool CanTransportUnit(Infantry unit)
    {
        if (!canTransportUnits) return false;
        if (transportedUnits.Count >= maxTransportCapacity) return false;
        if (unit == this) return false;
        if (unit.team != this.team) return false;

        // 1. 按类型名过滤（精确匹配类名，如 "Infantry", "Mech"）
        string unitType = unit.UnitTypeId;
        if (canTransportUnitTypes != null && canTransportUnitTypes.Count > 0)
        {
            if (!canTransportUnitTypes.Contains(unitType)) return false;
        }

        // 2. 按单位类别过滤（如 Infantry, Tank, Mech 等）
        if (canTransportUnitCategories != null && canTransportUnitCategories.Count > 0)
        {
            var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
            if (gm != null && gm.unitCategories.TryGetValue(unit, out var category))
            {
                if (!canTransportUnitCategories.Contains(category)) return false;
            }
        }

        // 3. 按属性标志过滤（零代码扩展新单位）
        if (transportExcludeAirUnits && unit.IsAirUnit) return false;
        if (transportExcludeArmored && unit.isArmoredUnit) return false;
        if (transportRequireCanCapture && unit.captureAbility != CaptureAbility.CanCapture) return false;
        if (transportMaxMovePoints > 0 && unit.defaultMovePoints > transportMaxMovePoints) return false;

        return true;
    }

    /// <summary>
    /// 搭载单位
    /// </summary>
    public virtual bool TransportUnit(Infantry unit)
    {
        if (!CanTransportUnit(unit)) return false;

        transportedUnits.Add(unit);

        unit.Hide();
        unit.ProcessMode = ProcessModeEnum.Disabled;

        if (unit.grid != null)
        {
            unit.grid.infantries.Remove(unit);
            if (unit.grid.infantry == unit)
                unit.grid.infantry = unit.grid.infantries.Count > 0 ? unit.grid.infantries[0] : null;
        }

        return true;
    }

    /// <summary>
    /// 卸下单位到指定格子
    /// </summary>
    public virtual bool UntransportUnit(Infantry unit, Grids targetGrid)
    {
        if (!transportedUnits.Contains(unit)) return false;
        if (targetGrid == null) return false;
        if (targetGrid.gridType == GridType.METEORITE) return false;

        // 检查目标格子是否有阻挡
        var blockingUnit = targetGrid.infantries.FirstOrDefault(u => 
            u != null && IsInstanceValid(u) && 
            u.overlapType == UnitOverlapType.NonOverlapping);
        if (blockingUnit != null) return false;

        transportedUnits.Remove(unit);

        unit.Show();
        unit.ProcessMode = ProcessModeEnum.Inherit;

        unit.Position = targetGrid.Position;
        unit.grid = targetGrid;

        if (!targetGrid.infantries.Contains(unit))
            targetGrid.infantries.Add(unit);
        if (targetGrid.infantry == null)
            targetGrid.infantry = unit;

        // 卸下后本回合不能移动
        unit.isMoved = true;
        unit.isAttacked = true;
        unit.state = UnitState.Acted;
        unit.SetWaitVisual(true);

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.unitManager?.BindUnitToGrid(unit, true);

        return true;
    }

// ========== ✅ 究极自由：通用装载方法（从 APC 迁移） ==========
public virtual bool LoadUnit(Infantry unit)
{
        if (isStealthed && !canLoadUnloadWhileStealthed) return false; // ✅ 隐身行为锁
    if (!CanTransportUnit(unit)) return false;
    
    // ✅ 彻底停止所有视觉组件的动画残留（sprite + animSprite + 嵌套子节点）
    StopAllVisuals(unit);
    
    transportedUnits.Add(unit);
    unit.isTransported = true;
    
    unit.Hide();
    unit.ProcessMode = ProcessModeEnum.Disabled;
    
    if (unit.grid != null)
    {
        unit.grid.infantries.Remove(unit);
        if (unit.grid.infantry == unit)
            unit.grid.infantry = unit.grid.infantries.Count > 0 ? unit.grid.infantries[0] : null;
        unit.grid = null; // ✅ 彻底断开与格子的关联
    }
    
    UpdateLoadedIcon();
    
    // ✅ 装载后刷新迷雾，确保被装载单位正确隐藏
    var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
    gm?.fogOfWarManager?.OnUnitMoved();
    
    return true;
}

    // ✅ 彻底停止并重置单位的所有视觉组件（防止装载后残留动画）
    private void StopAllVisuals(Infantry unit)
    {
        unit.breathTween?.Kill();
        unit.breathTween = null;
        
        void ResetNode(Node node)
        {
            if (node is Sprite2D sp && IsInstanceValid(sp))
            {
                sp.Scale = Vector2.One;
                sp.Modulate = Colors.White;
                sp.Hide();
            }
            else if (node is AnimatedSprite2D anim && IsInstanceValid(anim))
            {
                anim.Scale = Vector2.One;
                anim.Modulate = Colors.White;
                anim.Stop();
                anim.Hide();
            }
            else if (node is Label lbl && IsInstanceValid(lbl))
            {
                lbl.Hide();
            }
            foreach (var child in node.GetChildren())
                ResetNode(child);
        }
        
        ResetNode(unit);
        unit.Hide();
    }
    
    // ✅ 恢复单位的所有视觉组件（卸载后调用）
    private void RestoreAllVisuals(Infantry unit)
    {
        void RestoreNode(Node node)
        {
            if (node is Sprite2D sp && IsInstanceValid(sp))
            {
                sp.Modulate = Colors.White;
            }
            else if (node is AnimatedSprite2D anim && IsInstanceValid(anim))
            {
                anim.Modulate = Colors.White;
            }
            foreach (var child in node.GetChildren())
                RestoreNode(child);
        }
        
        RestoreNode(unit);
        unit.SetWaitVisual(false);
        unit.StartBreath();
    }

// ========== ✅ 究极自由：通用卸载方法（从 APC 迁移） ==========
public virtual bool UnloadUnit(Infantry unit, Grids targetGrid)
{
        if (isStealthed && !canLoadUnloadWhileStealthed) return false; // ✅ 隐身行为锁
    if (!transportedUnits.Contains(unit)) return false;
    if (targetGrid == null) return false;
    if (targetGrid.gridType == GridType.METEORITE) return false;

    var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;

    // ✅ P-1 公共运输单位：只能卸载当前势力的单位
    if (this.team == TeamHelper.Player)
    {
        string currentTurnTeam = TeamHelper.GetPhaseTeamName(gm?.turnPhase ?? 1);
        if (unit.team != currentTurnTeam)
            return false;
    }

    
    var blockingUnit = targetGrid.infantries.FirstOrDefault(u => 
        u != null && IsInstanceValid(u) && 
        u.overlapType == UnitOverlapType.NonOverlapping);
    if (blockingUnit != null) return false;
    
    transportedUnits.Remove(unit);
    unit.isTransported = false;
    
    unit.Show();
    unit.ProcessMode = ProcessModeEnum.Inherit;
    
    // ✅ 恢复被装载单位的所有视觉组件
    RestoreAllVisuals(unit);
    
    unit.Position = targetGrid.Position;
    unit.grid = targetGrid;
    
    if (!targetGrid.infantries.Contains(unit))
        targetGrid.infantries.Add(unit);
    if (targetGrid.infantry == null)
        targetGrid.infantry = unit;
    
    // 卸下后本回合不能移动
    unit.isMoved = true;
    unit.isAttacked = true;
    unit.state = UnitState.Acted;
    unit.SetWaitVisual(true);
    
    unitsCannotMove[unit] = 1;
    
    gm?.unitManager?.BindUnitToGrid(unit, true);
    gm?.fogOfWarManager?.OnUnitMoved(); // ✅ 卸载后刷新迷雾
    
    UpdateLoadedIcon();
    
    return true;
}

// ========== ✅ 究极自由：检查是否可以卸载到格子（从 APC 迁移） ==========
public virtual bool CanUnloadToGrid(Grids targetGrid)
{
    if (transportedUnits.Count == 0) return false;
    if (targetGrid == null) return false;
    if (targetGrid.gridType == GridType.METEORITE) return false;
    
    var blockingUnit = targetGrid.infantries.FirstOrDefault(u => 
        u != null && IsInstanceValid(u) && 
        u.overlapType == UnitOverlapType.NonOverlapping);
    
    return blockingUnit == null;
}

// ========== ✅ 究极自由：获取第一个已装载单位（从 APC 迁移） ==========
public virtual Infantry GetFirstLoadedUnit()
{
    return transportedUnits.FirstOrDefault(u => u != null && IsInstanceValid(u));
}

// ========== ✅ 究极自由：补给范围计算（从 APC 迁移） ==========
public virtual List<Grids> CalculateSupplyRange()
{
    var result = new List<Grids>();
    var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
    if (gm?.gridManager == null || grid == null) return result;

    var allRange = gm.gridManager.FindRange(grid, maxSupplyRange, false);

    if (minSupplyRange > 1)
    {
        var blindZone = gm.gridManager.FindRange(grid, minSupplyRange - 1, false);
        allRange = allRange.Where(g => !blindZone.Contains(g)).ToList();
    }

    allRange = allRange.Where(g => g != grid).ToList();

    return allRange;
}

// ========== ✅ 究极自由：获取补给范围内的友方单位（从 APC 迁移） ==========
public virtual List<Infantry> GetSupplyRangeUnits()
{
    var result = new List<Infantry>();
    var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
    if (gm?.gridManager == null) return result;
    
    var supplyRange = CalculateSupplyRange();
    
    foreach (var g in supplyRange)
    {
        if (g == null) continue;
        
        foreach (var unit in g.infantries)
        {
            if (unit != null && IsInstanceValid(unit) && unit.team == this.team && unit != this)
            {
                result.Add(unit);
            }
        }
    }
    
    return result;
}

// ========== ✅ 究极自由：补给黑炮弹药（从 APC 迁移） ==========
public virtual bool ResupplyBlackCannon(Weapon weapon)
{
    return ResupplyWeapon(weapon);
}

// ========== ✅ 究极自由：获取范围内需要弹药补给的黑炮（从 APC 迁移） ==========
public virtual List<Weapon> GetBlackCannonsNeedingAmmo()
{
    return GetWeaponsNeedingAmmo();
}

// ========== ✅ 究极自由：执行补给（从 APC 迁移） ==========
public virtual void PerformSupply()
{
    if (!canSupplyUnits) return;  // 没有补给能力的单位不能执行补给
{
    bool anySupplied = false;

    // 1. 补给单位
    var targetUnits = GetSupplyRangeUnits();
    foreach (var unit in targetUnits)
    {
        bool supplied = false;
        string supplyText = "";
        Color supplyColor = new Color(0.2f, 0.9f, 0.3f);

        // 补给弹药
        if (unit.hasPrimaryWeapon && unit.primaryHasLimitedAmmo && 
            unit.currentPrimaryAmmo < unit.maxPrimaryAmmo)
        {
            unit.currentPrimaryAmmo = unit.maxPrimaryAmmo;
            supplied = true;
            supplyText = "Ammo+";
        }

        // ✅ 补给照明弹（通用：任何开启照明模块的单位）
        if (unit.canIlluminate && unit.currentFlareAmmo < unit.maxFlareAmmo)
        {
            unit.currentFlareAmmo = unit.maxFlareAmmo;
            if (!supplied)
            {
                supplyText = "Flare+";
                supplyColor = new Color(0.9f, 0.9f, 0.2f);
            }
            else
            {
                supplyText += " Flare+";
            }
            supplied = true;
        }
        if (unit is Flare flareUnit && flareUnit.canIlluminate && 
            flareUnit.currentFlareAmmo < flareUnit.maxFlareAmmo)
        {
            flareUnit.currentFlareAmmo = flareUnit.maxFlareAmmo;
            if (!supplied)
            {
                supplyText = "Flare+";
                supplyColor = new Color(0.9f, 0.9f, 0.2f);
            }
            else
            {
                supplyText += " Flare+";
            }
            supplied = true;
        }

        // 补给燃料
        if (unit.consumeFuel && unit.fuel < unit.maxFuel)
        {
            unit.fuel = unit.maxFuel;
            if (!supplied)
            {
                supplyText = "Fuel+";
                supplyColor = new Color(0.9f, 0.7f, 0.2f);
            }
            else
            {
                supplyText = "Ammo+ Fuel+";
            }
            supplied = true;
        }

        if (supplied)
        {
            anySupplied = true;
            unit.UpdateHpLabel();
            ShowSupplyEffect(unit, supplyText, supplyColor);
        }
    }

    var weaponsNeedingAmmo = GetWeaponsNeedingAmmo();
    foreach (var weapon in weaponsNeedingAmmo)
    {
        if (ResupplyWeapon(weapon))
        {
            anySupplied = true;
        }
    }

    if (anySupplied)
    {
        ShowTransportSupplyEffect();
    }
}
}
private void ShowSupplyEffect(Infantry unit, string text, Color color)
{
    var effect = new SupplyEffect();
    if (grid != null)
    {
        var relativePos = unit.GlobalPosition - this.GlobalPosition;
        effect.Setup(relativePos, text, color);
        AddChild(effect);
    }
}

private void ShowTransportSupplyEffect()
{
    var effect = new GridSupplyEffect();
    AddChild(effect);
}

// ========== ✅ 究极自由：补给按钮回调（从 APC 迁移） ==========
public virtual void OnSupplySelected()
{
    PerformSupply();
    
    isMoved = true;
    isAttacked = true;
    hasActed = true;
    state = UnitState.Acted;
    originalGrid = null;
    
    SetWaitVisual(true);
    
    var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
    gm?.ClearSelectedInfantry();
}

// ========== ✅ 究极自由：回合开始自动补给（从 APC 迁移） ==========
public virtual void OnTurnStartAutoSupply()
{
    PerformSupply();
}

// ========== ✅ 究极自由：loadedIcon 视觉系统（从 APC 迁移） ==========
private void SetupLoadedIcon()
{
    if (loadedIcon == null)
    {
        return;
    }
    
    var spriteFrames = GD.Load<SpriteFrames>("res://asscets/AnimatedSprite/loaded.tres");
    if (spriteFrames == null)
    {
        return;
    }
    
    loadedIcon.SpriteFrames = spriteFrames;
    
    string[] animNames = loadedIcon.SpriteFrames.GetAnimationNames();
    int count = animNames.Length;
    
    
    if (count > 0)
    {
        string targetAnim = "";
        
        for (int i = 0; i < count; i++)
        {
            string animName = animNames[i];
            if (animName == "loaded")
            {
                targetAnim = animName;
                break;
            }
        }
        
        if (string.IsNullOrEmpty(targetAnim))
        {
            targetAnim = animNames[0];
        }
        
        loadedIcon.Animation = targetAnim;
        loadedIcon.Play();
        
    }
    
    loadedIcon.Hide();
    loadedIcon.ZIndex = 200;
    loadedIcon.Modulate = Colors.White;
}

private void StartLoadedIconBlink()
{
    if (loadedIcon == null) return;
    
    StopLoadedIconBlink();
    
    loadedIcon.Show();
    loadedIcon.Visible = true;
    
    if (!loadedIcon.IsPlaying())
    {
        loadedIcon.Play();
    }
    
    var timer = new Timer();
    timer.WaitTime = 1.0f;
    timer.OneShot = false;
    timer.Autostart = true;
    
    timer.Timeout += OnBlinkTimerTimeout;
    
    AddChild(timer);
    
    SetMeta("blink_timer", timer);
}

private void OnBlinkTimerTimeout()
{
    if (loadedIcon != null && IsInstanceValid(loadedIcon))
    {
        loadedIcon.Visible = !loadedIcon.Visible;
    }
}

private void StopLoadedIconBlink()
{
    if (loadedIconTween != null && loadedIconTween.IsValid())
    {
        loadedIconTween.Kill();
        loadedIconTween = null;
    }
    
    if (HasMeta("blink_timer"))
    {
        var timer = GetMeta("blink_timer").As<Timer>();
        if (timer != null && IsInstanceValid(timer))
        {
            timer.Stop();
            timer.QueueFree();
        }
        RemoveMeta("blink_timer");
    }
    
    if (loadedIcon != null)
    {
        loadedIcon.Hide();
        loadedIcon.Modulate = new Color(1, 1, 1, 1);
        loadedIcon.Visible = false;
    }
}

public void UpdateLoadedIcon()
{
    
    if (transportedUnits.Count > 0)
    {
        StartLoadedIconBlink();
    }
    else
    {
        StopLoadedIconBlink();
    }
}

// ========== ✅ 究极自由：燃料图标更新（从 APC/LightTank 迁移） ==========
public virtual void UpdateNoFuelIcon()
{
    if (noFuelIcon == null) return;
    
    if (!consumeFuel)
    {
        noFuelIcon.Visible = false;
    }
    else if (fuel <= 0)
    {
        noFuelIcon.Visible = true;
        if (noFuelIcon.SpriteFrames != null && noFuelIcon.SpriteFrames.HasAnimation("nofuel"))
        {
            noFuelIcon.Play("nofuel");
        }
    }
    else if (fuel <= lowFuelThreshold)
    {
        noFuelIcon.Visible = (Time.GetTicksMsec() / 500) % 2 == 0;
        if (noFuelIcon.SpriteFrames != null && noFuelIcon.SpriteFrames.HasAnimation("lowfuel"))
        {
            noFuelIcon.Play("lowfuel");
        }
    }
    else
    {
        noFuelIcon.Visible = false;
    }
}

// ========== ✅ 究极自由：unitsCannotMove 静态管理（从 APC 迁移） ==========
public static void UpdateUnitsCannotMove()
{
    var unitsToRemove = new List<Infantry>();
    
    foreach (var kvp in unitsCannotMove.ToList())
    {
        var unit = kvp.Key;
        int turnsLeft = kvp.Value;
        
        if (!IsInstanceValid(unit))
        {
            unitsToRemove.Add(unit);
            continue;
        }
        
        turnsLeft--;
        if (turnsLeft <= 0)
        {
            unitsToRemove.Add(unit);
        }
        else
        {
            unitsCannotMove[unit] = turnsLeft;
        }
    }
    
    foreach (var unit in unitsToRemove)
    {
        unitsCannotMove.Remove(unit);
    }
}

public static bool CanUnitMove(Infantry unit)
{
    return !unitsCannotMove.ContainsKey(unit);
}

    /// <summary>
    /// 获取可卸下的相邻格子
    /// </summary>
    public virtual List<Grids> GetAdjacentTransportGrids()
    {
        var result = new List<Grids>();
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.gridManager == null || grid == null) return result;

        Vector2I[] offsets = {
            new Vector2I(0, 1), new Vector2I(-1, 0),
            new Vector2I(0, -1), new Vector2I(1, 0)
        };

        foreach (var offset in offsets)
        {
            var pos = grid.GridIndex + offset;
            if (gm.unitManager.IsValidGrid(pos))
            {
                var targetGrid = gm.gridManager.map[pos.X, pos.Y];
                if (targetGrid != null && targetGrid.gridType != GridType.METEORITE)
                {
                    var blockingUnit = targetGrid.infantries.FirstOrDefault(u =>
                        u != null && IsInstanceValid(u) &&
                        u.overlapType == UnitOverlapType.NonOverlapping);
                    if (blockingUnit == null)
                        result.Add(targetGrid);
                }
            }
        }
        return result;
    }


    public virtual void ShowAnimState(string animName)
    {
        // ✅ 双图层 AnimatedSprite2D：恢复 tone + 同步动画
        if (baseAnimSprite != null && IsInstanceValid(baseAnimSprite))
        {
            if (baseAnimSprite.Material is ShaderMaterial baseMat)
                baseMat.SetShaderParameter("tone", normal);
            else
                baseAnimSprite.Modulate = normal;
            
            string actualAnim = animName;
            if (baseAnimSprite.SpriteFrames != null && !baseAnimSprite.SpriteFrames.HasAnimation(actualAnim))
                actualAnim = GetIdleAnimName();
            if (baseAnimSprite.SpriteFrames != null && baseAnimSprite.SpriteFrames.HasAnimation(actualAnim))
            {
                baseAnimSprite.Play(actualAnim);
                // 同步 Normal 层动画
                if (normalAnimSprite != null && normalAnimSprite.SpriteFrames != null &&
                    normalAnimSprite.SpriteFrames.HasAnimation(actualAnim))
                    normalAnimSprite.Play(actualAnim);
            }
        }
        
        // ✅ 双图层 Sprite2D：恢复 tone
        if (baseSprite != null && IsInstanceValid(baseSprite))
        {
            if (baseSprite.Material is ShaderMaterial baseMat)
                baseMat.SetShaderParameter("tone", normal);
            else
                baseSprite.Modulate = normal;
        }
        
        if (animSprite != null)
        {
            if (idleSprite != null) idleSprite.Hide();
            animSprite.Show();
            // ✅ LUT模式：通过Shader tone设置颜色
            if (animSprite.Material is ShaderMaterial shaderMat)
                shaderMat.SetShaderParameter("tone", normal);
            else
                animSprite.Modulate = normal;
            string actualAnim = animName;
            if (animSprite.SpriteFrames != null && !animSprite.SpriteFrames.HasAnimation(actualAnim))
                actualAnim = GetIdleAnimName();
            if (animSprite.SpriteFrames != null && animSprite.SpriteFrames.HasAnimation(actualAnim))
                animSprite.Play(actualAnim);
        }
        else if (sprite != null)
        {
            sprite.Modulate = normal;
        }
    }

    public virtual void ShowIdleState()
    {
        // ✅ 双图层 AnimatedSprite2D：变暗 + 同步 idle 动画
        if (baseAnimSprite != null && IsInstanceValid(baseAnimSprite))
        {
            if (baseAnimSprite.Material is ShaderMaterial baseMat)
                baseMat.SetShaderParameter("tone", dim);
            else
                baseAnimSprite.Modulate = dim;
            
            string idleAnimName = GetIdleAnimName();
            if (baseAnimSprite.SpriteFrames != null && baseAnimSprite.SpriteFrames.HasAnimation(idleAnimName))
            {
                baseAnimSprite.Animation = idleAnimName;
                baseAnimSprite.Frame = 0;
                baseAnimSprite.Stop();
            }
            else if (baseAnimSprite.SpriteFrames != null && baseAnimSprite.SpriteFrames.HasAnimation("idle"))
            {
                baseAnimSprite.Animation = "idle";
                baseAnimSprite.Frame = 0;
                baseAnimSprite.Stop();
            }
            // 同步 Normal 层
            if (normalAnimSprite != null && normalAnimSprite.SpriteFrames != null)
            {
                if (normalAnimSprite.SpriteFrames.HasAnimation(idleAnimName))
                {
                    normalAnimSprite.Animation = idleAnimName;
                    normalAnimSprite.Frame = 0;
                    normalAnimSprite.Stop();
                }
                else if (normalAnimSprite.SpriteFrames.HasAnimation("idle"))
                {
                    normalAnimSprite.Animation = "idle";
                    normalAnimSprite.Frame = 0;
                    normalAnimSprite.Stop();
                }
                // ✅ Normal 层也变暗（shader tone 只影响自身，不影响子节点）
                if (normalAnimSprite.Material is ShaderMaterial normalMat)
                    normalMat.SetShaderParameter("tone", dim);
                else
                    normalAnimSprite.Modulate = dim;
            }
        }
        
        // ✅ 双图层 Sprite2D：变暗
        if (baseSprite != null && IsInstanceValid(baseSprite))
        {
            if (baseSprite.Material is ShaderMaterial baseMat)
                baseMat.SetShaderParameter("tone", dim);
            else
                baseSprite.Modulate = dim;
            
            // ✅ Normal 层也变暗
            if (normalSprite != null && IsInstanceValid(normalSprite))
            {
                if (normalSprite.Material is ShaderMaterial normalMat)
                    normalMat.SetShaderParameter("tone", dim);
                else
                    normalSprite.Modulate = dim;
            }
        }
        if (animSprite != null)
        {
            if (idleSprite != null) idleSprite.Hide();
            animSprite.Show();
            string idleAnimName = GetIdleAnimName();
            if (animSprite.SpriteFrames != null && animSprite.SpriteFrames.HasAnimation(idleAnimName))
                animSprite.Animation = idleAnimName;
            else if (animSprite.SpriteFrames != null && animSprite.SpriteFrames.HasAnimation("idle"))
                animSprite.Animation = "idle";
            animSprite.Frame = 0;
            animSprite.Stop();
            // ✅ LUT模式：通过Shader tone设置变暗
            if (animSprite.Material is ShaderMaterial shaderMat)
                shaderMat.SetShaderParameter("tone", dim);
            else
                animSprite.Modulate = dim;
        }
        else if (sprite != null)
        {
            if (sprite.Material is ShaderMaterial shaderMat)
                shaderMat.SetShaderParameter("tone", dim);
            else
                sprite.Modulate = dim;
        }
    }

    public virtual void FreezeToIdleDark()
    {
        StopBreath();
        // ✅ 双图层 AnimatedSprite2D：变暗 + 同步 idle
        if (baseAnimSprite != null && IsInstanceValid(baseAnimSprite))
        {
            if (baseAnimSprite.Material is ShaderMaterial baseMat)
                baseMat.SetShaderParameter("tone", dim);
            else
                baseAnimSprite.Modulate = dim;
            
            string idleAnimName = GetIdleAnimName();
            if (baseAnimSprite.SpriteFrames != null && baseAnimSprite.SpriteFrames.HasAnimation(idleAnimName))
            {
                baseAnimSprite.Animation = idleAnimName;
                baseAnimSprite.Frame = 0;
                baseAnimSprite.Stop();
            }
            else if (baseAnimSprite.SpriteFrames != null && baseAnimSprite.SpriteFrames.HasAnimation("idle"))
            {
                baseAnimSprite.Animation = "idle";
                baseAnimSprite.Frame = 0;
                baseAnimSprite.Stop();
            }
            // 同步 Normal 层
            if (normalAnimSprite != null && normalAnimSprite.SpriteFrames != null)
            {
                if (normalAnimSprite.SpriteFrames.HasAnimation(idleAnimName))
                {
                    normalAnimSprite.Animation = idleAnimName;
                    normalAnimSprite.Frame = 0;
                    normalAnimSprite.Stop();
                }
                else if (normalAnimSprite.SpriteFrames.HasAnimation("idle"))
                {
                    normalAnimSprite.Animation = "idle";
                    normalAnimSprite.Frame = 0;
                    normalAnimSprite.Stop();
                }
                // ✅ Normal 层也变暗（shader tone 只影响自身，不影响子节点）
                if (normalAnimSprite.Material is ShaderMaterial normalMat)
                    normalMat.SetShaderParameter("tone", dim);
                else
                    normalAnimSprite.Modulate = dim;
            }
        }
        
        // ✅ 双图层 Sprite2D：变暗
        if (baseSprite != null && IsInstanceValid(baseSprite))
        {
            if (baseSprite.Material is ShaderMaterial baseMat)
                baseMat.SetShaderParameter("tone", dim);
            else
                baseSprite.Modulate = dim;
            
            // ✅ Normal 层也变暗
            if (normalSprite != null && IsInstanceValid(normalSprite))
            {
                if (normalSprite.Material is ShaderMaterial normalMat)
                    normalMat.SetShaderParameter("tone", dim);
                else
                    normalSprite.Modulate = dim;
            }
        }
        
        if (animSprite != null)
        {
            if (idleSprite != null) idleSprite.Hide();
            animSprite.Show();
            string idleAnimName = GetIdleAnimName();
            if (animSprite.SpriteFrames != null && animSprite.SpriteFrames.HasAnimation(idleAnimName))
                animSprite.Animation = idleAnimName;
            else if (animSprite.SpriteFrames != null && animSprite.SpriteFrames.HasAnimation("idle"))
                animSprite.Animation = "idle";
            animSprite.Frame = 0;
            animSprite.Stop();
            // ✅ LUT模式：通过Shader tone设置变暗
            if (animSprite.Material is ShaderMaterial shaderMat)
                shaderMat.SetShaderParameter("tone", dim);
            else
                animSprite.Modulate = dim;
        }
        else if (sprite != null)
        {
            if (sprite.Material is ShaderMaterial shaderMat)
                shaderMat.SetShaderParameter("tone", dim);
            else
                sprite.Modulate = dim;
        }
        
        // ✅ 保险：统一调用 SetWaitVisual 确保所有图层都被处理
        SetWaitVisual(true);
    }

    public virtual void RestoreAnimAndBreath()
    {
        string animName = GetIdleAnimName();
        ShowAnimState(animName);
        StartBreath();
    }

    public virtual void EnsureGridBound()
    {
        if (grid != null && IsInstanceValid(grid))
        {
            var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
            if (gm?.unitManager != null)
            {
                var expectedPos = gm.unitManager.GridToWorld(grid.GridIndex);
                if (Position.DistanceTo(expectedPos) > 1) Position = expectedPos;
            }
            return;
        }
        var gameManager = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gameManager?.unitManager != null)
        {
            bool bound = gameManager.unitManager.BindUnitToGrid(this, true);
            if (!bound || grid == null)
            {
                var gridPos = gameManager.unitManager.WorldToGrid(Position);
                if (gameManager.unitManager.IsValidGrid(gridPos))
                {
                    var targetGrid = gameManager.gridManager.map[gridPos.X, gridPos.Y];
                    if (targetGrid != null)
                    {
                        grid = targetGrid;
                        if (!targetGrid.infantries.Contains(this)) targetGrid.infantries.Add(this);
                        if (targetGrid.infantry == null) targetGrid.infantry = this;
                        Position = targetGrid.Position;
                    }
                }
            }
        }
    }


}
    
