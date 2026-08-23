import re

with open(r'C:\Users\honor\Documents\aw-one-new\spricts\RepairBoat.cs', 'r', encoding='utf-8') as f:
    content = f.read()

old = "bool isAllowedType = unit.GetType() == typeof(Infantry) || unit is Mech || unit is Bike;"
new = "bool isAllowedType = unit is Infantry && unit is not RepairBoat;"

if old in content:
    content = content.replace(old, new)
    # Also update comment
    content = content.replace(
        "// 只允许 Infantry / Mech / Bike",
        "// 只允许 Infantry / Mech / Bike（都继承 Infantry，但排除 RepairBoat 自身）"
    )
    with open(r'C:\Users\honor\Documents\aw-one-new\spricts\RepairBoat.cs', 'w', encoding='utf-8') as f:
        f.write(content)
    print("Fixed CanTransportUnit")
else:
    print("Pattern not found")
