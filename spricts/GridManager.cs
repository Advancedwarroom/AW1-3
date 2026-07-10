// GridManager.cs - 修改 HandleGridClick 和 SetupMoveRangeCallbacks 方法
using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Linq;
using System.Runtime.CompilerServices;

public partial class GridManager : Node
{
    [Export]
    public Node GridsNode;
    public Grids[,]map;
    [Export] public Vector2I searchRange;
    [Export] public Vector2I startPos;
    [Export] public Vector2I gridSize;
    [Export] public UnitManager unitManager;
    public List<Grids> previewPath = new List<Grids>();
    public Line2D pathLine;
    public List<Grids> grids =new List<Grids>();
    public Label damagePreviewLabel;

    public Infantry selectedUnit;

    public WeaponManager weaponManager;

    public override void _Ready()
    {
    

        AddToGroup("grid_manager");

        pathLine = new Line2D
        {
            Name = "PathPreview",
            Width = 4,
            DefaultColor = new Color(1,1,0,0.75f),
            ZIndex = 100
        };
        AddChild(pathLine);

        damagePreviewLabel = new Label();
        damagePreviewLabel.Name = "DamagePreviewLabel";
        damagePreviewLabel.ZIndex = 200;
        damagePreviewLabel.Hide();
        damagePreviewLabel.AddThemeColorOverride("font_color", new Color(1, 1, 0.8f));
        damagePreviewLabel.AddThemeFontSizeOverride("font_size", 14);
        damagePreviewLabel.AddThemeConstantOverride("outline_size", 3);
        damagePreviewLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        damagePreviewLabel.HorizontalAlignment = HorizontalAlignment.Left;
        damagePreviewLabel.VerticalAlignment = VerticalAlignment.Center;
        damagePreviewLabel.CustomMinimumSize = new Godot.Vector2(120, 40);
        AddChild(damagePreviewLabel);
        
        weaponManager = GetTree().GetFirstNodeInGroup("weapon_manager") as WeaponManager;
    }

    public void ShowWeaponAttackRange(Weapon weapon)
    {
        if (weapon == null || weapon.hasActed) return;
        
        CloseRange();
        HideAttackRange();
        ClearWeaponRange();
        
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm != null) gm.isSelectingAttackTarget = true;
        
