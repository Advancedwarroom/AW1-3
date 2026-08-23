path = r'C:\Users\honor\Documents\aw-one-new\spricts\GridManager.cs'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# 去掉 GetMoveRange 的 debug 日志
old1 = '''                int moveCost = infantry.GetMoveCost(neighbor.gridType);
                if (moveCost >= 999 || moveCost == int.MaxValue)
                {
                    GD.Print($"[GetMoveRange] 排除格子 {neighbor.GridIndex}: moveCost={moveCost} 过高");
                    continue;
                }'''
new1 = '''                int moveCost = infantry.GetMoveCost(neighbor.gridType);
                if (moveCost >= 999 || moveCost == int.MaxValue) continue;'''

# 去掉 CanStayOnGrid 的 debug 日志
old2 = '''    private bool CanStayOnGrid(Grids grid, Infantry requestingUnit)
    {
        var otherUnits = grid.infantries.Where(i => i != requestingUnit && IsInstanceValid(i)).ToList();
        GD.Print($"[CanStayOnGrid] 格子 {grid.GridIndex}, otherUnits={otherUnits.Count}");
        foreach (var u in otherUnits)
            GD.Print($"  - {u.GetType().Name} team={u.team} canTransport={u.canTransportUnits} maxCap={u.maxTransportCapacity}");
        if (otherUnits.Count == 0) return true;

        var friendlyTransport = otherUnits.FirstOrDefault(u => u.canTransportUnits && u.maxTransportCapacity > 0 && u.team == requestingUnit.team && u.CanTransportUnit(requestingUnit));
        GD.Print($"[CanStayOnGrid] friendlyTransport={(friendlyTransport != null ? friendlyTransport.GetType().Name : \"null\")}");
        if (friendlyTransport != null)
        {
        return true;
        }'''

new2 = '''    private bool CanStayOnGrid(Grids grid, Infantry requestingUnit)
    {
        var otherUnits = grid.infantries.Where(i => i != requestingUnit && IsInstanceValid(i)).ToList();
        if (otherUnits.Count == 0) return true;

        var friendlyTransport = otherUnits.FirstOrDefault(u => u.canTransportUnits && u.maxTransportCapacity > 0 && u.team == requestingUnit.team && u.CanTransportUnit(requestingUnit));
        if (friendlyTransport != null)
        {
        return true;
        }'''

content = content.replace(old1, new1)
content = content.replace(old2, new2)

with open(path, 'w', encoding='utf-8') as f:
    f.write(content)

print("Removed debug logs")
