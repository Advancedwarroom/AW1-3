import re

path = r'C:\Users\honor\Documents\aw-one-new\spricts\GridManager.cs'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

old = '''                int moveCost = infantry.GetMoveCost(neighbor.gridType);
                // ✅ 运输单位例外：如果目标格子有己方运输单位可搭载，允许进入（即使地形不可通行）
                var transportOnNeighbor = neighbor.infantries.FirstOrDefault(u =>
                    u != null && IsInstanceValid(u) && u != infantry && u.team == infantry.team &&
                    u.canTransportUnits && u.maxTransportCapacity > 0 && u.CanTransportUnit(infantry));
                if (transportOnNeighbor == null && (moveCost >= 999 || moveCost == int.MaxValue)) continue;
                if (transportOnNeighbor != null) moveCost = 1;'''

new = '''                int moveCost = infantry.GetMoveCost(neighbor.gridType);
                if (moveCost >= 999 || moveCost == int.MaxValue) continue;'''

if old in content:
    content = content.replace(old, new)
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)
    print("Rolled back")
else:
    print("Pattern not found")
