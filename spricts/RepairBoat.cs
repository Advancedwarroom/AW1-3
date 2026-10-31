// RepairBoat.cs —— 海军维修艇（Black Boat 复刻）
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class RepairBoat : Infantry
{
    // ✅ 修复模块字段统一使用 Infantry 基类（此前本类重复声明了一套同名字段，遮蔽基类，
    //    导致 GridManager.ShowRepairRange 读到的是基类默认值；修理逻辑也一并上移到基类，任何 canRepair 单位通用）

    // ========== 动画名称 ==========
    public override string GetIdleAnimName() => team == "Player2" ? "RepairBoat2" : "RepairBoat1";

    public override void _Ready()
    {
        // ✅ 设置 RepairBoat 默认属性（在 base._Ready 之前，覆盖场景预设值）
        moveType = MoveType.Hover;           // 移动方式：Hover（水陆两用）
        // 造价以检查器 cost 为准（已写入 .tscn），不在代码里覆盖
        visionRange = 1;                      // 视野：1
        defaultMovePoints = 7;                // 移动力：7
        movePoints = 7;
        maxFuel = 50;                         // 燃料上限：50
        fuel = 50;                            // 当前燃料：50
        consumeFuel = true;                   // 消耗燃料
        dailyFuelConsumption = 1;             // 每日消耗：1
        destroyOnOutOfFuel = true;            // 燃料耗尽自毁
        hasPrimaryWeapon = false;             // 无武器
        hasSecondaryWeapon = false;
        attackType = AttackType.NoAttack;     // 不能攻击
        canCapture = false;                   // 不能占领
        canTransportUnits = true;             // 可搭载
        canSupplyUnits = false;               // ✅ 不能补给（只有修复功能）
        maxTransportCapacity = 1;             // 搭载容量：1
        canTransportUnits = true;             // 可搭载
        maxTransportCapacity = 1;             // 搭载容量：1
        transportRequireCanCapture = true;    // 只能运输可占领单位（步兵类）
        transportMaxMovePoints = 5;           // 最大移动力限制（过滤掉非步兵）
        baseDefense = 20;                     // 基础防御
        overlapType = UnitOverlapType.NonOverlapping;

        base._Ready();
        // ✅ 强制确保 RepairBoat 修复字段正确（覆盖 Inspector/序列化可能带来的错误值）
        canRepair = true;
        canRepairAfterMove = true;
        repairAmountPerAction = 10;
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.RegisterUnit(this, UnitCategory.RepairBoat);
    }

    // ========== ✅ 重写：RepairBoat 卸载限制 ==========
    // 卸载必须在被卸载单位可移动的地形上（不能在海上卸载步兵）
    public override bool CanUnloadToGrid(Grids targetGrid)
    {
        if (transportedUnits.Count == 0) return false;
        if (targetGrid == null) return false;

        // 获取要卸载的单位
        var unitToUnload = GetFirstLoadedUnit();
        if (unitToUnload == null) return false;

        // 关键：检查被卸载单位是否能通过该地形
        int moveCost = unitToUnload.GetMoveCost(targetGrid.gridType);
        if (moveCost >= 999 || moveCost == int.MaxValue)
            return false; // 被卸载单位无法通过这种地形

        // 检查目标格子是否有非重叠单位阻挡
        var blockingUnit = targetGrid.infantries.FirstOrDefault(u =>
            u != null && IsInstanceValid(u) &&
            u.overlapType == UnitOverlapType.NonOverlapping);

        return blockingUnit == null;
    }

    // ========== ✅ 重写：RepairBoat 只能搭载步兵类单位（Infantry / Mech / Bike）==========
    public override bool CanTransportUnit(Infantry unit)
    {
    if (unit == null || !IsInstanceValid(unit)) return false;
    if (transportedUnits.Count >= maxTransportCapacity) return false;
    // ✅ P-1 公共单位可以搭载任何势力
    if (this.team != TeamHelper.Player && unit.team != this.team) return false;

    // 只允许 Infantry / Mech / Bike
    bool isAllowedType = unit.GetType() == typeof(Infantry) || unit is Mech || unit is Bike;
    if (!isAllowedType) return false;

    return true;
    }







    // ========== ✅ 回合结束处理（自动修复） ==========
    public override void OnTurnEnd()
    {
        base.OnTurnEnd();
        // 回合开始时自动修复由 GameManager 统一调用
    }
}
