// GameManager.cs - 添加完整的单位统计系统
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 由 GameManager.cs 拆出的共享类型

public enum UnitCategory
{
    Infantry,   
    Tank,       
    MdTank,     
    Vehicle,   
    Mech,       
    Oozium,     

    Rocket,     
    FlyBomb,    // 飞弹/自爆单位
    Artillery,
    APC, 
    Recon,
    AntiAir,
    AntiTank,
    PipeRunner,
    Bike,
    Flare,
    RepairBoat,
    Fighter,    // 战斗机
    Bomber,    // 轰炸机
    BCopter,    // 武装直升机
    TCopter,    // 运输直升机
    AntiAirMissile,    // 防空导弹车
    Stealth,    // 隐身战机
    Dog,    // 机械狗

    Other       // 其他未分类单位
}
