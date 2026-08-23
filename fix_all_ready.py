path = r'C:\Users\honor\Documents\aw-one-new\spricts\RepairBoat.cs'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

old = '''    public override void _Ready()
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
        transportRequireCanCapture = true;    // 只能运输可占领单位（步兵类）
        transportMaxMovePoints = 5;           // 最大移动力限制（过滤掉非步兵）
        baseDefense = 20;                     // 基础防御
        overlapType = UnitOverlapType.NonOverlapping;

        base._Ready();
        // ✅ 强制确保 RepairBoat 运输/修复字段正确（覆盖 UnitRegistry/Inspector 可能带来的错误值）
        canTransportUnits = true;             // 可搭载
        maxTransportCapacity = 1;             // 搭载容量：1
        canSupplyUnits = false;               // ✅ 不能补给（只有修复功能）
        canRepair = true;
        canRepairAfterMove = true;
        repairAmountPerAction = 10;
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.RegisterUnit(this, UnitCategory.RepairBoat);
    }'''

new = '''    public override void _Ready()
    {
        base._Ready();
        // ✅ 强制覆盖 UnitRegistry / Inspector 默认值，确保 RepairBoat 特性正确
        moveType = MoveType.Hover;            // 移动方式：Hover（水陆两用）
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
        baseDefense = 20;                     // 基础防御
        overlapType = UnitOverlapType.NonOverlapping;
        canTransportUnits = true;             // 可搭载
        maxTransportCapacity = 1;             // 搭载容量：1
        canSupplyUnits = false;               // ✅ 不能补给（只有修复功能）
        transportRequireCanCapture = true;    // 只能运输可占领单位（步兵类）
        transportMaxMovePoints = 5;           // 最大移动力限制（过滤掉非步兵）
        canRepair = true;
        canRepairAfterMove = true;
        repairAmountPerAction = 10;
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.RegisterUnit(this, UnitCategory.RepairBoat);
    }'''

if old in content:
    content = content.replace(old, new)
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)
    print("Fixed _Ready: all fields moved after base._Ready()")
else:
    print("Pattern not found")