        weapon.ShowAttackRange();
    }

    public void ClearWeaponRange()
    {
        Grids.IsForceActionMode = false;
        foreach (var grid in grids)
        {
            if (grid == null || !IsInstanceValid(grid)) continue;
            
            grid.IsInWeaponRange = false;
            
            if (!moveRange.Contains(grid))
            {
                grid.pathIcon?.Hide();
            }
            
            if (!attackRange.Contains(grid))
            {
                grid.attackRangeIcon?.Hide();
            }
            
            if (grid.OnClickGrid?.Target is Weapon) 
            {
                grid.OnClickGrid = null;
            }
        }
    }

    public void SetEmptyClickToCloseMenuOnly(Infantry unit)
    {
        foreach (var grid in grids)
        {
            grid.OnClickEmpty = () => 
            {
                CloseRange();
                HideAttackRange();
                ClearWeaponRange();
                var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
                actionMenu?.CancelExplosionPreview(); // ✅ 取消爆炸预览
                actionMenu?.Hide();
                
                var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
                gm?.ClearSelectedInfantry();
            };
        }
    }

    public override void _Process(double delta)
    {
    }

    private bool ShouldBlockGrid(Grids grid, Infantry requesting)
    {
        if (requesting.overlapType == UnitOverlapType.Overlapping)
            return false;
        return grid.infantry != null && grid.infantry != requesting;
    }

    public List<Grids> BuildPath(Grids from, Grids to)
    {
        var queue = new Queue<Grids>();
        var parent = new Dictionary<Grids, Grids>();
        queue.Enqueue(from);
        parent[from] = null;

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == to) break;

            foreach (var n in GetNeighbours(cur, true))
            {
                if (parent.ContainsKey(n)) continue;
                parent[n] = cur;
                queue.Enqueue(n);
            }
        }

        var path = new List<Grids>();
        Grids walk = to;
        while (walk != null)
        {
            path.Add(walk);
            walk = parent.GetValueOrDefault(walk);
        }
        path.Reverse();
        return path.Count > 1 ? path : new List<Grids>();
    }

    public void Init()
    {
        map = new Grids[searchRange.X, searchRange.Y];
        grids.Clear(); // ✅ 防止重复填充导致统计翻倍
        for (int i = 0; i < GridsNode.GetChildCount(); i++)
        {
            Grids grid = GridsNode.GetChild<Grids>(i);
            grids.Add(grid);
            int x = (int)((grid.Position.X - startPos.X) / gridSize.X);
            int y = (int)((grid.Position.Y - startPos.Y) / gridSize.Y);

            if (x >= 0 && x < searchRange.X && y >= 0 && y < searchRange.Y)
            {
                grid.GridIndex = new Vector2I(x, y);
                map[x, y] = grid;

                switch (grid.gridType)
                {
                    case GridType.GROUND:
                    case GridType.ROAD:
                        break;
                    case GridType.HILL:
                        break;
                    default:
                        break;
                }

                // ✅ 初始化城市默认势力
                if (grid.city != null && string.IsNullOrEmpty(grid.city.facilityTeam))
                {
                    int middleStart = searchRange.X / 3;
                    int middleEnd = searchRange.X * 2 / 3;

                    if (x >= middleStart && x < middleEnd)
                        grid.city.facilityTeam = "Player0";
                    else if (x < middleStart)
                        grid.city.facilityTeam = "Player1";
                    else
                        grid.city.facilityTeam = "Player2";
                }
            }
            else
            {
                grid.GridIndex = new Vector2I(-1, -1);
            }
        }
    }

    public Vector2I[] offset =
    {
        new Vector2I(0,1),
        new Vector2I(-1,0),
        new Vector2I(0,-1),
        new Vector2I(1,0),
    };

    public List<Grids> GetAttackNeighbours(Grids grid)
    {
        var neighbours = new List<Grids>();
        if (grid == null) return neighbours;

        for (int i = 0; i < 4; i++)
        {
            var vector = grid.GridIndex + offset[i];
            if (vector.X < 0 || vector.X >= searchRange.X ||
                vector.Y < 0 || vector.Y >= searchRange.Y) continue;
            if (map[vector.X, vector.Y] == null) continue;
            neighbours.Add(map[vector.X, vector.Y]);
        }
        return neighbours;
    }

    public void OnMouseEnteredGrid(Grids grid)
    {
        if (!moveRange.Contains(grid)) return;

        // ✅ 雾战紫色路径：恢复所有移动范围格子的颜色，再标记路径为紫色
        foreach (var g in moveRange)
        {
            if (g?.pathIcon != null)
                g.pathIcon.Modulate = Colors.White;
        }

        previewPath = BuildPath(selectedUnit.grid, grid);

        // ✅ 将路径上在移动范围内的格子标记为紫色
        foreach (var g in previewPath)
        {
            if (moveRange.Contains(g) && g?.pathIcon != null)
                g.pathIcon.Modulate = new Color(0.7f, 0.3f, 1.0f, 1.0f); // 紫色
        }

        UpdatePathLine();
    }

    public void OnMouseExitedGrid(Grids _)
    {
        previewPath.Clear();
        pathLine.ClearPoints();

        // ✅ 恢复所有移动范围格子的颜色
        foreach (var g in moveRange)
        {
            if (g?.pathIcon != null)
                g.pathIcon.Modulate = Colors.White;
        }
    }

    public void UpdatePathLine()
    {
        pathLine.ClearPoints();
        foreach (var g in previewPath)
            pathLine.AddPoint(g.Position + new Godot.Vector2(16, 16));
    }

    public List<Grids> GetNeighbours(Grids grid, bool walk = false)
    {
        List<Grids> neighbours = new List<Grids>();
        for (int i = 0; i < 4; i++)
        {
            Vector2I vector = grid.GridIndex + offset[i];
            if (vector.X < 0 || vector.X >= searchRange.X
                || vector.Y < 0 || vector.Y >= searchRange.Y) continue;
            if (map[vector.X, vector.Y] == null) continue;

            var neighbor = map[vector.X, vector.Y];

            // ✅ 移除METEORITE硬编码：所有地形通行性由GetMoveCost统一控制
            // SpaceShiper等移动方式可以通过METEORITE
            neighbours.Add(neighbor);
        }
        return neighbours;
    }

    public List<Grids> FindRange(Grids grid, int distance, bool walk = false)
    {
        List<Grids> range =new List<Grids>();
        List<Grids> now =new List<Grids>();
        List<Grids> open =new List<Grids>();

        now.Add(grid);
        for (int i = 0; i < distance; i++)
        {
            foreach (var current in now)
            {
                List<Grids> neighbours = GetNeighbours(current,true);
                foreach (var neighbour in neighbours)
                {
                    if(!open.Contains(neighbour) && neighbour !=grid)
                    {
                        open.Add(neighbour);
                        range.Add(neighbour);
                    }
                }
            }
            now.Clear();
            foreach(var item in open)
                now.Add(item);
            open.Clear();
        }
        return range;
    }

    public  List<Grids> moveRange = new List<Grids>() ;

    public void ShowMoveRange(Infantry infantry)
    {
        Grids.IsForceActionMode = true;
    var terrainEditor = GetTree().GetFirstNodeInGroup("terrain_editor") as TerrainEditor;
    if (terrainEditor != null && terrainEditor.ShouldBlockUnitOperations())
    {
        return;
    }

        selectedUnit = infantry;
        if (infantry == null || infantry.grid == null) return;

        ForceClearAllRangeVisuals();
        
        CloseRange();
        moveRange.Clear();
        HideAttackRange();
        ClearWeaponRange(); 

        int effectiveMovePoints = infantry.movePoints;
        if (infantry.consumeFuel)
            effectiveMovePoints = Mathf.Min(infantry.movePoints, infantry.fuel);

        // ✅ 究极自由：所有单位使用统一的通用移动范围计算
        CalculateMoveRangeGeneric(infantry, effectiveMovePoints);
        SetupMoveRangeCallbacks(infantry);
    }

    private void ForceClearAllRangeVisuals()
    {
        foreach (var grid in grids)
        {
            if (grid == null || !IsInstanceValid(grid)) continue;
            
            grid.pathIcon?.Hide();
            grid.attackRangeIcon?.Hide();
            
            if (grid.pathIcon != null)
                grid.pathIcon.Modulate = Colors.White;
            
            if (grid.attackRangeIcon != null)
            {
                grid.attackRangeIcon.Modulate = new Color(1, 0.2f, 0.2f, 0.9f);
                grid.attackRangeIcon.Hide(); // ✅ 确保隐藏
            }
                
            grid.OnClickGrid = null;
            grid.OnClickEmpty = null;
            grid.OnMouseEnteredAttackGrid = null;
            grid.OnMouseExitedAttackGrid = null;
            
            grid.IsInAttackRangeMode = false;
            grid.IsInWeaponRange = false;
        }
        
        attackRange.Clear();
        explosionRange.Clear(); // ✅ 清除爆炸范围缓存
        flareRange.Clear(); // ✅ 清除照明弹射程范围
        illuminationRange.Clear(); // ✅ 清除照明覆盖范围
        
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm != null) 
        {
            gm.isSelectingAttackTarget = false;
        }
    }

    // ========== ✅ 核心修改：SetupMoveRangeCallbacks ==========
    // ✅ 公共方法：纯计算移动范围（不修改UI，供玩家和AI统一使用）
    public List<Grids> GetMoveRange(Infantry infantry, int maxMove)
    {
        var result = new List<Grids>();
        if (infantry == null || infantry.grid == null || map == null) return result;

        var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
        var fog = gm?.fogOfWarManager;
        bool isFogEnabled = fog != null && fog.isFogOfWarEnabled;

        var open = new Queue<(Grids grid, int cost)>();
        var best = new Dictionary<Grids, int>();
        open.Enqueue((infantry.grid, 0));
        best[infantry.grid] = 0;

        while (open.Count > 0)
        {
            var (cur, accCost) = open.Dequeue();

            // ✅ 可见格子或迷雾关闭时：当前格子有敌方单位则阻挡后续路径扩展（仅NonOverlapping）
            if (cur != infantry.grid && infantry.overlapType == UnitOverlapType.NonOverlapping)
            {
                bool curVisible = !isFogEnabled || fog.IsGridVisible(cur);
                if (curVisible)
                {
                    cur.infantries.RemoveAll(u => u == null || !IsInstanceValid(u));
                    var enemyOnCur = cur.infantries.FirstOrDefault(u => u != infantry && u.team != infantry.team);
                    if (enemyOnCur != null)
                        continue;
                }
            }

            for (int dirIdx = 0; dirIdx < 4; dirIdx++)
            {
                var dir = offset[dirIdx];
                var nIdx = cur.GridIndex + dir;

                if (nIdx.X < 0 || nIdx.X >= searchRange.X ||
                    nIdx.Y < 0 || nIdx.Y >= searchRange.Y) continue;

                var neighbor = map[nIdx.X, nIdx.Y];
                if (neighbor == null) continue;

                int moveCost = infantry.GetMoveCost(neighbor.gridType);
                if (moveCost >= 999 || moveCost == int.MaxValue) continue;

                if (unitManager?.CanMoveTo(neighbor.GridIndex, infantry) == false)
                    continue;

                int newCost = accCost + moveCost;
                if (best.TryGetValue(neighbor, out int old) && newCost >= old) continue;
                if (newCost > maxMove) continue;

                if (infantry is not Oozium && neighbor.weapon != null)
                    continue;

                // ✅ 可见格子或迷雾关闭时：敌方单位所在格子不加入移动范围（仅NonOverlapping）
                if (infantry.overlapType == UnitOverlapType.NonOverlapping)
                {
                    bool neighborVisible = !isFogEnabled || fog.IsGridVisible(neighbor);
                    if (neighborVisible)
                    {
                        neighbor.infantries.RemoveAll(u => u == null || !IsInstanceValid(u));
                        var enemyOnNeighbor = neighbor.infantries.FirstOrDefault(u => u != infantry && u.team != infantry.team);
                        if (enemyOnNeighbor != null)
                            continue;
                    }
                }

                best[neighbor] = newCost;
                open.Enqueue((neighbor, newCost));

                if (!result.Contains(neighbor))
                    result.Add(neighbor);
            }
        }
        return result;
    }

    // ✅ 新增：通用移动范围计算方法（替代所有 ShowXXXMoveRange）
    private void CalculateMoveRangeGeneric(Infantry infantry, int maxMove)
    {
        var range = GetMoveRange(infantry, maxMove);
        moveRange.Clear();
        moveRange.AddRange(range);
    }

