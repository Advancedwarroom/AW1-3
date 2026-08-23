path = r'C:\Users\honor\Documents\aw-one-new\spricts\RepairBoat.cs'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# 删除 base._Ready() 之前的重复运输设置，在 base._Ready() 之后强制设置
old = '''        canTransportUnits = true;             // 可搭载
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
        repairAmountPerAction = 10;'''

new = '''        transportRequireCanCapture = true;    // 只能运输可占领单位（步兵类）
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
        repairAmountPerAction = 10;'''

if old in content:
    content = content.replace(old, new)
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)
    print("Fixed _Ready order")
else:
    print("Pattern not found")
