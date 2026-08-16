// RepairBoat.cs —— 海军维修艇（Black Boat 复刻）
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class RepairBoat : Infantry
{
    // ========== ✅ RepairBoat 特有：修复模块配置 ==========
    [ExportGroup("修复模块")]
    [Export] public bool canRepair = true;                      // 是否可以修复
    [Export] public int minRepairRange = 1;                     // 最小修复范围
    [Export] public int maxRepairRange = 1;                     // 最大修复范围（默认贴身）
    [Export] public bool repairCostsFunds = false;              // 修复是否消耗资金
    [Export] public int repairCostPerHp = 0;                    // 每点血量消耗资金
    [Export] public bool canExceedMaxHealth = false;            // 是否能超越最大血量
    [Export] public int exceededMaxHealth = 100;                // 超越后的新最大值
    [Export] public bool canRepairAfterMove = true;             // 是否可以移动后修复
    [Export] public int repairAmountPerAction = 10;             // 每次修复多少血量（10=10%一滴血，可为负=扣血，可为0）
    [Export] public bool isAreaRepair = false;                  // 能否群体修复（范围内全部修复）
    [Export] public int repairTargetMode = 1;                   // 0=所有 1=仅友方 2=仅敌方 3=仅自己
    [Export] public bool autoRepairOnTurnStart = false;         // 每回合自动修复（不占用操作）
    [Export] public bool autoRepairAfterMove = false;           // 移动后自动修复（不占用操作）

    // ========== ✅ RepairBoat 覆盖：解决字段隐藏问题 ==========
    public override bool CanPerformRepair => canRepair;

    // ========== 动画名称 ==========
    public override string GetIdleAnimName() => team == "Player2" ? "RepairBoat2" : "RepairBoat1";

    public override void _Ready()
    {
        // ✅ 设置 RepairBoat 默认属性（在 base._Ready 之前，覆盖场景预设值）
        moveType = MoveType.Hover;           // 移动方式：Hover（水陆两用）
        cost = 7500;                          // 费用：7500
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
        if (unit.team != this.team) return false;

        // 只允许 Infantry / Mech / Bike
        bool isAllowedType = unit.GetType() == typeof(Infantry) || unit is Mech || unit is Bike;
        if (!isAllowedType) return false;

        return true;
    }

    // ========== ✅ 修复范围计算 ==========
    public List<Grids> CalculateRepairRange()
    {
        var result = new List<Grids>();
        var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.gridManager == null || grid == null) return result;

        var allRange = gm.gridManager.FindRange(grid, maxRepairRange, false);

        if (minRepairRange > 1)
        {
            var blindZone = gm.gridManager.FindRange(grid, minRepairRange - 1, false);
            allRange = allRange.Where(g => !blindZone.Contains(g)).ToList();
        }

        allRange = allRange.Where(g => g != grid).ToList();
        return allRange;
    }

    // ========== ✅ 获取修复范围内的可修复目标 ==========
    public List<Infantry> GetRepairableTargets()
    {
        var result = new List<Infantry>();
        var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.gridManager == null) return result;

        var repairRange = CalculateRepairRange();

        foreach (var g in repairRange)
        {
            if (g == null) continue;

            foreach (var unit in g.infantries)
            {
                if (unit == null || !IsInstanceValid(unit)) continue;
                if (unit == this) continue;

                // 目标过滤
                bool isAlly = unit.team == this.team;
                bool isEnemy = !isAlly;

                switch (repairTargetMode)
                {
                    case 1: // 仅友方
                        if (!isAlly) continue;
                        break;
                    case 2: // 仅敌方
                        if (!isEnemy) continue;
                        break;
                }

                // 检查单位是否还可以被修复（未达上限）
                if (!canExceedMaxHealth && unit.health >= unit.maxHealth)
                    continue;

                result.Add(unit);
            }
        }

        return result;
    }

    // ========== ✅ 执行修复 ==========
    public virtual bool PerformRepair(Infantry target)
    {
        if (target == null || !IsInstanceValid(target)) return false;
        if (!canRepair) return false;

        // 检查目标是否在修复范围内
        var repairRange = CalculateRepairRange();
        if (!repairRange.Contains(target.grid)) return false;

        // 检查目标是否符合修复条件
        bool isAlly = target.team == this.team;
        bool isEnemy = !isAlly;
        switch (repairTargetMode)
        {
            case 1: if (!isAlly) return false; break;
            case 2: if (!isEnemy) return false; break;
        }

        // 计算实际修复量
        int actualRepair = repairAmountPerAction;
        if (actualRepair == 0) return false;

        var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;

        if (actualRepair > 0)
        {
            // 加血
            int oldHealth = target.health;
            int healCap = canExceedMaxHealth ? exceededMaxHealth : target.maxHealth;
            target.health = Mathf.Min(healCap, target.health + actualRepair);
            int actualHealed = target.health - oldHealth;

            if (actualHealed <= 0) return false;

            // 消耗资金（如果启用）
            if (repairCostsFunds && gm != null)
            {
                int cost = actualHealed * repairCostPerHp;
                int currentFunds = gm.GetTeamFunds(team);
                if (currentFunds < cost)
                    return false; // 资金不足
                gm.SetTeamFunds(team, currentFunds - cost);
            }

            target.UpdateHpLabel();
            ShowRepairEffect(target, actualHealed);
        }
        else
        {
            // 扣血（负值）
            int damage = Mathf.Abs(actualRepair);
            target.health -= damage;
            target.UpdateHpLabel();
            ShowRepairEffect(target, -damage);

            // 检查死亡
            if (target.health <= 0)
            {
                target.OnDestroyed();
                gm?.RemovePiece(target);
                target.QueueFree();
            }
        }

        return true;
    }

    // ========== ✅ 群体修复 ==========
    public virtual void PerformAreaRepair()
    {
        if (!isAreaRepair) return;

        var targets = GetRepairableTargets();
        foreach (var target in targets)
        {
            PerformRepair(target);
        }
    }

    // ========== ✅ 自动修复（回合开始触发） ==========
    public virtual void OnTurnStartAutoRepair()
    {
        if (!autoRepairOnTurnStart) return;
        if (isAreaRepair)
            PerformAreaRepair();
        else
        {
            var targets = GetRepairableTargets();
            if (targets.Count > 0)
                PerformRepair(targets[0]);
        }
    }

    // ========== ✅ 显示修复特效 ==========
    private void ShowRepairEffect(Infantry target, int amount)
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

    // ========== ✅ 回合结束处理（自动修复） ==========
    public override void OnTurnEnd()
    {
        base.OnTurnEnd();
        // 回合开始时自动修复由 GameManager 统一调用
    }
}
