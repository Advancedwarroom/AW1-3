using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

// Infantry · DamageMatrix（由 Infantry.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class Infantry
{

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
        
            // ✅ 矩阵空洞修复（2026-10-31审计：照明车机枪补全+互缺对补值）
            ["Flare_Infantry"] = 65,
            ["Flare_Mech"] = 50,
            ["Flare_Bike"] = 60,
            ["Flare_Recon"] = 65,
            ["Flare_APC"] = 45,
            ["Flare_LightTank"] = 35,
            ["Flare_MdTank"] = 25,
            ["Flare_Artillery"] = 45,
            ["Flare_Rocket"] = 45,
            ["Flare_AntiAir"] = 35,
            ["Flare_AntiTank"] = 35,
            ["Infantry_Flare"] = 55,
            ["Bike_Flare"] = 50,
            ["Recon_Flare"] = 70,
            ["Infantry_Bike"] = 65,
            ["Bike_Infantry"] = 70,
            ["Recon_Bike"] = 60,
            ["Bike_Recon"] = 35,
            ["AntiTank_Oozium"] = 35,
            ["PipeRunner_Oozium"] = 35,
            ["BCopter_TCopter"] = 65,
            
            ["Dog_Infantry"] = 28, ["Dog_Mech"] = 22, ["Dog_Bike"] = 32, ["Dog_Recon"] = 6,
            ["Dog_APC"] = 8, ["Dog_LightTank"] = 8, ["Dog_MdTank"] = 2, ["Dog_Artillery"] = 12,
            ["Dog_Rocket"] = 12, ["Dog_AntiAir"] = 12, ["Dog_AntiTank"] = 1, ["Dog_Oozium"] = 18,
            ["Dog_Flare"] = 28, ["Dog_AntiAirMissile"] = 12,
            ["AntiAir_Dog"] = 74, ["Artillery_Dog"] = 63, ["AntiTank_Dog"] = 52, ["Bike_Dog"] = 49,
            ["Bomber_Dog"] = 77, ["Flare_Dog"] = 46, ["Infantry_Dog"] = 38, ["LightTank_Dog"] = 52,
            ["MdTank_Dog"] = 77, ["Mech_Dog"] = 46, ["Oozium_Dog"] = 699, ["PipeRunner_Dog"] = 66,
            ["Recon_Dog"] = 49, ["Rocket_Dog"] = 66, ["Stealth_Dog"] = 66, ["Dog_Dog"] = 32,

            ["BCopter_Dog"] = 53, ["TCopter_Dog"] = 53,
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
    // ✅ 雾战攻防表：优先级 隐身表 > 雾战表 > 默认/模板表（雾中隐身=上面已用隐身表返回）
    if (FogActive() && fogMatrixOverride != null && fogMatrixOverride.Count > 0)
        foreach (var k in keys) if (fogMatrixOverride.TryGetValue(k, out int fval)) return fval;
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
}
