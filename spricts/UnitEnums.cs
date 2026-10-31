using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

// 由 Infantry.cs 拆出的共享类型

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
