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

// AI_Manager · DamageEstimate（由 AI_Manager.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class AI_Manager
{

    // ==================== 伤害估算（复刻 Infantry.CalculateFinalDamage，可指定假想位置/血量） ====================
    private int BaseDamage(Infantry att, Infantry def, out WeaponType wt, bool? attStealthed = null)
    {
        wt = WeaponType.None;
        if (att.attackType == AttackType.NoAttack) return 0;
        // 与引擎同一查表入口（含隐身专属攻防表），可假设攻击时是否隐身
        int p = att.CanUsePrimaryWeapon() ? att.GetMatrixDamage(def, false, attStealthed) : 0;
        if (p > 0) { wt = WeaponType.Primary; return p; }
        int s = att.CanUseSecondaryWeapon() ? att.GetMatrixDamage(def, true, attStealthed) : 0;
        if (s > 0) { wt = WeaponType.Secondary; return s; }
        return 0;
    }

    private int EstDamage(Infantry att, int attHp, Infantry def, int defHp, Grids defGrid, bool counter, bool? attStealthed = null)
    {
        int baseValue = BaseDamage(att, def, out _, attStealthed);
        if (baseValue <= 0 || attHp <= 0) return 0;
        float dmg = baseValue * (attHp / (float)Math.Max(1, att.maxHealth));
        float terrain = (def.defenseBonusType == Infantry.DefenseBonusType.CanDefenseBonus && defGrid != null) ? defGrid.TerrainDefenseBonus : 0f;
        float effDef = def.baseDefense * (defHp / (float)Math.Max(1, def.maxHealth)) * (1f + terrain);
        float raw = dmg - effDef;
        if (counter) raw *= att.counterMul;
        int r = Mathf.RoundToInt(raw);
        return Math.Max(1, r);
    }

    // 与 Infantry.IsTargetInAttackRange 一致
    private static bool CanHitAtDistance(Infantry att, int d)
    {
        if (d < 1) return false;
        if (att.useMinMaxAttackRange) return d >= att.minAttackRange && d <= att.maxAttackRange;
        return d <= att.attackRange;
    }

    private static int MaxRange(Infantry u) => Math.Max(1, u.useMinMaxAttackRange ? u.maxAttackRange : u.attackRange);

    // 反击估算：引擎在攻击前用"防守方攻击前血量"计算反击，这里保持一致
    private int EstCounter(Infantry att, Grids attFrom, int attHp, bool attStealthed, Infantry def, int dmgDealt)
    {
        if (dmgDealt >= def.health) return 0;
        if (att.cannotCounterWhenAttacked || !def.canCounterWhenDefending) return 0;
        if (def.attackType == AttackType.NoAttack || !TeamHelper.CanCounterAttack(def.team)) return 0;
        // 隐身反击门（与 Infantry.Attack 一致）：防守方打不了隐身攻击者则不能反击
        //（攻击后现形的单位先现形再挨反击，所以不受此门保护）
        if (attStealthed && !att.stealthRevealOnAttack && !StealthAttackable(def, att)
            && !(att.stealthRevealOnFlare && att.IsFlareLit())) return 0;
        int d = Dist(attFrom, def.grid);
        if (!def.canCounterAtRange ? d != 1 : !CanHitAtDistance(def, d)) return 0;
        // 与引擎一致：默认按挨打后的血量反击（counterUsePreDamageHp=true 时按挨打前）
        int counterHp = def.counterUsePreDamageHp ? def.health : def.health - dmgDealt;
        return EstDamage(def, counterHp, att, attHp, attFrom, counter: true);
    }

    private static bool StealthAttackable(Infantry attacker, Infantry target)
    {
        if (target.stealthAttackableBy == null || target.stealthAttackableBy.Length == 0) return false;
        return Array.IndexOf(target.stealthAttackableBy, attacker.UnitTypeId) >= 0;
    }

    /// <summary>AI 档案目标权重：空表=1；先查兵种名，再查 "*"</summary>
    private static float TargetWeight(Infantry u, Infantry e)
    {
        var w = u.aiTargetWeights;
        if (w == null || w.Count == 0) return 1f;
        if (w.TryGetValue(e.UnitTypeId, out float v)) return v;
        if (w.TryGetValue("*", out float d)) return d;
        return 1f;
    }

    private float UnitValue(Infantry u)
    {
        float v = Math.Max(u.cost, 500);
        foreach (var c in u.transportedUnits) if (c != null && IsInstanceValid(c)) v += Math.Max(c.cost, 500);
        return v;
    }

    /// <summary>站在 g、血量 hp、是否隐身 时，下回合预计损失的价值</summary>
    private float ThreatLoss(Infantry u, Grids g, int hp, bool stealthed, Infantry ignore = null)
    {
        if (hp <= 0) return 0;
        float loss = 0;
        if (ctx.threat.TryGetValue(g, out var list))
        {
            var dmgs = new List<int>();
            foreach (var e in list)
            {
                if (e == ignore || !IsInstanceValid(e)) continue;
                if (stealthed && !StealthAttackable(e, u)) continue;
                int d = EstDamage(e, e.health, u, hp, g, false);
                if (d > 0) dmgs.Add(d);
            }
            dmgs.Sort((a, b) => b.CompareTo(a));
            float total = 0; float[] w = { 1f, 0.6f, 0.35f, 0.2f };
            for (int i = 0; i < dmgs.Count && i < w.Length; i++) total += dmgs[i] * w[i];
            float lost = Math.Min(total, hp);
            loss = lost / u.maxHealth * UnitValue(u);
            if (total >= hp) loss += UnitValue(u) * 0.3f;   // 可能被击杀
        }
        else if (!stealthed && ctx.rememberedThreat.Contains(g))
        {
            loss = UnitValue(u) * 0.12f;                     // 雾中可能有敌人
        }
        float roleRisk = u.aiRole == 4 ? 1.5f : u.aiRole == 1 ? 0.6f : 1f;   // 刺客更惜命，突击更莽
        return loss * riskAversion * Math.Max(0f, u.aiRiskMultiplier) * roleRisk * (difficulty == 0 ? 0.3f : 1f);
    }
}