private void SetupMoveRangeCallbacks(Infantry infantry)
{
    var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
    var fog = gm?.fogOfWarManager;
    bool isFogEnabled = fog != null && fog.isFogOfWarEnabled;

    foreach (var g in moveRange)
    {
        g.pathIcon?.Show();

        // ✅ 禁用移动范围内所有单位的输入，防止误选（包括己方未行动单位）
        OverrideUnitInput(g, true);

        bool canStay = CanStayOnGrid(g, infantry);

        if (canStay)
        {
            var transport = g.infantries.FirstOrDefault(u => 
                u.canTransportUnits 
                && u.maxTransportCapacity > 0 
                && u.team == infantry.team 
                && u != infantry) as Infantry;

            if (infantry.overlapType != UnitOverlapType.Overlapping 
                && transport != null 
                && transport.CanTransportUnit(infantry))
            {
                g.OnClickGrid = to => HandleNonOverlappingTransportMount(infantry, transport, to);
            }
            else if (infantry.overlapType == UnitOverlapType.Overlapping)
            {
                var otherUnits = g.infantries.Where(u => u != infantry && IsInstanceValid(u) && !u.isMoved).ToList();
                if (otherUnits.Count > 0)
                {
                    var targetUnit = otherUnits[0];
                    g.OnClickGrid = to => ShowOverlappingMoveChoiceDialog(infantry, targetUnit, to, transport);
                }
                else
                {
                    g.OnClickGrid = to => HandleGridClick(infantry, to);
                }
            }
            else
            {
                g.OnClickGrid = to => HandleGridClick(infantry, to);
            }
        }
        else
        {
            // ⬅️ 截断移动：不可停留但可达的格子
            if (infantry.overlapType == UnitOverlapType.NonOverlapping)
            {
                // ✅ 只有可见格子才显示红色（不可见格子不暴露敌方单位位置）
                bool gridVisible = !isFogEnabled || fog.IsGridVisible(g);
                if (gridVisible)
                {
                    g.pathIcon.Modulate = new Color(1, 0.3f, 0.3f, 0.6f);
                }
                g.OnClickGrid = to => HandleTruncatedMove(infantry, to);
            }
            else
            {
                g.OnClickGrid = to => HandleGridClick(infantry, to);
            }
        }

        g.OnClickEmpty = null;
    }

    // ✅ 空地点击回退（包括范围外的单位所在格子）
    foreach (var grid in grids)
    {
        if (!moveRange.Contains(grid))
        {
            // ✅ 禁用范围外单位的选择，统一处理为回退
            OverrideUnitInput(grid, true);
            
            grid.OnClickEmpty = () => 
            {
                var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
                bool shouldRollback = gm?.selectedInfantry != null && 
                                      gm.selectedInfantry.state == UnitState.Moved && 
                                      gm.selectedInfantry.originalGrid != null;

                if (shouldRollback)
                    gm.RollbackMove();
                else
                {
                    CloseRange();
                    HideAttackRange();
                    ClearWeaponRange();
                    var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
                    actionMenu?.Hide();
                    gm?.ClearSelectedInfantry();
                }
            };
            
            // ✅ 范围外格子点击也触发回退
            grid.OnClickGrid = to => {
                var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
                bool shouldRollback = gm?.selectedInfantry != null && 
                                      gm.selectedInfantry.state == UnitState.Moved && 
                                      gm.selectedInfantry.originalGrid != null;

                if (shouldRollback)
                    gm.RollbackMove();
                else
                {
                    CloseRange();
                    HideAttackRange();
                    ClearWeaponRange();
                    var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
                    actionMenu?.Hide();
                    gm?.ClearSelectedInfantry();
                }
            };
        }
    }
}

    private void ShowOverlappingMoveChoiceDialog(Infantry movingUnit, Infantry targetUnit, Grids targetGrid, Infantry transport)
    {
        // 先关闭移动范围（防止重复点击）
        CloseRange();

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;

        // 创建弹窗面板
        var dialog = new Control();
        dialog.Name = "MoveChoiceDialog";
        dialog.ZIndex = 500;
        dialog.SetAnchorsPreset(Control.LayoutPreset.Center);

        // 背景遮罩
        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0, 0, 0, 0.5f);
        dialog.AddChild(bg);

        // 弹窗面板
        var panel = new Panel();
        panel.CustomMinimumSize = new Godot.Vector2(320, 280);
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.2f, 0.2f, 0.3f, 0.95f);
        style.SetCornerRadiusAll(12);
        panel.AddThemeStyleboxOverride("panel", style);
        dialog.AddChild(panel);

        // 标题
        var title = new Label();
        title.Text = $"目标有 {targetUnit.Name}";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", Colors.Yellow);
        title.Position = new Godot.Vector2(0, 15);
        title.Size = new Godot.Vector2(320, 30);
        panel.AddChild(title);

        // 按钮容器
        var vbox = new VBoxContainer();
        vbox.Position = new Godot.Vector2(60, 60);
        vbox.Size = new Godot.Vector2(200, 200);
        vbox.AddThemeConstantOverride("separation", 12);
        panel.AddChild(vbox);

        // 1. 取消按钮
        var cancelBtn = new Button();
        cancelBtn.Text = "❌ 取消";
        cancelBtn.CustomMinimumSize = new Godot.Vector2(200, 40);
        cancelBtn.Pressed += () => {
            dialog.QueueFree();
            // 恢复选中移动单位，但不显示范围（用户需要重新点击）
            gm?.ClearSelectedInfantry();
        };
        vbox.AddChild(cancelBtn);

        // 2. 确认移动按钮
        var confirmBtn = new Button();
        confirmBtn.Text = "✅ 确认移动";
        confirmBtn.CustomMinimumSize = new Godot.Vector2(200, 40);
        confirmBtn.Pressed += () => {
            dialog.QueueFree();
            // 执行移动
            MoveTo(movingUnit, targetGrid);
        };
        vbox.AddChild(confirmBtn);

        // 3. 切换单位按钮
        var switchBtn = new Button();
        switchBtn.Text = $"🔄 切换到{targetUnit.Name}";
        switchBtn.CustomMinimumSize = new Godot.Vector2(200, 40);
        switchBtn.Pressed += () => {
            dialog.QueueFree();
            // 关闭当前范围，切换到新单位
            HideAttackRange();
            ClearWeaponRange();
            actionMenu?.Hide();
            gm?.ClearSelectedInfantry();
            gm?.OnSelectPiece(targetUnit);
        };
        vbox.AddChild(switchBtn);

        // 4. 搭载按钮（仅当APC可用时）
        if (transport != null && transport.CanTransportUnit(movingUnit))
        {
            var mountBtn = new Button();
            mountBtn.Text = $"🚛 搭载到{transport.Name}";
            mountBtn.CustomMinimumSize = new Godot.Vector2(200, 40);
            mountBtn.Pressed += () => {
                dialog.QueueFree();
                // 显示APC装载菜单（不是直接搭载）
                var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
                if (actionMenu != null)
                {
                    actionMenu.Hide();
                    actionMenu.ShowTransportLoadMenu(transport, movingUnit);
                }
            };
            vbox.AddChild(mountBtn);
        }

        GetTree().CurrentScene.AddChild(dialog);

        GD.Print($"[ShowOverlappingMoveChoiceDialog] 显示选择弹窗：{movingUnit.Name} -> {targetUnit.Name}");
    }

    // ✅ 究极自由：非Overlapping单位显示通用装载菜单
    private void HandleNonOverlappingTransportMount(Infantry infantry, Infantry transport, Grids to)
    {
        var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
        if (actionMenu != null)
        {
            actionMenu.Hide();
            // ✅ 究极自由：使用通用运输菜单
            actionMenu.ShowTransportLoadMenu(transport,infantry);
        }
        CloseRange();

        GD.Print($"[HandleNonOverlappingTransportMount] {infantry.Name} 显示 {transport.Name} 装载菜单");
    }

    // ========== ✅ 核心修改：HandleGridClick ==========
    public void HandleGridClick(Infantry infantry, Grids to)
    {
        // ✅ 编辑模式下禁止单位移动操作
        var terrainEditor = GetTree().GetFirstNodeInGroup("terrain_editor") as TerrainEditor;
        if (terrainEditor != null && terrainEditor.ShouldBlockUnitOperations())
        {
            return;
        }

        // ✅ 究极自由：检查任何可搭载单位
        var transport = to.infantries.FirstOrDefault(u => 
            u.canTransportUnits 
            && u.maxTransportCapacity > 0 
            && u.team == infantry.team 
            && u != infantry) as Infantry;
        if (transport != null && transport.CanTransportUnit(infantry))
        {
            var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
            actionMenu?.ShowTransportMenu(transport, afterMove: false);
            CloseRange();
            return;
        }

        var otherUnits = to.infantries.Where(i => i != infantry && IsInstanceValid(i)).ToList();
        bool hasOtherUnits = otherUnits.Count > 0 || (to.infantry != null && to.infantry != infantry);

        if (hasOtherUnits)
        {
            switch (infantry.overlapType)
            {
                case UnitOverlapType.NonOverlapping:
                var friendlyTransport = otherUnits.FirstOrDefault(u => u.canTransportUnits && u.maxTransportCapacity > 0 && u.team == infantry.team);
                if (friendlyTransport != null && friendlyTransport.CanTransportUnit(infantry))
                {
                    var menu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
                    menu?.ShowTransportLoadMenu(friendlyTransport, infantry);
                    CloseRange();
                    return;
                }
                // ⬅️ 改为截断移动而非直接取消
                HandleTruncatedMove(infantry, to);
                return;

                case UnitOverlapType.Overlapping:
                    MoveTo(infantry, to);
                    return;

                case UnitOverlapType.Oozium:
                    foreach (var target in otherUnits)
                        DevourUnit(target);
                    to.infantries.Clear();
                    to.infantry = null;
                    
                    MoveTo(infantry, to);
                    ForceOoziumActed(infantry);
                    return;
            }
        }
        else
        {
            // ✅ NonOverlapping单位检查路径上的不可移动格子，有则截断待机
            if (infantry.overlapType == UnitOverlapType.NonOverlapping)
            {
                var path = BuildPath(infantry.grid, to);
                Grids lastValidGrid = infantry.grid;
                
                for (int i = 1; i < path.Count; i++)
                {
                    var g = path[i];
                    
                    // 检查1: 敌方单位阻挡
                    g.infantries.RemoveAll(u => u == null || !IsInstanceValid(u));
                    var enemies = g.infantries.Where(u => u != infantry && u.team != infantry.team).ToList();
                    if (enemies.Count > 0)
                    {
                        // 截断到最后一个可停留的格子
                        TruncateAndMove(infantry, lastValidGrid);
                        return;
                    }
                    
                    // 检查2: 兵器阻挡（非Oozium单位）
                    if (infantry is not Oozium && g.weapon != null)
                    {
                        TruncateAndMove(infantry, lastValidGrid);
                        return;
                    }
                    
                    // 检查3: 地形不可移动
                    int moveCost = infantry.GetMoveCost(g.gridType);
                    if (moveCost >= 999 || moveCost == int.MaxValue)
                    {
                        TruncateAndMove(infantry, lastValidGrid);
                        return;
                    }
                    
                    // 检查4: CanMoveTo判定
                    if (unitManager?.CanMoveTo(g.GridIndex, infantry) == false)
                    {
                        TruncateAndMove(infantry, lastValidGrid);
                        return;
                    }
                    
                    lastValidGrid = g;
                }
            }
            
            MoveTo(infantry, to);
        }
    }

    private void ForceOoziumActed(Infantry oozium)
    {
        if (oozium == null) return;
        
        oozium.isMoved = true;
        oozium.isAttacked = true;
        oozium.state = UnitState.Acted;
        oozium.originalGrid = null;
        
        CloseRange();
        HideAttackRange();
        
        var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
        actionMenu?.Hide();
        
        ClearAllEmptyCallbacks();
        
        if (oozium is Oozium oz && oz.animSprite != null)
        {
            oz.animSprite.Modulate = new Color(0.5f, 0.5f, 0.5f, 1.0f);
            oz.StopBreathAnimation();
        }
        
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.ClearSelectedInfantry();
        
        GD.Print($"[ForceOoziumActed] {oozium.Name} 吞噬完成，强制结束行动！");
    }

    private bool CanMoveToGrid(Grids grid, Infantry requestingUnit, bool checkPassThrough = false)
    {
        if (requestingUnit is Oozium && grid.weapon != null)
            return false;
            
        var otherUnits = grid.infantries.Where(i => i != requestingUnit && IsInstanceValid(i)).ToList();
        if (otherUnits.Count == 0) return true;

        switch (requestingUnit.overlapType)
        {
            case UnitOverlapType.NonOverlapping:
                bool hasEnemy = otherUnits.Any(u => u.team != requestingUnit.team);
                bool hasAlly = otherUnits.Any(u => u.team == requestingUnit.team);
                
                if (hasEnemy)
                {
                    return false;
                }
                else if (hasAlly)
                {
                    var allyTransport = otherUnits.FirstOrDefault(u => u.canTransportUnits && u.maxTransportCapacity > 0 && u.team == requestingUnit.team && u.CanTransportUnit(requestingUnit));
                    if (allyTransport != null)
                    {
                    return true;
                    }

                    return checkPassThrough;
                }
                return false;
                
            case UnitOverlapType.Overlapping:
            case UnitOverlapType.Oozium: 
                return true;
                
            default: 
                return false;
        }
    }

    private bool CanStayOnGrid(Grids grid, Infantry requestingUnit)
    {
        var otherUnits = grid.infantries.Where(i => i != requestingUnit && IsInstanceValid(i)).ToList();
        if (otherUnits.Count == 0) return true;

        var friendlyTransport = otherUnits.FirstOrDefault(u => u.canTransportUnits && u.maxTransportCapacity > 0 && u.team == requestingUnit.team && u.CanTransportUnit(requestingUnit));
        if (friendlyTransport != null)
        {
        return true;
        }


        switch (requestingUnit.overlapType)
        {
            case UnitOverlapType.NonOverlapping:
                return false;
            case UnitOverlapType.Overlapping:
            case UnitOverlapType.Oozium:
                return true;
            default:
                return false;
        }
    }

    public void MoveTo(Infantry infantry, Grids to, bool showMenu = true)
    {
        if (infantry == null || to == null) return;

        bool isOozium = infantry is Oozium;

        if (infantry.overlapType == UnitOverlapType.NonOverlapping)
        {
            var otherUnits = to.infantries.Where(i => i != infantry && IsInstanceValid(i)).ToList();
            if (otherUnits.Count > 0) return;
        }

        if (infantry.overlapType == UnitOverlapType.Oozium)
        {
            var targetsToRemove = to.infantries.Where(i => i != infantry && IsInstanceValid(i)).ToList();
            foreach (var target in targetsToRemove)
                DevourUnit(target);
            to.infantries.Clear();
            to.infantry = null;
        }

        var path = BuildPath(infantry.grid, to);
        if (path.Count == 0) return;

        int totalMoveCost = 0;
        int totalFuelCost = 0;
        bool isFirst = true;
        foreach (var g in path)
        {
            if (isFirst) { isFirst = false; continue; }
            int moveCost = GetActualMoveCost(infantry, g.gridType);
            totalMoveCost += moveCost;
            // ✅ 传送点(TP)不消耗燃料，其他地形正常消耗
            if (infantry.consumeFuel && g.gridType != GridType.TP)
                totalFuelCost += moveCost;
        }

        if (isOozium)
        {
            totalMoveCost = 1;
            totalFuelCost = 0;
        }

        if (isOozium)
        {
            if (infantry.movePoints < 0) return;
        }
        else
        {
            if (infantry.movePoints < totalMoveCost) return;
        }

        if (infantry.consumeFuel && infantry.fuel < totalFuelCost) return;

        if (infantry.originalGrid == null)
            infantry.originalGrid = infantry.grid;

        infantry.movePoints -= totalMoveCost;
        if (infantry.consumeFuel)
            infantry.ConsumeFuel(totalFuelCost);

        infantry.Position = to.Position;

        var fromGrid = infantry.grid;
        if (fromGrid != null)
        {
            fromGrid.infantries.Remove(infantry);
            if (fromGrid.infantry == infantry)
                fromGrid.infantry = fromGrid.infantries.Count > 0 ? fromGrid.infantries[0] : null;
        }

        infantry.grid = to;
        if (!to.infantries.Contains(infantry))
            to.infantries.Add(infantry);
        if (to.infantry == null || infantry.overlapType != UnitOverlapType.Overlapping)
            to.infantry = infantry;

        infantry.state = UnitState.Moved;
        infantry.isMoved = true;

        // ✅ 移动完成后立即刷新战争迷雾
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.fogOfWarManager?.OnUnitMoved();

        CloseRange();
        HideAttackRange();

   var transport = to.infantries.FirstOrDefault(u => 
        u.canTransportUnits 
        && u.maxTransportCapacity > 0 
        && u.team == infantry.team 
        && u != infantry) as Infantry;
    bool hasMountedToTransport = false;

    if (transport != null && transport.CanTransportUnit(infantry))
    {
        var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
        if (actionMenu != null)
        {

            actionMenu.ShowTransportMenu(transport, afterMove: true);
            hasMountedToTransport = true;
        }
    }

    if (!hasMountedToTransport && showMenu)
    {
        SetupEmptyClickForRollback(infantry);
        var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
        actionMenu?.ShowMenu(infantry, true, true);
    }
    else if (!hasMountedToTransport)
    {
        // ⬅️ 不弹菜单，直接待机
        infantry.isAttacked = true;
        infantry.state = UnitState.Acted;
        infantry.SetWaitVisual(true);
        infantry.originalGrid = null;
        gm?.ClearSelectedInfantry();
    }
    }

    // ========== ✅ 新增：移动截断辅助方法 ==========
    private void TruncateAndMove(Infantry infantry, Grids lastValidGrid)
    {
        if (lastValidGrid != infantry.grid && CanStayOnGrid(lastValidGrid, infantry))
        {
            MoveTo(infantry, lastValidGrid, showMenu: false);
        }
        else
        {
            CloseRange();
            var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
            actionMenu?.Hide();
            var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
            gm?.ClearSelectedInfantry();
        }
    }

    // ========== ✅ 新增：移动截断（正序路径检查，遇到不可移动格子截断后待机） ==========
    public void HandleTruncatedMove(Infantry infantry, Grids target)
    {
        var path = BuildPath(infantry.grid, target);
        if (path.Count <= 1) return; // 就在原地，不处理

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;

        // 从起点正序检查路径，找最后一个可停留的格子
        Grids truncatedTarget = infantry.grid;
        for (int i = 1; i < path.Count; i++)
        {
            var g = path[i];
            
            // ✅ 检查1: 敌方单位（NonOverlapping单位才被阻挡）
            if (infantry.overlapType == UnitOverlapType.NonOverlapping)
            {
                g.infantries.RemoveAll(u => u == null || !IsInstanceValid(u));
                var enemies = g.infantries.Where(u => u != infantry && u.team != infantry.team).ToList();
                if (enemies.Count > 0)
                {
                    break; // 截断到上一个可停留格
                }
            }
            
            // ✅ 检查2: 兵器阻挡（非Oozium）
            if (infantry is not Oozium && g.weapon != null)
            {
                break; // 截断到上一个可停留格
            }
            
            // ✅ 检查3: 地形不可移动
            int moveCost = infantry.GetMoveCost(g.gridType);
            if (moveCost >= 999 || moveCost == int.MaxValue)
            {
                break; // 截断到上一个可停留格
            }
            
            // ✅ 检查4: CanMoveTo判定
            if (unitManager?.CanMoveTo(g.GridIndex, infantry) == false)
            {
                break; // 截断到上一个可停留格
            }
            
            if (CanStayOnGrid(g, infantry))
            {
                truncatedTarget = g;
            }
            else
            {
                break; // 截断到上一个可停留的格子
            }
        }

        if (truncatedTarget != infantry.grid)
        {
            GD.Print($"[TruncatedMove] {infantry.Name} 截断至 {truncatedTarget.GridIndex} 后直接待机");
            MoveTo(infantry, truncatedTarget, showMenu: false);
        }
        else
        {
            GD.Print($"[TruncatedMove] 无法截断，取消移动");
            CloseRange();
            var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
            actionMenu?.Hide();
            gm?.ClearSelectedInfantry();
        }
    }

    private int GetActualMoveCost(Infantry infantry, GridType gridType)
    {
        int cost = infantry.GetMoveCost(gridType);
        // ✅ 如果返回 int.MaxValue，表示不可通行，但在路径计算中应该已经被过滤了
        // 这里作为安全检查，返回一个极大值
        return cost >= 999 ? 999 : cost;
    }

    private void DevourUnit(Infantry target)
    {
        if (target == null || !IsInstanceValid(target)) return;
        if (target.grid != null)
        {
            target.grid.infantries.Remove(target);
            if (target.grid.infantry == target)
                target.grid.infantry = null;
            target.grid = null;
        }
        var gameManager = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gameManager?.RemovePiece(target);
        target.QueueFree();
    }

