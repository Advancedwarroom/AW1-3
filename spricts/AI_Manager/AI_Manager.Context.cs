// AI_Manager.cs —— 战棋 AI（效用评估 + 威胁图 + 迷雾记忆 + 全技能）
//
// 决策流程（每个 AI 小回合）：
//   1. 兵器（Weapon.ExecuteAI）逐个行动
//   2. 战术阶段：每一步为所有未行动单位重新评估"最佳方案"，先执行高价值战术（击杀/攻击/自爆/照明/完成占领/合流/修理）
//   3. 机动阶段：剩余单位按 照明→远程→近战→占领→运输→补给/修理 的顺序推进或撤退
//   4. 生产：按敌军构成反制选兵 + 占领需求 + 兵种多样性
//   5. 立即可动的新兵再走一轮机动
//
// 公平性：只用本势力视野（CalculateGlobalVisionPool + 照明弹 + 隐身规则 IsVisibleTo），不读取雾中/隐身单位。
//        被雾中或隐身敌军挡路时与玩家一样触发截断（遭遇）。
// 对外接口保持不变：gameManager / p1AIEnabled / p2AIEnabled / autoEndTurn / IsAITeam / IsCurrentPhaseAI / OnPhaseStart
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// AI_Manager · Context（由 AI_Manager.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class AI_Manager
{

    // ==================== 上下文（视野/威胁图/记忆） ====================
    private void BuildContext()
    {
        var gm = gameManager;
        var fog = gm.fogOfWarManager;
        ctx = new Ctx
        {
            team = aiTeam,
            day = gm.turns,
            fogOn = fog != null && fog.isFogOfWarEnabled
        };
        if (ctx.fogOn) ctx.vision = fog.CalculateGlobalVisionPool(aiTeam) ?? new HashSet<Grids>();

        var all = (gm.unitManager?.AllUnits ?? new List<Infantry>())
            .Where(u => u != null && IsInstanceValid(u) && !u.IsQueuedForDeletion() && u.health > 0).ToList();
        ctx.mine = all.Where(u => u.team == aiTeam && !u.isTransported && u.grid != null).ToList();
        ctx.enemies = all.Where(u => !u.isTransported && u.grid != null && IsEnemy(u.team) && CanSee(u)).ToList();
        ctx.enemyWeapons = (gm.weaponManager?.AllWeapons ?? new List<Weapon>())
            .Where(w => w != null && IsInstanceValid(w) && !w.isDestroyed && w.grid != null && IsEnemy(w.team)
                        && (!ctx.fogOn || WeaponGrids(w).Any(g => SeesGrid(g)))).ToList();

        // 记忆：更新可见，保留雾中最近见过的
        if (!memory.TryGetValue(aiTeam, out var mem)) memory[aiTeam] = mem = new Dictionary<Infantry, Memo>();
        foreach (var e in ctx.enemies) mem[e] = new Memo { grid = e.grid, day = ctx.day, hp = e.health };
        foreach (var k in mem.Keys.ToList())
        {
            if (k == null || !IsInstanceValid(k) || k.health <= 0 || ctx.day - mem[k].day > memoryDays) { mem.Remove(k); continue; }
            if (ctx.enemies.Contains(k)) continue;
            // 记忆格已在视野内却看不到 → 它已离开（或隐身），记忆降权：只保留位置用于推进
            ctx.remembered.Add((k, mem[k].grid, mem[k].hp));
        }

        // 威胁图：每个可见敌军下回合能打到的格子
        foreach (var e in ctx.enemies)
        {
            if (e.attackType == AttackType.NoAttack) continue;
            foreach (var g in ThreatTiles(e, e.grid))
            {
                if (!ctx.threat.TryGetValue(g, out var list)) ctx.threat[g] = list = new List<Infantry>();
                list.Add(e);
            }
        }
        foreach (var (u, g, _) in ctx.remembered)
        {
            if (u.attackType == AttackType.NoAttack || g == null) continue;
            foreach (var t in ThreatTiles(u, g)) ctx.rememberedThreat.Add(t);
        }

        // 据点
        foreach (var g in gm.gridManager.grids)
        {
            if (g?.city == null) continue;
            if (g.city.facilityTeam != aiTeam && !g.city.isPublicFacility) ctx.capturables.Add(g);
        }
        ctx.crystals = (gm.weaponManager?.AllWeapons ?? new List<Weapon>()).OfType<Crystal>()
            .Where(c => IsInstanceValid(c) && !c.isDestroyed && c.grid != null).ToList();
        foreach (var k in moveClaims.Keys.ToList())
        {
            var c = moveClaims[k];
            if (c == null || !IsInstanceValid(c) || c.health <= 0 || c.team != aiTeam || !ctx.capturables.Contains(k)) moveClaims.Remove(k);
            else ctx.captureClaims[k] = c;
        }
        foreach (var u in ctx.mine)
            if (u.capturingGrid != null && u.capturingGrid == u.grid) ctx.captureClaims[u.capturingGrid] = u;
    }

    private bool IsEnemy(string team) => team != aiTeam && TeamHelper.IsEnemyForAttacker(aiTeam, team);

    private bool SeesGrid(Grids g)
    {
        if (g == null) return false;
        if (!ctx.fogOn) return true;
        return ctx.vision.Contains(g) || gameManager.fogOfWarManager.IsGridIlluminated(g);
    }

    private bool CanSee(Infantry u)
    {
        if (u?.grid == null) return false;
        if (!SeesGrid(u.grid)) return false;
        return Infantry.IsVisibleTo(u, aiTeam, gameManager.gridManager.grids);
    }

    // 敌军 e 位于 from 时，下回合能攻击到的所有格子
    private HashSet<Grids> ThreatTiles(Infantry e, Grids from)
    {
        var result = new HashSet<Grids>();
        if (from == null) return result;
        var origins = new List<Grids> { from };
        if (e.canAttackAfterMoving)
        {
            var r = ComputeReach(e, from, e.defaultMovePoints, enemyView: true);
            origins = r.cost.Keys.Where(g => g == from || g.infantries.All(o => o == e || o.team == e.team || o.isTransported)).ToList();
        }
        int maxR = MaxRange(e);
        foreach (var o in origins)
            foreach (var g in Ring(o, 1, maxR))
                if (CanHitAtDistance(e, Dist(o, g))) result.Add(g);
        return result;
    }
}
