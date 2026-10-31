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

// AI_Manager · Weapons（由 AI_Manager.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class AI_Manager
{

    // ==================== 阶段 0：兵器 ====================
    private void StepWeapons()
    {
        while (weaponQueue.Count > 0)
        {
            var w = weaponQueue.Dequeue();
            if (w == null || !IsInstanceValid(w) || w.isDestroyed) continue;
            // 黑炮/巨炮/死光自带 AI 只服务 P0/P-1，阵营兵器由这里按"能力"操作；激光自带 AI 不设阵营门，直接用
            if (w is Crystal) continue;   // 黑水晶等单位走位完再脉冲（见 PulseCrystals）
            bool acted = w is BlackCannon bc ? OperateCannon(bc) : RunBuiltinWeaponAI(w);
            Next(acted ? stepDelay : 0.05f);
            return;
        }
        stage = 1;
        Next(0.05f);
    }

    /// <summary>己方黑水晶：所有单位走位完成后再脉冲，净收益为正才用（避免给敌人回血）</summary>
    private void PulseCrystals()
    {
        foreach (var c in (gameManager.weaponManager?.AllWeapons ?? new List<Weapon>()).OfType<Crystal>().ToList())
        {
            if (!IsInstanceValid(c) || c.isDestroyed || c.team != aiTeam || !c.CanAttack()) continue;
            float v = 0; int supplyCount = 0;
            foreach (var u in gameManager.unitManager.AllUnits)
            {
                if (u == null || !IsInstanceValid(u) || u.grid == null || u.isTransported || u.health <= 0) continue;
                float sign = u.team == aiTeam ? 1f : TeamHelper.IsEnemyForAttacker(aiTeam, u.team) ? -1f : 0f;
                if (c.AIInHealRange(u.grid)) v += sign * c.AIPreviewHealDelta(u) / (float)Math.Max(1, u.maxHealth) * UnitValue(u);
                if (c.AIInSupplyRange(u.grid) && c.AIWouldSupply(u)) { supplyCount++; v += sign * 300; }
            }
            if (supplyCount > 0 && c.AISupplyBlockedByAmmo(supplyCount)) Log($"黑水晶@{c.grid?.GridIndex} 弹药不足，本次补给会被跳过");
            if (v <= 0) { Log($"黑水晶@{c.grid?.GridIndex} 不脉冲（净收益{v:F0}）"); continue; }
            c.ExecuteAI();
            Log($"黑水晶@{c.grid?.GridIndex} 脉冲 治疗/补给 净收益{v:F0}");
        }
    }

    /// <summary>单位站在 g 时能从会作用到我方的黑水晶拿到的收益（回血价值 + 补给需求）</summary>
    private float CrystalBonus(Infantry u, Grids g)
    {
        float b = 0;
        foreach (var c in ctx.crystals)
        {
            if (c.AIInHealRange(g)) b += c.AIPreviewHealDelta(u) / (float)Math.Max(1, u.maxHealth) * UnitValue(u);
            if (c.AIInSupplyRange(g) && c.AIWouldSupply(u)) b += 200 + SupplyNeed(u) * 0.5f;
        }
        return b;
    }

    private IEnumerable<Grids> CrystalTilesFor(Infantry u)
    {
        foreach (var c in ctx.crystals)
        {
            bool heal = c.AIPreviewHealDelta(u) > 0, sup = c.AIWouldSupply(u);
            if (!heal && !sup) continue;
            foreach (var g in Ring(c.grid, 0, Math.Max(c.crystalHealMaxRange, c.crystalSupplyMaxRange)))
                if ((heal && c.AIInHealRange(g)) || (sup && c.AIInSupplyRange(g))) yield return g;
        }
    }

    private bool RunBuiltinWeaponAI(Weapon w)
    {
        bool before = w.hasActed; int ra = w.remainingAttacks;
        w.ExecuteAI();
        Log($"兵器 {w.GetType().Name}@{w.grid?.GridIndex} 自带AI 执行（已行动={w.hasActed} 剩余次数{ra}→{w.remainingAttacks}）");
        return w.hasActed != before || w.remainingAttacks != ra;
    }

    /// <summary>
    /// 阵营炮台 AI：遍历可转向的所有朝向，算出每个朝向下最佳开火方案（单体炮取最佳格，死光取整条光束），
    /// 选最高分转过去开火；无利可图则恢复原朝向不动。
    /// </summary>
    private bool OperateCannon(BlackCannon bc)
    {
        if (bc.isDestroyed || !bc.CanAttack()) { Log($"兵器 {bc.GetType().Name}@{bc.grid?.GridIndex} 不可攻击（冷却/弹药/已行动）"); return false; }
        BuildContext();
        var origDir = bc.direction;
        float bestScore = 0; BlackCannon.CannonDirection bestDir = origDir; Grids bestGrid = null; string bestNote = "";

        var tried = new HashSet<BlackCannon.CannonDirection>();
        for (int r = 0; r < 4; r++)
        {
            if (!tried.Add(bc.direction)) break;   // 不能转向时只评估当前朝向
            var range = bc.CalculateAttackRange() ?? new List<Grids>();
            if (bc is DeathRay dr)
            {
                // 光束一次打中射程内全部敌方单位（死光自身只打敌方）
                float s = 0; int n = 0;
                foreach (var g in range)
                    foreach (var t in g.infantries)
                    {
                        if (t == null || !IsInstanceValid(t) || t.team == aiTeam || !CanSee(t)) continue;
                        if (!TeamHelper.IsEnemyForAttacker(aiTeam, t.team)) continue;
                        s += CannonHitValue(t, Mathf.Max(1, Mathf.RoundToInt(t.maxHealth * dr.fixedDamagePercent)), dr.CanDestroy); n++;
                    }
                if (s > bestScore && range.Count > 0) { bestScore = s; bestDir = bc.direction; bestGrid = range[0]; bestNote = $"光束命中{n}"; }
            }
            else
            {
                foreach (var g in range)
                {
                    if (!SeesGrid(g)) continue;
                    var tgt = bc.AIPeekTargetInGrid(g);
                    float s = 0; string note = null;
                    if (tgt is Infantry t && IsInstanceValid(t))
                    {
                        if (t.team != aiTeam && !CanSee(t)) continue;
                        s = CannonEffectValue(bc, t); note = $"{t.GetType().Name}@{g.GridIndex}";
                    }
                    else if (tgt is Weapon tw && IsInstanceValid(tw) && TeamHelper.IsEnemyForAttacker(aiTeam, tw.team))
                    {
                        float pct = bc.useModifiedDamage ? bc.modifiedHealthPercent : bc.fixedDamagePercent;
                        if (pct <= 0) continue;
                        int dmg = Mathf.RoundToInt(tw.maxHealth * pct);
                        s = Math.Min(dmg, tw.health) / (float)Math.Max(1, tw.maxHealth) * Math.Max(8000, tw.cost);
                        if (bc.CanDestroy && dmg >= tw.health) s += Math.Max(8000, tw.cost) * killBonusRatio;
                        note = $"兵器{tw.GetType().Name}@{g.GridIndex}";
                    }
                    if (s > bestScore) { bestScore = s; bestDir = bc.direction; bestGrid = g; bestNote = note; }
                }
            }
            if (!bc.canRotate) break;
            bc.RotateDirection();
        }

        // 转到最佳朝向（没有目标则恢复原朝向）
        var want = bestGrid != null ? bestDir : origDir;
        for (int i = 0; i < 4 && bc.direction != want; i++) bc.RotateDirection();
        if (bestGrid == null) { Log($"兵器 {bc.GetType().Name}@{bc.grid?.GridIndex} 四向无可打目标"); return false; }

        bool fired;
        if (bc is DeathRay ray) { int b = ray.remainingAttacks; ray.PerformAttack(null); fired = ray.remainingAttacks < b || ray.hasActed; }
        else fired = bc.AIFireAtGrid(bestGrid);
        if (fired)
        {
            Log($"兵器 {bc.GetType().Name}@{bc.grid?.GridIndex} 朝{bc.direction} 开火 {bestNote} 分{bestScore:F0}");
            if (followCamera) FocusCamera(bestGrid);
        }
        return fired;
    }

    /// <summary>黑炮/巨炮对单个单位的效果价值：敌方掉血为正，己方回血为正，误伤为负</summary>
    private float CannonEffectValue(BlackCannon bc, Infantry t)
    {
        float pct = bc.useModifiedDamage ? bc.modifiedHealthPercent : bc.fixedDamagePercent;
        bool enemy = TeamHelper.IsEnemyForAttacker(aiTeam, t.team);
        if (pct > 0)
        {
            int dmg = Mathf.Max(1, Mathf.RoundToInt(t.maxHealth * pct));
            float v = CannonHitValue(t, dmg, bc.CanDestroy);
            return enemy ? v : (t.team == aiTeam ? -v : 0);
        }
        if (bc.useModifiedDamage && pct == 0)
        {
            // 纯弹药/燃料效果：正=扣，负=补
            float v = 0;
            if (t.maxPrimaryAmmo > 0 && bc.modifiedAmmoPercent != 0)
            {
                int a = Mathf.RoundToInt(t.maxPrimaryAmmo * Math.Abs(bc.modifiedAmmoPercent));
                int real = bc.modifiedAmmoPercent > 0 ? Math.Min(a, t.currentPrimaryAmmo) : Math.Min(a, t.maxPrimaryAmmo - t.currentPrimaryAmmo);
                v += real / (float)t.maxPrimaryAmmo * UnitValue(t) * 0.3f;
            }
            if (t.consumeFuel && t.maxFuel > 0 && bc.modifiedFuelPercent != 0)
            {
                int f = Mathf.RoundToInt(t.maxFuel * Math.Abs(bc.modifiedFuelPercent));
                int real = bc.modifiedFuelPercent > 0 ? Math.Min(f, t.fuel) : Math.Min(f, t.maxFuel - t.fuel);
                v += real / (float)t.maxFuel * UnitValue(t) * 0.2f;
                if (bc.modifiedFuelPercent > 0 && t.fuel - f <= 0 && t.destroyOnOutOfFuel) v += UnitValue(t) * 0.8f;   // 油耗尽坠毁
            }
            bool drain = bc.modifiedAmmoPercent > 0 || bc.modifiedFuelPercent > 0;
            if (enemy) return drain ? v : -v;
            if (t.team == aiTeam) return drain ? -v : v;
            return 0;
        }
        if (pct < 0 && t.team == aiTeam)   // 治疗炮：给最缺血的己方单位
        {
            int heal = Mathf.RoundToInt(t.maxHealth * -pct);
            int room = bc.canOverMaxHp ? heal : Math.Max(0, t.maxHealth - t.health);
            return Math.Min(heal, room) / (float)t.maxHealth * UnitValue(t) * 0.8f;
        }
        return 0;
    }

    private float CannonHitValue(Infantry t, int dmg, bool canDestroy)
    {
        int real = canDestroy ? Math.Min(dmg, t.health) : Math.Min(dmg, Math.Max(0, t.health - 1));
        float v = real / (float)Math.Max(1, t.maxHealth) * UnitValue(t);
        if (canDestroy && dmg >= t.health) v += UnitValue(t) * killBonusRatio;
        if (t.capturingGrid != null && t.currentCaptureProgress > 0) v += 800;   // 打断占领
        return v;
    }
}