public void CloseRange()
{
    Grids.IsForceActionMode = false;
    if (moveRange == null) return;
    foreach (var grid in moveRange)
    {
        if (grid != null && IsInstanceValid(grid))
        {
            grid.pathIcon?.Hide();
            grid.pathIcon?.SetDeferred("modulate", Colors.White);
            grid.OnClickGrid = null;
            grid.OnClickEmpty = null;
            
            // ✅ 恢复单位输入
            OverrideUnitInput(grid, false);
        }
    }
    moveRange.Clear();
    HideExplosionRange(); // ✅ 关闭移动范围时也清除爆炸预览
}

    public void ClearAllEmptyCallbacks()
    {
        foreach (var grid in grids)
            grid.OnClickEmpty = null;
    }

    public void SetupEmptyClickForRollback(Infantry infantry)
    {
        foreach (var grid in grids)
        {
            grid.OnClickEmpty = () => 
            {
                var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
                var unit = gm?.selectedInfantry;
                
                bool shouldRollback = unit != null && 
                                      unit.state == UnitState.Moved && 
                                      unit.originalGrid != null;

                if (shouldRollback)
                {
                    gm.RollbackMove();
                }
                else
                {
                    CloseRange();
                    HideAttackRange();
                    ClearWeaponRange();
                    var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
                    actionMenu?.CancelExplosionPreview(); // ✅ 取消爆炸预览
                    actionMenu?.Hide();
                    gm?.ClearSelectedInfantry();
                }
            };
        }
    }

    public void RemoveUnit(Infantry unit)
    {
        if (unit == null) return;
        if (unit.grid != null)
        {
            unit.grid.infantry = null;
            unit.grid = null;
            var gameManager = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
            gameManager?.RemovePiece(unit);
        }
        unit.QueueFree();
    }

    public void SetGrid(Infantry infantry)
    {
        int xindex = (int)(infantry.Position.X - startPos.X) / gridSize.X;
        int yindex = (int)(infantry.Position.Y - startPos.Y) / gridSize.Y;
        if((xindex >= 0) && (xindex < searchRange.X) &&
            (yindex >= 0) && (yindex < searchRange.Y))
        {
            if(map[xindex, yindex] != null)
            {
                if (map[xindex, yindex].infantries.Count > 0 && 
                    infantry.overlapType == UnitOverlapType.NonOverlapping)
                    return;

                infantry.grid = map[xindex, yindex];
                map[xindex, yindex].infantries.Add(infantry);
            }
        }
    }

    public List<Facility> GetAllCities()
    {
        var cities = new List<Facility>();
        foreach (var grid in grids)
        {
            if (grid != null && grid.city != null)
            {
                cities.Add(grid.city);
            }
        }
        return cities;
    }

    public void PerformTurnSupply()
    {
        var cities = GetAllCities();
        foreach (var city in cities)
            city.PerformSupply();
    }

    public List<Grids> attackRange = new List<Grids>();
    public bool IsShowingAttackRange => attackRange.Count > 0;
    public List<Grids> explosionRange = new List<Grids>(); // ✅ 爆炸范围
    public List<Grids> flareRange = new List<Grids>(); // ✅ 照明弹射程范围（绿色）
    public List<Grids> illuminationRange = new List<Grids>(); // ✅ 照明覆盖范围（黄色）
    
    // ========== ✅ 爆炸范围显示 ==========
    public void ShowExplosionRange(Infantry unit, int minR, int maxR)
    {
        HideExplosionRange();
        if (unit?.grid == null) return;

        var allRangeGrids = FindRange(unit.grid, maxR, false);
        List<Grids> innerGrids = minR > 0 ? FindRange(unit.grid, minR - 1, false) : new List<Grids>();

        foreach (var grid in allRangeGrids)
        {
            if (innerGrids.Contains(grid)) continue; // 排除内圈（形成环形）
            explosionRange.Add(grid);
            if (grid.attackRangeIcon != null)
            {
                grid.attackRangeIcon.Modulate = new Color(1f, 0.3f, 0f, 0.9f); // 橙红色
                grid.attackRangeIcon.Show();
            }
        }
    }

    public void HideExplosionRange()
    {
        foreach (var grid in explosionRange)
        {
            if (grid?.attackRangeIcon != null)
            {
                grid.attackRangeIcon.Modulate = Colors.White; // 恢复默认颜色
                grid.attackRangeIcon.Hide();
            }
        }
        explosionRange.Clear();
    }

    // ========== ✅ 照明弹射程范围显示（绿色）==========
    public void ShowFlareRange(Infantry unit, int minR, int maxR)
    {
        HideFlareRange();
        if (unit?.grid == null) return;

        var allRangeGrids = FindRange(unit.grid, maxR, false);
        List<Grids> innerGrids = minR > 0 ? FindRange(unit.grid, minR - 1, false) : new List<Grids>();

        foreach (var grid in allRangeGrids)
        {
            if (innerGrids.Contains(grid)) continue;
            flareRange.Add(grid);
            if (grid.attackRangeIcon != null)
            {
                grid.attackRangeIcon.Modulate = new Color(0f, 1f, 0f, 0.7f); // 绿色
                grid.attackRangeIcon.Show();
            }
        }
    }

    public void HideFlareRange()
    {
        foreach (var grid in flareRange)
        {
            if (grid?.attackRangeIcon != null)
            {
                grid.attackRangeIcon.Modulate = Colors.White;
                grid.attackRangeIcon.Hide();
            }
        }
        flareRange.Clear();
    }

    // ========== ✅ 照明覆盖范围显示（黄色，以目标为中心）==========
    public void ShowIlluminationRange(Grids center, int minR, int maxR)
    {
        HideIlluminationRange();
        if (center == null) return;

        var allRangeGrids = FindRange(center, maxR, false);
        List<Grids> innerGrids = minR > 0 ? FindRange(center, minR - 1, false) : new List<Grids>();

        foreach (var grid in allRangeGrids)
        {
            if (innerGrids.Contains(grid)) continue;
            illuminationRange.Add(grid);
            if (grid.attackRangeIcon != null)
            {
                grid.attackRangeIcon.Modulate = new Color(1f, 1f, 0f, 0.7f); // 黄色
                grid.attackRangeIcon.Show();
            }
        }
    }

    public void HideIlluminationRange()
    {
        foreach (var grid in illuminationRange)
        {
            if (grid?.attackRangeIcon != null)
            {
                grid.attackRangeIcon.Modulate = Colors.White;
                grid.attackRangeIcon.Hide();
            }
        }
        illuminationRange.Clear();
    }
    
    public void ShowMinMaxAttackRange(Infantry unit)
    {
        if (unit.attackType == AttackType.NoAttack) return;
        if (unit.hasPrimaryWeapon && !unit.CanUsePrimaryWeapon()) return;
        if (!unit.canAttackAfterMoving && unit.state == UnitState.Moved)
        {
            return;
        }
        
        CloseRange();
        HideAttackRange();
        
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm != null) gm.isSelectingAttackTarget = true;
        var fog = gm?.fogOfWarManager;
        bool isFogEnabled = fog != null && fog.isFogOfWarEnabled;
        
        attackRange.Clear();
        
        int minRange = unit.minAttackRange;
        int maxRange = unit.maxAttackRange;
        
        var allRangeGrids = FindRange(unit.grid, maxRange, false);
        var blindZoneGrids = new HashSet<Grids>();
        
        blindZoneGrids.Add(unit.grid);
            foreach (var grid in allRangeGrids)
    {
        if (blindZoneGrids.Contains(grid)) continue;
        
        bool hasEnemyTarget = grid.HasEnemyInfantry(unit) || grid.HasEnemyWeapon(unit.team);
        if (hasEnemyTarget)
        {
            if (isFogEnabled && !fog.IsGridVisible(grid)) continue;
            attackRange.Add(grid);
            grid.attackRangeIcon?.Show();
            grid.IsInAttackRangeMode = true;
            
            // ✅ 强制攻击回调
            grid.OnClickGrid = to => OnAttackGridClicked(unit, to);
            OverrideUnitInput(grid, true); // 禁用单位选择
            
            grid.OnMouseEnteredAttackGrid = () => ShowDamagePreview(unit, grid);
            grid.OnMouseExitedAttackGrid = () => HideDamagePreview();
        }
    }
        if (minRange > 1)
        {
            var nearGrids = FindRange(unit.grid, minRange - 1, false);
            foreach (var g in nearGrids) 
                blindZoneGrids.Add(g);
        }
        
        foreach (var grid in allRangeGrids)
        {
            if (blindZoneGrids.Contains(grid)) continue;
            if (grid.weapon != null && grid.weapon.team != unit.team)
            {
                if (isFogEnabled && !fog.IsGridVisible(grid)) continue;
                attackRange.Add(grid);
                grid.attackRangeIcon?.Show();
                grid.IsInAttackRangeMode = true;
                grid.OnClickGrid = to => OnAttackGridClicked(unit, to);
                grid.OnMouseEnteredAttackGrid = () => ShowDamagePreview(unit, grid);
                grid.OnMouseExitedAttackGrid = () => HideDamagePreview();
                continue;
            }
            if (grid.weapon != null && grid.weapon.team != unit.team && !grid.weapon.hasActed)
            {
                if (isFogEnabled && !fog.IsGridVisible(grid)) continue;
                attackRange.Add(grid);
                grid.attackRangeIcon?.Show();
                grid.IsInAttackRangeMode = true;
                var area2D = grid.GetNodeOrNull<Area2D>("Area2D");
                if (area2D != null) { area2D.ZIndex = 10; area2D.InputPickable = true; }

                grid.OnClickGrid = to => OnAttackGridClicked(unit, to);
                grid.OnMouseEnteredAttackGrid = () => ShowDamagePreview(unit, grid);
                grid.OnMouseExitedAttackGrid = () => HideDamagePreview();
            }
            
            bool hasEnemyTarget = grid.HasEnemyInfantry(unit) || grid.HasEnemyWeapon(unit.team);
            if (hasEnemyTarget)
            {
                if (isFogEnabled && !fog.IsGridVisible(grid)) continue;
                attackRange.Add(grid);
                grid.attackRangeIcon?.Show();
                grid.IsInAttackRangeMode = true;
                var area2D = grid.GetNodeOrNull<Area2D>("Area2D");
                if (area2D != null)
                {
                    area2D.ZIndex = 10;
                    area2D.InputPickable = true;
                }
                
                grid.OnClickGrid = to => OnAttackGridClicked(unit, to);
                grid.OnMouseEnteredAttackGrid = () => ShowDamagePreview(unit, grid);
                grid.OnMouseExitedAttackGrid = () => HideDamagePreview();
            }
        }
        
        if (attackRange.Count == 0)
        {
            unit.actionMenu?.Hide();
            if (unit.state == UnitState.Moved && unit.originalGrid != null)
                gm?.RollbackMove();
        }
    }

    public void ShowAttackRange(Infantry unit)
    {   
        Grids.IsForceActionMode = true;
        if (unit.attackType == AttackType.NoAttack) return;
        var terrainEditor = GetTree().GetFirstNodeInGroup("terrain_editor") as TerrainEditor;
    if (terrainEditor != null && terrainEditor.ShouldBlockUnitOperations())
    {
        return;
    }
        CloseRange();
        HideAttackRange();
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm != null) gm.isSelectingAttackTarget = true;
        var fog = gm?.fogOfWarManager;
        bool isFogEnabled = fog != null && fog.isFogOfWarEnabled;

        attackRange.Clear();
        
        if (unit.useMinMaxAttackRange)
        {
            ShowMinMaxAttackRange(unit);
            return;
        }
        
        if (unit.attackType == AttackType.NoAttack) return;
        var neighbors = GetAttackNeighbours(unit.grid);
        
        // ✅ 禁用所有单位选择，防止误选己方未行动单位
        foreach (var grid in grids)
        {
            OverrideUnitInput(grid, true);
        }
        
        foreach (var neighbor in neighbors)
        {
            if (neighbor.weapon != null && neighbor.weapon.team != unit.team && !neighbor.weapon.hasActed)
            {
                if (isFogEnabled && fog != null && !fog.IsGridVisible(neighbor)) continue;
                attackRange.Add(neighbor);
                neighbor.attackRangeIcon?.Show();
                neighbor.IsInAttackRangeMode = true;
                var area2D = neighbor.GetNodeOrNull<Area2D>("Area2D");
                if (area2D != null) { area2D.ZIndex = 10; area2D.InputPickable = true; }

                neighbor.OnClickGrid = to => OnAttackGridClicked(unit, to);
                neighbor.OnMouseEnteredAttackGrid = () => ShowDamagePreview(unit, neighbor);
                neighbor.OnMouseExitedAttackGrid = () => HideDamagePreview();
            }
            
            if (neighbor.HasEnemyInfantry(unit))
            {
                if (isFogEnabled && fog != null && !fog.IsGridVisible(neighbor)) continue;
                attackRange.Add(neighbor);
                neighbor.attackRangeIcon?.Show();
                neighbor.IsInAttackRangeMode = true;
                var area2D = neighbor.GetNodeOrNull<Area2D>("Area2D");
                if (area2D != null)
                {
                    area2D.ZIndex = 10;
                    area2D.InputPickable = true;
                }
                
                neighbor.OnClickGrid = to => OnAttackGridClicked(unit, to);
                neighbor.OnMouseEnteredAttackGrid = () => ShowDamagePreview(unit, neighbor);
                neighbor.OnMouseExitedAttackGrid = () => HideDamagePreview();
            }
        }

        if (unit.overlapType == UnitOverlapType.Overlapping && unit.grid.HasEnemyInfantry(unit))
        {
            if (isFogEnabled && fog != null && !fog.IsGridVisible(unit.grid)) { /* 不添加 */ }
            else
            {
                attackRange.Add(unit.grid);
                unit.grid.attackRangeIcon?.Show();
                unit.grid.OnClickGrid = to => OnAttackGridClicked(unit, to);
                unit.grid.OnMouseEnteredAttackGrid = () => ShowDamagePreview(unit, unit.grid);
                unit.grid.OnMouseExitedAttackGrid = () => HideDamagePreview();
            }
        }

        // ✅ 攻击范围外点击回退（包括所有格子）
        foreach (var grid in grids)
        {
            var capturedUnit = unit;
            
            if (!attackRange.Contains(grid))
            {
                // ✅ 范围外格子点击触发回退
                grid.OnClickEmpty = () => HandleAttackRangeOutsideClick(unit);
            }
        }

        if (attackRange.Count == 0)
        {
            unit.actionMenu?.Hide();
            gm?.RollbackMove();
        }
    }
    
    // ✅ 新增：攻击范围外点击处理
    private void HandleAttackRangeOutsideClick(Infantry unit)
    {
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        
        bool shouldRollback = gm?.selectedInfantry != null && 
                              gm.selectedInfantry.state == UnitState.Moved && 
                              gm.selectedInfantry.originalGrid != null;

        if (shouldRollback)
        {
            gm.RollbackMove();
            HideAttackRange();
        }
        else
        {
            HideDamagePreview();
            HideAttackRange();
            CloseRange();
            unit.actionMenu?.Hide();
            gm?.ClearSelectedInfantry();
        }
    }
   public void OverrideUnitInput(Grids grid, bool overrideInput)
{
    if (grid == null) return;
    
    foreach (var u in grid.infantries)
    {
        if (u == null || !IsInstanceValid(u)) continue;
        
        var area2D = u.GetNodeOrNull<Area2D>("Area2D");
        if (area2D != null)
        {
            area2D.InputPickable = !overrideInput; // 覆盖时禁用，恢复时启用
        }
    }
    
    // 同样处理兵器
    if (grid.weapon != null && IsInstanceValid(grid.weapon))
    {
        var area2D = grid.weapon.GetNodeOrNull<Area2D>("Area2D");
        if (area2D != null)
        {
            area2D.InputPickable = !overrideInput;
        }
    }
}
 

    public void OnAttackGridClicked(Infantry attacker, Grids to)
    {
        if (to == attacker.grid && to.infantry == attacker)
        {
            var enemies = to.GetEnemyInfantries(attacker);
            if (enemies.Count == 0) return;
        }
        
        Weapon targetWeapon = null;
        if (to.weapon != null && to.weapon.team != attacker.team)
            targetWeapon = to.weapon;

        if (targetWeapon != null)
        {
            attacker.AttackTarget(targetWeapon); 
        }
        else
        {
            Infantry target = null;
            if (to == attacker.grid)
            {
                var enemies = to.GetEnemyInfantries(attacker);
                if (enemies.Count > 0) target = enemies[0];
            }
            else
            {
                var enemies = to.GetEnemyInfantries(attacker);
                if (enemies.Count > 0) target = enemies[0];
            }

            if (target == null)
            {
                HideAttackRange();
                attacker.actionMenu.Hide();
                var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
                gm?.RollbackMove();
                return;
            }

            attacker.originalGrid = null;
            
            var gameManager = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
            gameManager?.OnAttack(attacker, target);
        }
    }

    
    // ✅ 新增：支持 attackRange 的扩展射程（非minMax模式）
    public void ShowExtendedAttackRange(Infantry unit)
    {
        if (unit.attackType == AttackType.NoAttack) return;
        if (unit.hasPrimaryWeapon && !unit.CanUsePrimaryWeapon()) return;
        if (!unit.canAttackAfterMoving && unit.state == UnitState.Moved)
        {
            return;
        }

        CloseRange();
        HideAttackRange();

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm != null) gm.isSelectingAttackTarget = true;
        var fog = gm?.fogOfWarManager;
        bool isFogEnabled = fog != null && fog.isFogOfWarEnabled;

        attackRange.Clear();

        int range = unit.attackRange;
        var allRangeGrids = FindRange(unit.grid, range, false);

        foreach (var grid in allRangeGrids)
        {
            if (grid == null || grid == unit.grid) continue;

            // 检查敌方兵器
            if (grid.weapon != null && grid.weapon.team != unit.team && !grid.weapon.hasActed)
            {
                if (isFogEnabled && fog != null && !fog.IsGridVisible(grid)) continue;
                attackRange.Add(grid);
                grid.attackRangeIcon?.Show();
                grid.IsInAttackRangeMode = true;
                var area2D = grid.GetNodeOrNull<Area2D>("Area2D");
                if (area2D != null) { area2D.ZIndex = 10; area2D.InputPickable = true; }
                grid.OnClickGrid = to => OnAttackGridClicked(unit, to);
                grid.OnMouseEnteredAttackGrid = () => ShowDamagePreview(unit, grid);
                grid.OnMouseExitedAttackGrid = () => HideDamagePreview();
                continue;
            }

            // 检查敌方单位
            if (grid.HasEnemyInfantry(unit))
            {
                if (isFogEnabled && fog != null && !fog.IsGridVisible(grid)) continue;
                attackRange.Add(grid);
                grid.attackRangeIcon?.Show();
                grid.IsInAttackRangeMode = true;
                var area2D = grid.GetNodeOrNull<Area2D>("Area2D");
                if (area2D != null)
                {
                    area2D.ZIndex = 10;
                    area2D.InputPickable = true;
                }
                grid.OnClickGrid = to => OnAttackGridClicked(unit, to);
                grid.OnMouseEnteredAttackGrid = () => ShowDamagePreview(unit, grid);
                grid.OnMouseExitedAttackGrid = () => HideDamagePreview();
            }
        }

        // Overlapping 单位检查同格敌人
        if (unit.overlapType == UnitOverlapType.Overlapping && unit.grid.HasEnemyInfantry(unit))
        {
            if (isFogEnabled && fog != null && !fog.IsGridVisible(unit.grid)) { /* 不添加 */ }
            else
            {
                attackRange.Add(unit.grid);
                unit.grid.attackRangeIcon?.Show();
                unit.grid.OnClickGrid = to => OnAttackGridClicked(unit, to);
                unit.grid.OnMouseEnteredAttackGrid = () => ShowDamagePreview(unit, unit.grid);
                unit.grid.OnMouseExitedAttackGrid = () => HideDamagePreview();
            }
        }

        // 空地点击处理
        foreach (var grid in grids)
        {
            var capturedUnit = unit;
            grid.OnClickEmpty = () => 
            {
                var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
                bool shouldRollback = gm?.selectedInfantry != null && 
                                      gm.selectedInfantry.state == UnitState.Moved && 
                                      gm.selectedInfantry.originalGrid != null;
                if (shouldRollback)
                {
                    gm.RollbackMove();
                    HideAttackRange();
                }
                else
                {
                    HideDamagePreview();
                    HideAttackRange();
                    CloseRange();
                    capturedUnit.actionMenu?.Hide();
                    gm?.ClearSelectedInfantry();
                }
            };
        }

        if (attackRange.Count == 0)
        {
            unit.actionMenu?.Hide();
            gm?.RollbackMove();
        }
    }

