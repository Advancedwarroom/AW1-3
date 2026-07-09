// AI_Manager.cs - 战棋AI管理器（类似AW原版AI）
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class AI_Manager : Node
{
    [Export] public GameManager gameManager;

    public bool p1AIEnabled = false;
    public bool p2AIEnabled = false;
    public bool autoEndTurn = false;

    private bool isRunningAI = false;
    private int aiUnitIndex = 0;
    private List<object> aiActionQueue = new();
    private Timer aiTimer;

    public override void _Ready()
    {
        AddToGroup("ai_manager");
        if (gameManager == null)
            gameManager = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;

        aiTimer = new Timer();
        aiTimer.WaitTime = 0.15f;
        aiTimer.OneShot = true;
        aiTimer.Timeout += OnAIStep;
        AddChild(aiTimer);
    }

    public bool IsAITeam(string team)
    {
        return (team == TeamHelper.Player1 && p1AIEnabled)
            || (team == TeamHelper.Player2 && p2AIEnabled);
    }

    public bool IsCurrentPhaseAI()
    {
        string phaseTeam = TeamHelper.GetPhaseTeamName(gameManager?.turnPhase ?? 1);
        return IsAITeam(phaseTeam);
    }

    // 在 GameManager.NextPhase 的末尾调用
    public void OnPhaseStart()
    {
        if (gameManager == null) return;
        string phaseTeam = TeamHelper.GetPhaseTeamName(gameManager.turnPhase);
        if (!IsAITeam(phaseTeam)) return;

        GD.Print($"[AI] {phaseTeam} AI 回合开始");
        isRunningAI = true;
        aiUnitIndex = 0;
        BuildAIQueue(phaseTeam);
        if (aiActionQueue.Count > 0)
            aiTimer.Start();
        else
            TryAutoEndTurn();
    }

    private void BuildAIQueue(string team)
    {
        aiActionQueue.Clear();

        // 兵器优先
        var weapons = gameManager.weaponManager?.AllWeapons
            ?.Where(w => IsInstanceValid(w) && w.team == team && w.CanAttack())
            .ToList() ?? new List<Weapon>();
        foreach (var w in weapons) aiActionQueue.Add(w);

        // 然后单位
        var units = gameManager.unitManager?.AllUnits
            ?.Where(u => IsInstanceValid(u) && u.team == team && u.state != UnitState.Acted)
            .ToList() ?? new List<Infantry>();
        foreach (var u in units) aiActionQueue.Add(u);
    }

    private void OnAIStep()
    {
        if (!isRunningAI || aiUnitIndex >= aiActionQueue.Count)
        {
            isRunningAI = false;
            TryAutoEndTurn();
            return;
        }

        var target = aiActionQueue[aiUnitIndex];
        aiUnitIndex++;

        if (target is Weapon weapon && IsInstanceValid(weapon))
        {
            if (weapon.CanAttack())
            {
                GD.Print($"[AI] 兵器行动: {weapon.Name}");
                weapon.ExecuteAI();
            }
        }
        else if (target is Infantry unit && IsInstanceValid(unit))
        {
            if (unit.state != UnitState.Acted)
            {
                GD.Print($"[AI] 单位行动: {unit.Name}");
                ExecuteUnitAI(unit);
            }
        }

        // 检查是否继续
        if (aiUnitIndex < aiActionQueue.Count)
            aiTimer.Start();
        else
        {
            isRunningAI = false;
            TryAutoEndTurn();
        }
    }

    private void TryAutoEndTurn()
    {
        if (!autoEndTurn || gameManager == null) return;
        string phaseTeam = TeamHelper.GetPhaseTeamName(gameManager.turnPhase);
        if (!IsAITeam(phaseTeam)) return;

        // 延迟检查是否还有未行动的单位
        var checkTimer = GetTree().CreateTimer(0.5f);
        checkTimer.Timeout += () =>
        {
            if (!HasPendingActions(phaseTeam))
            {
                GD.Print($"[AI] {phaseTeam} 无剩余行动，自动切换回合");
                gameManager.NextPhase();
            }
        };
    }

    private bool HasPendingActions(string team)
    {
        // 检查兵器
        if (gameManager.weaponManager?.AllWeapons != null)
            foreach (var w in gameManager.weaponManager.AllWeapons)
                if (IsInstanceValid(w) && w.team == team && w.CanAttack())
                    return true;
        // 检查单位
        if (gameManager.unitManager?.AllUnits != null)
            foreach (var u in gameManager.unitManager.AllUnits)
                if (IsInstanceValid(u) && u.team == team && u.state != UnitState.Acted)
                    return true;
        return false;
    }

    // ========== 单位AI核心逻辑 ==========
    private void ExecuteUnitAI(Infantry unit)
    {
        if (unit == null || !IsInstanceValid(unit)) return;
        var gm = gameManager;
        var gridMgr = gm?.gridManager;
        if (gridMgr == null) return;

        // 1. 检查是否只剩APC（无战斗单位）→ 躲避
        if (IsOnlyAPCRemaining(unit.team))
        {
            AIEvade(unit);
            return;
        }

        // 2. 如果是APC（运输车），不是只剩APC的情况 → 向敌人前线靠近（搭载步兵）
        if (unit is APC)
        {
            AITransportMove(unit);
            return;
        }

        // 3. 战斗单位AI：先找可攻击的敌人
        var enemies = GetAllEnemyUnitsAndWeapons(unit.team);
        if (enemies.Count == 0)
        {
            // 无敌人：向最近城市移动占领
            AICaptureMove(unit);
            return;
        }

        // 找到最佳攻击目标（优先：可击杀 > 血量最低 > 最近）
        var bestTarget = FindBestAttackTarget(unit, enemies);
        if (bestTarget != null)
        {
            // 检查是否已经在攻击范围内
            if (IsInAttackRange(unit, bestTarget))
            {
                AIAttack(unit, bestTarget);
                return;
            }

            // 尝试移动到可攻击范围内
            var moveTarget = FindMoveToAttackPosition(unit, bestTarget);
            if (moveTarget != null && moveTarget != unit.grid)
            {
                AIMove(unit, moveTarget);
                // 移动后如果还能攻击
                if (unit.state == UnitState.Moved && !unit.isAttacked && IsInAttackRange(unit, bestTarget))
                {
                    AIAttack(unit, bestTarget);
                }
                else if (unit.state == UnitState.Moved && !unit.isAttacked)
                {
                    // 移动后仍不能攻击，找新目标
                    var newTarget = FindBestAttackTarget(unit, enemies);
                    if (newTarget != null && IsInAttackRange(unit, newTarget))
                        AIAttack(unit, newTarget);
                    else
                        AIEndTurn(unit);
                }
                return;
            }
        }

        // 4. 无法攻击：占领或待机
        AICaptureMove(unit);
    }

    // ========== AI子方法 ==========

    private bool IsOnlyAPCRemaining(string team)
    {
        var units = gameManager.unitManager?.AllUnits
            ?.Where(u => IsInstanceValid(u) && u.team == team).ToList() ?? new List<Infantry>();
        if (units.Count == 0) return false;
        return units.All(u => u is APC || u.attackType == AttackType.NoAttack);
    }

    private void AIEvade(Infantry unit)
    {
        // APC躲避：向离敌人最远的方向移动
        var enemies = GetAllEnemyUnitsAndWeapons(unit.team);
        if (enemies.Count == 0 || unit.grid == null)
        {
            AIEndTurn(unit);
            return;
        }

        // 计算所有敌人重心
        float avgX = 0, avgY = 0;
        foreach (var e in enemies)
        {
            var g = e is Infantry i ? i.grid : (e is Weapon w ? w.grid : null);
            if (g != null) { avgX += g.GridIndex.X; avgY += g.GridIndex.Y; }
        }
        avgX /= enemies.Count;
        avgY /= enemies.Count;

        // 找到移动范围内离敌人重心最远的格子
        var moveRange = CalculateAIMoveRange(unit);
        Grids bestGrid = null;
        float maxDist = -1;
        foreach (var g in moveRange)
        {
            float dist = Mathf.Abs(g.GridIndex.X - avgX) + Mathf.Abs(g.GridIndex.Y - avgY);
            if (dist > maxDist) { maxDist = dist; bestGrid = g; }
        }

        if (bestGrid != null && bestGrid != unit.grid)
            AIMove(unit, bestGrid);
        else
            AIEndTurn(unit);
    }

    private void AITransportMove(Infantry unit)
    {
        // APC：向最近的己方步兵移动（搭载），或者向敌人靠近
        var allies = gameManager.unitManager?.AllUnits
            ?.Where(u => IsInstanceValid(u) && u.team == unit.team && u != unit && u is Infantry && !(u is APC))
            .ToList() ?? new List<Infantry>();

        if (allies.Count > 0 && unit.maxTransportCapacity > unit.transportedUnits.Count)
        {
            // 找最近的未搭载步兵
            var nearest = allies.OrderBy(a => Distance(unit.grid, a.grid)).FirstOrDefault();
            if (nearest != null && unit.grid != null)
            {
                var moveRange = CalculateAIMoveRange(unit);
                var targetGrid = moveRange.OrderBy(g => Distance(g, nearest.grid)).FirstOrDefault();
                if (targetGrid != null && targetGrid != unit.grid)
                {
                    AIMove(unit, targetGrid);
                    return;
                }
            }
        }

        // 没有步兵可搭载：向敌人方向靠近（但不要太近）
        var enemies = GetAllEnemyUnitsAndWeapons(unit.team);
        if (enemies.Count > 0 && unit.grid != null)
        {
            var nearestEnemy = enemies.OrderBy(e => Distance(unit.grid, GetEntityGrid(e))).FirstOrDefault();
            if (nearestEnemy != null)
            {
                var enemyGrid = GetEntityGrid(nearestEnemy);
                var moveRange = CalculateAIMoveRange(unit);
                // 保持3格距离
                Grids bestGrid = null;
                float bestScore = float.MinValue;
                foreach (var g in moveRange)
                {
                    float distToEnemy = Distance(g, enemyGrid);
                    float score = -Mathf.Abs(distToEnemy - 3); // 尽量保持3格距离
                    if (score > bestScore) { bestScore = score; bestGrid = g; }
                }
                if (bestGrid != null && bestGrid != unit.grid)
                {
                    AIMove(unit, bestGrid);
                    return;
                }
            }
        }

        AIEndTurn(unit);
    }

    private void AICaptureMove(Infantry unit)
    {
        if (unit.captureAbility != CaptureAbility.CanCapture || unit.grid == null)
        {
            AIEndTurn(unit);
            return;
        }

        // 找最近的可占领城市
        var cities = gameManager.gridManager?.grids
            ?.Where(g => g != null && IsInstanceValid(g) && g.city != null)
            .OrderBy(g => Distance(unit.grid, g))
            .ToList() ?? new List<Grids>();

        foreach (var cityGrid in cities)
        {
            if (cityGrid.city.facilityTeam == unit.team) continue; // 已占领
            var moveRange = CalculateAIMoveRange(unit);
            var target = moveRange.OrderBy(g => Distance(g, cityGrid)).FirstOrDefault();
            if (target != null)
            {
                if (target == unit.grid)
                {
                    // 已经在城市上，执行占领
                    AICapture(unit);
                }
                else
                {
                    AIMove(unit, target);
                }
                return;
            }
        }

        AIEndTurn(unit);
    }

    private void AICapture(Infantry unit)
    {
        if (unit.grid?.city == null) return;
        unit.isMoved = true;
        unit.state = UnitState.Moved;
        unit.PerformCapture();
        AIEndTurn(unit);
    }

    private Infantry FindBestAttackTarget(Infantry unit, List<object> enemies)
    {
        if (enemies.Count == 0) return null;

        // 优先：能击杀的 > 血量最低的 > 最近的
        var validEnemies = enemies
            .Select(e => e is Infantry i ? i : null)
            .Where(i => i != null && IsInstanceValid(i) && i.health > 0)
            .ToList();

        if (validEnemies.Count == 0) return null;

        // 优先攻击可击杀的（估算伤害）
        foreach (var e in validEnemies.OrderBy(e => e.health))
        {
            WeaponType wt = unit.SelectWeaponByMatrix(e);
            if (wt == WeaponType.None) continue;
            int estimatedDmg = unit.CalculateFinalDamage(e, wt);
            if (estimatedDmg >= e.health) return e;
        }

        // 没有可击杀的：优先攻击血量最低的
        return validEnemies.OrderBy(e => e.health).FirstOrDefault();
    }

    private bool IsInAttackRange(Infantry unit, Infantry target)
    {
        if (unit.grid == null || target.grid == null) return false;
        int dist = Mathf.Abs(unit.grid.GridIndex.X - target.grid.GridIndex.X)
                 + Mathf.Abs(unit.grid.GridIndex.Y - target.grid.GridIndex.Y);
        if (unit.useMinMaxAttackRange)
            return dist >= unit.minAttackRange && dist <= unit.maxAttackRange;
        return dist <= unit.attackRange;
    }

    private Grids FindMoveToAttackPosition(Infantry unit, Infantry target)
    {
        if (unit.grid == null || target.grid == null) return null;
        var moveRange = CalculateAIMoveRange(unit);
        Grids best = null;
        int bestDist = int.MaxValue;
        foreach (var g in moveRange)
        {
            int dist = Mathf.Abs(g.GridIndex.X - target.grid.GridIndex.X)
                     + Mathf.Abs(g.GridIndex.Y - target.grid.GridIndex.Y);
            bool canAttack = unit.useMinMaxAttackRange
                ? (dist >= unit.minAttackRange && dist <= unit.maxAttackRange)
                : dist <= unit.attackRange;
            if (canAttack && dist < bestDist) { bestDist = dist; best = g; }
        }
        return best;
    }

    private List<Grids> CalculateAIMoveRange(Infantry unit)
    {
        var result = new List<Grids>();
        if (unit.grid == null || gameManager?.gridManager == null) return result;

        var gridMgr = gameManager.gridManager;
        var open = new Queue<(Grids grid, int cost)>();
        var best = new Dictionary<Grids, int>();
        open.Enqueue((unit.grid, 0));
        best[unit.grid] = 0;

        int effectiveMove = unit.movePoints;
        if (unit.consumeFuel) effectiveMove = Mathf.Min(unit.movePoints, unit.fuel);

        while (open.Count > 0)
        {
            var (cur, accCost) = open.Dequeue();
            if (cur != unit.grid && unit.overlapType == UnitOverlapType.NonOverlapping)
            {
                cur.infantries.RemoveAll(u => u == null || !IsInstanceValid(u));
                var enemyOnCur = cur.infantries.FirstOrDefault(u => u != unit && u.team != unit.team);
                if (enemyOnCur != null) continue;
            }

            for (int d = 0; d < 4; d++)
            {
                var dir = gridMgr.offset[d];
                var nIdx = cur.GridIndex + dir;
                if (nIdx.X < 0 || nIdx.X >= gridMgr.searchRange.X ||
                    nIdx.Y < 0 || nIdx.Y >= gridMgr.searchRange.Y) continue;
                var neighbor = gridMgr.map[nIdx.X, nIdx.Y];
                if (neighbor == null) continue;

                int moveCost = unit.GetMoveCost(neighbor.gridType);
                if (moveCost >= 999 || moveCost == int.MaxValue) continue;

                int newCost = accCost + moveCost;
                if (best.TryGetValue(neighbor, out int old) && newCost >= old) continue;
                if (newCost > effectiveMove) continue;

                if (unit is not Oozium && neighbor.weapon != null) continue;

                if (unit.overlapType == UnitOverlapType.NonOverlapping)
                {
                    neighbor.infantries.RemoveAll(u => u == null || !IsInstanceValid(u));
                    var enemyOnNeighbor = neighbor.infantries.FirstOrDefault(u => u != unit && u.team != unit.team);
                    if (enemyOnNeighbor != null) continue;
                }

                best[neighbor] = newCost;
                open.Enqueue((neighbor, newCost));
                if (!result.Contains(neighbor)) result.Add(neighbor);
            }
        }
        return result;
    }

    private void AIMove(Infantry unit, Grids to)
    {
        if (unit == null || to == null || unit.grid == null) return;
        var gridMgr = gameManager?.gridManager;
        if (gridMgr == null) return;

        // 计算路径和消耗
        var path = gridMgr.BuildPath(unit.grid, to);
        if (path.Count == 0) { AIEndTurn(unit); return; }

        int totalMoveCost = 0;
        int totalFuelCost = 0;
        bool isFirst = true;
        foreach (var g in path)
        {
            if (isFirst) { isFirst = false; continue; }
            int cost = unit.GetMoveCost(g.gridType);
            totalMoveCost += cost;
            if (unit.consumeFuel && g.gridType != GridType.TP)
                totalFuelCost += cost;
        }

        if (unit.movePoints < totalMoveCost) { AIEndTurn(unit); return; }
        if (unit.consumeFuel && unit.fuel < totalFuelCost) { AIEndTurn(unit); return; }

        // 执行移动
        unit.movePoints -= totalMoveCost;
        if (unit.consumeFuel) unit.ConsumeFuel(totalFuelCost);

        var fromGrid = unit.grid;
        fromGrid.infantries.Remove(unit);
        if (fromGrid.infantry == unit)
            fromGrid.infantry = fromGrid.infantries.Count > 0 ? fromGrid.infantries[0] : null;

        unit.Position = to.Position;
        unit.grid = to;
        if (!to.infantries.Contains(unit)) to.infantries.Add(unit);
        if (to.infantry == null || unit.overlapType != UnitOverlapType.Overlapping)
            to.infantry = unit;

        unit.state = UnitState.Moved;
        unit.isMoved = true;
        unit.originalGrid = null;

        gameManager?.fogOfWarManager?.OnUnitMoved();
        GD.Print($"[AI] {unit.Name} 移动到 {to.GridIndex}");
    }

    private void AIAttack(Infantry unit, Infantry target)
    {
        if (unit == null || target == null || !IsInstanceValid(target)) return;
        if (!IsInAttackRange(unit, target)) { AIEndTurn(unit); return; }

        GD.Print($"[AI] {unit.Name} 攻击 {target.Name}");
        unit.Attack(target);

        // 更新UI
        gameManager?.gridManager?.HideAttackRange();
        gameManager?.ClearSelectedInfantry();
        gameManager?.CallDeferred(nameof(gameManager.UpdateUnitLists));
    }

    private void AIEndTurn(Infantry unit)
    {
        if (unit == null || !IsInstanceValid(unit)) return;
        unit.isAttacked = true;
        if (unit.state != UnitState.Acted)
        {
            unit.state = UnitState.Acted;
            unit.SetWaitVisual(true);
        }
        unit.originalGrid = null;
        GD.Print($"[AI] {unit.Name} 结束行动");
    }

    private List<object> GetAllEnemyUnitsAndWeapons(string team)
    {
        var result = new List<object>();
        if (gameManager?.unitManager?.AllUnits != null)
            foreach (var u in gameManager.unitManager.AllUnits)
                if (IsInstanceValid(u) && u.health > 0 && TeamHelper.IsEnemyForAttacker(team, u.team))
                    result.Add(u);
        if (gameManager?.weaponManager?.AllWeapons != null)
            foreach (var w in gameManager.weaponManager.AllWeapons)
                if (IsInstanceValid(w) && w.health > 0 && TeamHelper.IsEnemyForAttacker(team, w.team))
                    result.Add(w);
        return result;
    }

    private Grids GetEntityGrid(object entity)
    {
        if (entity is Infantry i) return i.grid;
        if (entity is Weapon w) return w.grid;
        return null;
    }

    private int Distance(Grids a, Grids b)
    {
        if (a == null || b == null) return int.MaxValue;
        return Mathf.Abs(a.GridIndex.X - b.GridIndex.X) + Mathf.Abs(a.GridIndex.Y - b.GridIndex.Y);
    }
}