public void HideAttackRange()
{
    Grids.IsForceActionMode = false;
    HideDamagePreview();

    foreach (var g in attackRange)
    {
        if (g != null && IsInstanceValid(g))
        {
            g.IsInAttackRangeMode = false;
            
            // ✅ 恢复单位输入
            OverrideUnitInput(g, false);
            
            if (g.attackRangeIcon != null && IsInstanceValid(g.attackRangeIcon))
                g.attackRangeIcon.Hide();
            g.OnClickGrid = null;
            g.OnClickEmpty = null;
            g.OnMouseEnteredAttackGrid = null;
            g.OnMouseExitedAttackGrid = null;
        }
    }
    attackRange.Clear();
    HideExplosionRange(); // ✅ 关闭攻击范围时也清除爆炸预览

        
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm != null) gm.isSelectingAttackTarget = false;
        
        foreach (var grid in grids)
            grid.OnClickEmpty = null;
    }

    private void ShowDamagePreview(Infantry attacker, Grids targetGrid)
    {
        if (damagePreviewLabel == null) return;
        
        var target = targetGrid.GetEnemyInfantries(attacker)
            .OrderBy(u => u.health)
            .FirstOrDefault();
        
        if (target == null) return;
        
        var (damage, attackInfo) = attacker.CalculateDamagePreview(target);
        var (counterDamage, counterInfo) = target.CalculateCounterPreview(attacker);
        
        bool targetWillDie = target.health - damage <= 0;
        bool attackerWillDie = !targetWillDie && attacker.health - counterDamage <= 0;
        
        string text = "";
        
        if (targetWillDie)
            text += $"💀 {damage} (击杀!)";
        else
            text += $"⚔️ {damage}";
        
        if (!targetWillDie && counterDamage > 0)
        {
            if (attackerWillDie)
                text += $"\n💀 反击{counterDamage} (同归于尽!)";
            else
                text += $"\n🛡️ 反击{counterDamage}";
        }
        else if (!targetWillDie && counterDamage == 0)
            text += "\n🛡️ 无反击";
        
        damagePreviewLabel.Text = text;
        
        damagePreviewLabel.TopLevel = true;
        damagePreviewLabel.GlobalPosition = targetGrid.GlobalPosition + new Godot.Vector2(-30, -60);
        
        damagePreviewLabel.Show();
        damagePreviewLabel.QueueRedraw();
    }

    private void HideDamagePreview()
    {
        damagePreviewLabel?.Hide();
    }

    public void SetEmptyClickToCloseMenu(Infantry unit)
    {
        foreach (var grid in grids)
            grid.OnClickEmpty = null;

        foreach (var grid in grids)
        {
            grid.OnClickEmpty = () => 
            {
                var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
                actionMenu?.Hide();
                var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
                gm?.ClearSelectedInfantry();
                foreach (var g in grids)
                    g.OnClickEmpty = null;
            };
        }
    }
}
