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

public partial class AI_Manager : Node
{
    [Export] public GameManager gameManager;

    public bool p1AIEnabled = false;
    public bool p2AIEnabled = false;
    public bool autoEndTurn = false;

    // ==================== 检查器参数（默认即可） ====================
    [ExportGroup("AI 参数")]
    [Export(PropertyHint.Range, "0,2")] public int difficulty = 2;     // 0=简单(随机扰动大/不避险) 1=普通 2=困难
    [Export] public float stepDelay = 0.4f;                             // 每个单位行动间隔(秒)
    [Export] public float moveStepTime = 0.06f;                         // 移动动画每格耗时(秒)
    [Export] public float riskAversion = 0.8f;                          // 威胁规避权重（越大越怕死）
    [Export] public float killBonusRatio = 0.6f;                        // 击杀额外价值 = 目标造价 × 该系数
    [Export] public int memoryDays = 3;                                 // 雾中敌军位置记忆天数
    [Export] public bool followCamera = true;                           // 镜头跟随（仅跟随玩家可见的单位）
    [Export] public bool logDecisions = false;                          // 输出决策日志

    // ==================== 运行时 ====================
    private Timer aiTimer;
    private bool isRunningAI = false;
    private bool isBusy = false;
    private string aiTeam = "";
    private int stage = 0;                       // 0=兵器 1=单位 2=生产 3=新兵 4=结束
    private Queue<Weapon> weaponQueue = new();
    private HashSet<Infantry> handled = new();
    private Ctx ctx;
    private readonly RandomNumberGenerator rng = new();
    private readonly Dictionary<string, Dictionary<Infantry, Memo>> memory = new();
    private readonly Dictionary<Grids, Infantry> moveClaims = new();   // 占领兵"认领"的目标据点（跨回合保留，防止扎堆）

    private struct Memo { public Grids grid; public int day; public int hp; }

    private enum Act { Wait, Attack, AttackWeapon, Capture, Merge, Load, Unload, Supply, Repair, Illuminate, Explode, Hide, Reveal }

    private class Plan
    {
        public Infantry unit;
        public Grids dest;
        public Act act = Act.Wait;
        public Infantry target;
        public Weapon weaponTarget;
        public Grids actGrid;                    // 照明中心 / 卸载格
        public List<(Infantry cargo, Grids to)> unloads;
        public bool preReveal;                   // 行动前先显形（免费切换）
        public bool postHide;                    // 行动后隐身（免费切换）
        public float score = float.NegativeInfinity;
        public bool tactical;                    // 属于战术阶段（优先执行）
        public string note = "";
        public List<Grids> path;
    }

    private class Reach
    {
        public Dictionary<Grids, int> cost = new();
        public Dictionary<Grids, Grids> parent = new();
    }

    private class Ctx
    {
        public string team;
        public int day;
        public bool fogOn;
        public HashSet<Grids> vision = new();
        public List<Infantry> mine = new();
        public List<Infantry> enemies = new();            // 当前可见敌军
        public List<(Infantry u, Grids g, int hp)> remembered = new();   // 雾中记忆
        public List<Weapon> enemyWeapons = new();
        public Dictionary<Grids, List<Infantry>> threat = new();
        public HashSet<Grids> rememberedThreat = new();   // 记忆中敌军的大致威胁区
        public List<Grids> capturables = new();            // 非己方据点
        public List<Crystal> crystals = new();             // 会作用到我方的黑水晶（己方 + 中立按过滤器）
        public Dictionary<Grids, Infantry> captureClaims = new();
        public Dictionary<string, Dictionary<Grids, int>> distCache = new();
    }

    // ==================== 生命周期 ====================
    public override void _Ready()
    {
        AddToGroup("ai_manager");
        rng.Randomize();
        aiTimer = new Timer { WaitTime = stepDelay, OneShot = true };
        aiTimer.Timeout += OnAIStep;
        AddChild(aiTimer);
    }

    public bool IsAITeam(string team)
    {
        if (team == TeamHelper.Player1) return p1AIEnabled;
        if (team == TeamHelper.Player2) return p2AIEnabled;
        return false;
    }

    public bool IsCurrentPhaseAI()
    {
        if (gameManager == null) return false;
        return IsAITeam(TeamHelper.GetPhaseTeamName(gameManager.turnPhase));
    }

    public void OnPhaseStart()
    {
        if (!IsCurrentPhaseAI() || isRunningAI) return;
        aiTeam = TeamHelper.GetPhaseTeamName(gameManager.turnPhase);
        isRunningAI = true;
        isBusy = false;
        stage = 0;
        handled.Clear();
        if (logDecisions)
            foreach (var u in gameManager.unitManager?.AllUnits ?? new List<Infantry>())
                if (u != null && IsInstanceValid(u) && u.team == aiTeam && (u.hasActed || u.state == UnitState.Acted))
                    GD.Print($"[AI] 回合开始即已行动: {u.GetType().Name}@{u.grid?.GridIndex} state={u.state} transported={u.isTransported}");
        weaponQueue = new Queue<Weapon>((gameManager.weaponManager?.AllWeapons ?? new List<Weapon>())
            .Where(w => w != null && IsInstanceValid(w) && !w.isDestroyed && w.team == aiTeam));
        Log($"===== {aiTeam} 回合开始 =====");
        aiTimer.WaitTime = 0.3f;
        aiTimer.Start();
    }

    private void OnAIStep()
    {
        if (!isRunningAI || isBusy) return;
        if (gameManager == null || !IsInstanceValid(gameManager) || gameManager.gameEnded || !IsCurrentPhaseAI())
        {
            isRunningAI = false;
            return;
        }

        try
        {
            switch (stage)
            {
                case 0: StepWeapons(); return;
                case 1: case 3: StepUnits(); return;
                case 2: StepProduction(); return;
                default: FinishTurn(); return;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[AI] 出错，已跳过当前步骤: {e}");
            stage++;
            Next(stepDelay);
        }
    }

    private void Next(float delay)
    {
        if (!isRunningAI) return;
        aiTimer.WaitTime = Mathf.Max(0.05f, delay);
        aiTimer.Start();
    }

    private void FinishTurn()
    {
        isRunningAI = false;
        Log($"===== {aiTeam} 回合结束 =====");
        if (autoEndTurn && IsCurrentPhaseAI())
            gameManager.CallDeferred(nameof(GameManager.NextPhase));
    }

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

    // ==================== 阶段 1/3：单位 ====================
    private void StepUnits()
    {
        BuildContext();
        var pending = ctx.mine.Where(u => !handled.Contains(u) && u.state != UnitState.Acted && !u.hasActed).ToList();
        if (pending.Count == 0)
        {
            if (stage == 1) PulseCrystals();
            stage = stage == 1 ? 2 : 4;
            Next(0.05f);
            return;
        }

        // 每个待行动单位都评估一次，挑出最值得先执行的方案
        Plan best = null;
        float bestKey = float.NegativeInfinity;
        foreach (var u in pending)
        {
            var p = BestPlan(u);
            if (p == null) { Log($"{u.GetType().Name}@{u.grid?.GridIndex} 无可行方案"); continue; }
            float key = p.act == Act.Illuminate ? 2_000_000f + p.score : p.tactical ? 1_000_000f + p.score : -RoleOrder(u) * 10_000f - DistToFront(u);
            if (key > bestKey) { bestKey = key; best = p; }
        }

        if (best == null)
        {
            foreach (var u in pending) { handled.Add(u); FinishUnit(u); }
            Next(0.05f);
            return;
        }

        handled.Add(best.unit);
        Log($"{best.unit.GetType().Name}@{best.unit.grid?.GridIndex} → {best.act} 目的地{best.dest?.GridIndex} 分数{best.score:F0} {best.note}");
        ExecutePlan(best);
    }

    private int RoleOrder(Infantry u)
    {
        if (u.canIlluminate && u.currentFlareAmmo > 0 && ctx.fogOn) return 0;
        if (IsIndirect(u)) return 1;
        if (u.canTransportUnits && u.transportedUnits.Count > 0) return 2;
        if (u.attackType != AttackType.NoAttack && u.captureAbility == CaptureAbility.CannotCapture) return 3;
        if (u.captureAbility != CaptureAbility.CannotCapture) return 4;
        if (u.canTransportUnits) return 5;
        return 6;   // 补给/修理最后（等别人站好位）
    }

    private float DistToFront(Infantry u)
    {
        if (u.grid == null || ctx.enemies.Count == 0) return 0;
        return ctx.enemies.Min(e => Dist(u.grid, e.grid));
    }

    // ==================== 阶段 2：生产 ====================
    private void StepProduction()
    {
        BuildContext();
        Produce();
        stage = 3;
        handled.RemoveWhere(u => u == null || !IsInstanceValid(u));
        Next(stepDelay);
    }

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

    // ==================== 寻路 ====================
    private static readonly Vector2I[] Dirs = { new(0, 1), new(-1, 0), new(0, -1), new(1, 0) };

    private Grids At(int x, int y)
    {
        var gmg = gameManager.gridManager;
        if (x < 0 || y < 0 || x >= gmg.searchRange.X || y >= gmg.searchRange.Y) return null;
        return gmg.map[x, y];
    }

    private IEnumerable<Grids> Ring(Grids c, int minR, int maxR)
    {
        if (c == null) yield break;
        for (int dx = -maxR; dx <= maxR; dx++)
            for (int dy = -maxR; dy <= maxR; dy++)
            {
                int d = Math.Abs(dx) + Math.Abs(dy);
                if (d < minR || d > maxR) continue;
                var g = At(c.GridIndex.X + dx, c.GridIndex.Y + dy);
                if (g != null) yield return g;
            }
    }

    private static int Dist(Grids a, Grids b) =>
        (a == null || b == null) ? 999 : Math.Abs(a.GridIndex.X - b.GridIndex.X) + Math.Abs(a.GridIndex.Y - b.GridIndex.Y);

    private static int StepCost(Infantry u, Grids g)
    {
        int c = u.GetMoveCost(g.gridType);
        return (c >= 999 || c == int.MaxValue || c < 0) ? -1 : Math.Max(1, c);
    }

    /// <summary>可达格（Dijkstra）。enemyView=true 时以敌方视角计算（AI 单位阻挡它）。</summary>
    private Reach ComputeReach(Infantry u, Grids start, int movePts, bool enemyView = false)
    {
        var r = new Reach();
        if (start == null) return r;
        r.cost[start] = 0;
        if (movePts <= 0) return r;
        int fuelLimit = (u.consumeFuel && !enemyView) ? Math.Max(0, u.fuel) : int.MaxValue;
        var fuelUsed = new Dictionary<Grids, int> { [start] = 0 };
        var open = new PriorityQueue<Grids, int>();
        open.Enqueue(start, 0);
        while (open.TryDequeue(out var cur, out int cc))
        {
            if (cc > r.cost[cur]) continue;
            foreach (var d in Dirs)
            {
                var n = At(cur.GridIndex.X + d.X, cur.GridIndex.Y + d.Y);
                if (n == null) continue;
                int sc = StepCost(u, n);
                if (sc < 0) continue;
                if (n.weapon != null && IsInstanceValid(n.weapon) && u.overlapType != UnitOverlapType.Oozium) continue;
                int nc = cc + sc;
                if (nc > movePts) continue;
                int nf = fuelUsed[cur] + (n.gridType == GridType.TP ? 0 : sc);
                if (nf > fuelLimit) continue;
                if (Blocks(u, n, enemyView)) continue;
                if (r.cost.TryGetValue(n, out int old) && old <= nc) continue;
                r.cost[n] = nc; r.parent[n] = cur; fuelUsed[n] = nf;
                open.Enqueue(n, nc);
            }
        }
        return r;
    }

    private bool Blocks(Infantry mover, Grids g, bool enemyView)
    {
        foreach (var o in g.infantries)
        {
            if (o == null || o == mover || !IsInstanceValid(o) || o.isTransported) continue;
            if (o.team == mover.team || o.team == TeamHelper.Player) continue;
            if (enemyView) { if (o.team == aiTeam) return true; continue; }
            if (ctx.enemies.Contains(o)) return true;   // 只有"看得见"的敌军才会被 AI 绕开
        }
        return false;
    }

    private enum EndKind { None, Free, Load, Merge }

    private EndKind CanEnd(Infantry u, Grids g, out Infantry partner)
    {
        partner = null;
        if (g == u.grid) return EndKind.Free;
        if (g.weapon != null && IsInstanceValid(g.weapon) && u.overlapType != UnitOverlapType.Oozium) return EndKind.None;
        var others = g.infantries.Where(o => o != null && o != u && IsInstanceValid(o) && !o.isTransported).ToList();
        // AI 不知道雾中/隐身敌军：当作空格（真实移动时会被截断）
        var known = others.Where(o => !IsEnemy(o.team) || ctx.enemies.Contains(o)).ToList();
        if (known.Count == 0) return EndKind.Free;
        if (known.Any(o => IsEnemy(o.team))) return EndKind.None;
        if (u.isStealthed && u.stealthOverlapMode == 2) return EndKind.None;

        partner = known.FirstOrDefault(o => o.canTransportUnits && (o.team == u.team || o.team == TeamHelper.Player) && o.CanTransportUnit(u));
        if (partner != null && !(u.isStealthed && !u.canLoadUnloadWhileStealthed)) return EndKind.Load;

        partner = known.FirstOrDefault(o => o.team == u.team && o.GetType() == u.GetType());
        if (partner != null && !(u.isStealthed && u.stealthOverlapMode == 1) && gameManager.CanMerge(u, partner)) return EndKind.Merge;

        partner = null;
        if (u.overlapType == UnitOverlapType.Overlapping || u.overlapType == UnitOverlapType.Oozium) return EndKind.Free;
        return EndKind.None;
    }

    // 真实占用（执行移动时用，含雾中单位）
    private bool RealStayable(Infantry u, Grids g)
    {
        if (g == u.grid) return true;
        var others = g.infantries.Where(o => o != null && o != u && IsInstanceValid(o) && !o.isTransported).ToList();
        if (others.Count == 0) return g.weapon == null || u.overlapType == UnitOverlapType.Oozium;
        if (others.Any(o => o.team != u.team && o.team != TeamHelper.Player)) return false;
        return u.overlapType == UnitOverlapType.Overlapping || u.overlapType == UnitOverlapType.Oozium;
    }

    // 距离场：从目标集合出发，按单位移动消耗反向 Dijkstra（缓存到本步）
    private Dictionary<Grids, int> DistanceField(Infantry u, string key, IEnumerable<Grids> goals) => DistanceField(u, key, () => goals);

    private Dictionary<Grids, int> DistanceField(Infantry u, string key, Func<IEnumerable<Grids>> goalsFn)
    {
        string k = u.UnitTypeId + "|" + key;
        if (ctx.distCache.TryGetValue(k, out var f)) return f;
        var goals = goalsFn();
        f = new Dictionary<Grids, int>();
        var open = new PriorityQueue<Grids, int>();
        foreach (var g in goals.Where(x => x != null).Distinct()) { f[g] = 0; open.Enqueue(g, 0); }
        while (open.TryDequeue(out var cur, out int cc))
        {
            if (cc > f[cur]) continue;
            int enter = StepCost(u, cur);
            if (enter < 0 && cc > 0) continue;
            foreach (var d in Dirs)
            {
                var n = At(cur.GridIndex.X + d.X, cur.GridIndex.Y + d.Y);
                if (n == null || StepCost(u, n) < 0) continue;
                int nc = cc + Math.Max(1, enter < 0 ? 1 : enter);
                if (f.TryGetValue(n, out int old) && old <= nc) continue;
                f[n] = nc; open.Enqueue(n, nc);
            }
        }
        ctx.distCache[k] = f;
        return f;
    }

    private static int FieldAt(Dictionary<Grids, int> f, Grids g) => f != null && g != null && f.TryGetValue(g, out int v) ? v : 999;

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

    // ==================== 方案评估 ====================
    private Plan BestPlan(Infantry u)
    {
        if (u.grid == null) return null;
        var plans = new List<Plan>();

        bool stealthed = u.isStealthed;
        bool freeToggle = CanToggle(u) && !u.stealthToggleCostsAction;
        bool mayMove = Infantry.CanUnitMove(u) && u.CanMove() && !u.isMoved && u.state != UnitState.Moved;
        bool preRevealForMove = false;
        if (stealthed && !u.canMoveWhileStealthed)
        {
            if (freeToggle) preRevealForMove = true; else mayMove = false;
        }

        int mp = mayMove ? u.movePoints : 0;
        var reach = ComputeReach(u, u.grid, mp);
        var dests = new List<(Grids g, EndKind k, Infantry partner)>();
        foreach (var g in reach.cost.Keys)
        {
            var k = CanEnd(u, g, out var partner);
            if (k != EndKind.None) dests.Add((g, k, partner));
        }
        if (dests.Count == 0) dests.Add((u.grid, EndKind.Free, null));

        // 仍处于继续占领状态：评估“原地继续”
        foreach (var (g, kind, partner) in dests)
        {
            bool moved = g != u.grid;
            bool stealthAtDest = stealthed && !(preRevealForMove && moved);
            bool reveal = preRevealForMove && moved;

            if (kind == EndKind.Load) { plans.Add(EvalLoad(u, g, partner, reveal)); continue; }
            if (kind == EndKind.Merge) { plans.Add(EvalMerge(u, g, partner, reveal)); continue; }

            EvalAttacks(u, g, moved, stealthAtDest, reveal, freeToggle, plans);
            EvalCapture(u, g, moved, stealthAtDest, reveal, freeToggle, plans);
            EvalSupport(u, g, moved, stealthAtDest, reveal, freeToggle, plans);
            EvalStealthAndWait(u, g, moved, stealthAtDest, reveal, freeToggle, plans);
        }

        Plan best = null;
        foreach (var p in plans)
        {
            if (p == null || float.IsNegativeInfinity(p.score)) continue;
            p.unit = u;
            if (difficulty < 2) p.score += rng.RandfRange(-1f, 1f) * (difficulty == 0 ? 1500f : 400f);
            if (best == null || p.score > best.score) best = p;
        }
        if (best != null) best.tactical = best.act != Act.Wait && best.act != Act.Hide && best.act != Act.Reveal && best.act != Act.Load && best.score > 300;
        if (best != null) PathFor(best, reach);
        return best;
    }

    private void PathFor(Plan p, Reach r) { p.path = BuildPath(r, p.unit.grid, p.dest); }

    private List<Grids> BuildPath(Reach r, Grids start, Grids dest)
    {
        var path = new List<Grids>();
        var cur = dest;
        int guard = 0;
        while (cur != null && guard++ < 500)
        {
            path.Add(cur);
            if (cur == start) break;
            if (!r.parent.TryGetValue(cur, out cur)) break;
        }
        path.Reverse();
        if (path.Count == 0 || path[0] != start) path.Insert(0, start);
        return path;
    }

    // —— 攻击（单位 & 兵器） ——
    private void EvalAttacks(Infantry u, Grids g, bool moved, bool stealthAtDest, bool reveal, bool freeToggle, List<Plan> plans)
    {
        if (u.attackType == AttackType.NoAttack) return;
        if (moved && !u.canAttackAfterMoving) return;
        if (u.isAttacked) return;
        bool needReveal = reveal;
        bool attStealthed = stealthAtDest;
        if (stealthAtDest && !u.canAttackWhileStealthed)
        {
            if (!freeToggle) return;
            needReveal = true; attStealthed = false;
        }
        bool stillStealthAfter = attStealthed && !u.stealthRevealOnAttack;
        int maxR = MaxRange(u);

        foreach (var e in ctx.enemies)
        {
            int d = Dist(g, e.grid);
            if (d > maxR || !CanHitAtDistance(u, d)) continue;
            if (ctx.fogOn && !Infantry.IsVisibleTo(e, u.team, null)) continue;   // ✅ 公平性：AI不攻击看不见的单位（含隐身+林地隐蔽）
            if (!Infantry.CanTargetStealth(u, e)) continue;
            int dmg = EstDamage(u, u.health, e, e.health, e.grid, false, attStealthed);
            if (dmg <= 0) continue;
            int dealt = Math.Min(dmg, e.health);
            bool kill = dmg >= e.health;
            int counter = EstCounter(u, g, u.health, attStealthed, e, dmg);
            int hpAfter = Math.Max(0, u.health - counter);

            float tw = TargetWeight(u, e);
            if (tw <= 0) continue;                                    // AI 档案：不打此类目标
            float score = dealt / (float)e.maxHealth * UnitValue(e) * tw;
            if (kill && u.aiPreferKill) score += UnitValue(e) * killBonusRatio * tw;
            if (u.aiFocusFire && e.health < e.maxHealth) score += (1f - e.health / (float)e.maxHealth) * UnitValue(e) * 0.15f * tw;
            if (e.capturingGrid != null && e.capturingGrid.city != null)
                score += (e.capturingGrid.city.facilityTeam == aiTeam ? 2500 : 800) + e.currentCaptureProgress * 60;
            if (e.canTransportUnits && e.transportedUnits.Count > 0 && kill) score += 1500;
            score -= counter / (float)u.maxHealth * UnitValue(u);
            if (hpAfter <= 0) score -= UnitValue(u);
            score -= ThreatLoss(u, g, hpAfter, stillStealthAfter, kill ? e : null);
            score += Terrain(u, g) * 200;
            bool hideAfter = freeToggle && !stillStealthAfter && WantsHide(u, g, hpAfter);
            if (hideAfter) score += ThreatLoss(u, g, hpAfter, false, kill ? e : null) - ThreatLoss(u, g, hpAfter, true, kill ? e : null);
            plans.Add(new Plan { dest = g, act = Act.Attack, target = e, score = score, preReveal = needReveal, postHide = hideAfter, note = $"伤{dealt}{(kill ? " 击杀" : "")} 反{counter}" });
        }

        foreach (var w in ctx.enemyWeapons)
        {
            if (!u.CanTargetWeapon(w)) continue;
            var wt = u.CanUsePrimaryWeapon() ? WeaponType.Primary : (u.CanUseSecondaryWeapon() ? WeaponType.Secondary : WeaponType.None);
            if (wt == WeaponType.None) continue;
            Grids hitGrid = null;
            foreach (var wg in WeaponGrids(w))
            {
                if (w.occupiedGrids.Count > 1 && !w.IsWeakPointGrid(wg)) continue;
                int d = Dist(g, wg);
                if (CanHitAtDistance(u, d)) { hitGrid = wg; break; }
            }
            if (hitGrid == null) continue;
            int dmg = u.CalculateDamageAgainstWeapon(w, wt);
            if (dmg <= 0) continue;
            float wv = Math.Max(w.cost, 8000) * (w.contributesToVictory ? 2f : 1f);
            float score = Math.Min(dmg, w.health) / (float)Math.Max(1, w.maxHealth) * wv;
            if (dmg >= w.health) score += wv * killBonusRatio;
            score -= ThreatLoss(u, g, u.health, stillStealthAfter);
            plans.Add(new Plan { dest = g, act = Act.AttackWeapon, weaponTarget = w, score = score, preReveal = needReveal, note = $"兵器伤{dmg}" });
        }
    }

    private IEnumerable<Grids> WeaponGrids(Weapon w)
    {
        if (w.occupiedGrids != null && w.occupiedGrids.Count > 0) return w.occupiedGrids.Where(x => x != null);
        return w.grid != null ? new[] { w.grid } : Array.Empty<Grids>();
    }

    // —— 占领 ——
    private void EvalCapture(Infantry u, Grids g, bool moved, bool stealthAtDest, bool reveal, bool freeToggle, List<Plan> plans)
    {
        if (u.captureAbility == CaptureAbility.CannotCapture || g.city == null) return;
        if (u.aiRole == 1) return;   // AI 档案：突击单位不占领
        if (moved && !u.allowCaptureAfterMove) return;
        if (!g.city.CanBeCapturedBy(u)) return;
        bool needReveal = reveal;
        if (stealthAtDest && !u.canCaptureWhileStealthed) { if (!freeToggle) return; needReveal = true; }
        if (ctx.captureClaims.TryGetValue(g, out var claimer) && claimer != u && IsInstanceValid(claimer)) return;

        int required = Math.Max(1, g.city.capturePointsRequired);
        int progress = (u.capturingGrid == g) ? u.currentCaptureProgress : 0;
        int power = Math.Max(1, u.GetCapturePower());
        int remaining = required - progress;
        int turns = (int)Math.Ceiling(remaining / (float)power);
        float val = FacilityValue(g.city);
        float score = turns <= 1 ? val * 1.2f : val * 0.9f / turns;
        if (progress > 0) score += 1200 + progress * 40;   // 已经占了一半，别半途而废
        float risk = ThreatLoss(u, g, u.health, false);
        score -= risk * 0.7f;                               // 占领本身值得冒点险
        score += Terrain(u, g) * 150;
        if (u.aiRole == 2) score += 1500;   // AI 档案：占点优先
        plans.Add(new Plan { dest = g, act = Act.Capture, score = score, preReveal = needReveal, note = $"占领 剩{turns}回合" });
    }

    private float FacilityValue(Facility f)
    {
        float v = 3000 + f.GetCurrentIncome() * 2.5f;
        if (f.canProduce) v += 5000;
        if (!string.IsNullOrEmpty(f.facilityTeam) && f.facilityTeam != TeamHelper.Player0 && IsEnemy(f.facilityTeam)) v *= 1.4f;   // 夺敌据点=双倍收益
        return v;
    }

    // —— 补给 / 修理 / 照明 / 自爆 / 卸载 ——
    private void EvalSupport(Infantry u, Grids g, bool moved, bool stealthAtDest, bool reveal, bool freeToggle, List<Plan> plans)
    {
        bool customLocked = stealthAtDest && !u.canCustomActionWhileStealthed;
        bool customReveal = reveal || (customLocked && freeToggle);
        bool customOk = !customLocked || freeToggle;

        // 补给
        if (u.canSupplyUnits && customOk)
        {
            float val = 0;
            foreach (var t in Ring(g, Math.Max(1, u.minSupplyRange), Math.Max(1, u.maxSupplyRange)).SelectMany(x => x.infantries))
            {
                if (t == null || t == u || t.team != u.team || !IsInstanceValid(t)) continue;
                val += SupplyNeed(t);
            }
            if (val > 0)
                plans.Add(new Plan { dest = g, act = Act.Supply, score = val - ThreatLoss(u, g, u.health, stealthAtDest && !customReveal), preReveal = customReveal && customLocked, note = "补给" });
        }

        // 修理（修理艇）
        var rp = RepairParams(u);
        if (rp.can && (!moved || rp.afterMove) && customOk)
        {
            Infantry bestT = null; float bestV = 0;
            foreach (var t in Ring(g, Math.Max(0, rp.min), Math.Max(0, rp.max)).SelectMany(x => x.infantries))
            {
                if (t == null || !IsInstanceValid(t) || t.isTransported) continue;
                if (!u.IsRepairTargetAllowed(t)) continue;   // 与引擎/界面同一判定
                int amt = rp.amount;
                float v;
                if (amt > 0 && t.team == u.team) v = Math.Min(amt, t.maxHealth - t.health) / (float)t.maxHealth * UnitValue(t);
                else if (amt < 0 && IsEnemy(t.team)) v = Math.Min(-amt, t.health) / (float)t.maxHealth * UnitValue(t);
                else continue;
                if (v > bestV) { bestV = v; bestT = t; }
            }
            if (bestT != null)
                plans.Add(new Plan { dest = g, act = Act.Repair, target = bestT, score = bestV * 1.2f - ThreatLoss(u, g, u.health, stealthAtDest && !customReveal), preReveal = customReveal && customLocked, note = "修理" });
        }

        // 照明弹：先照亮，后面的单位才能打到雾中/隐身敌人
        if (u.canIlluminate && u.currentFlareAmmo > 0 && ctx.fogOn && (!moved || u.canIlluminateAfterMove) && customOk)
        {
            Grids bestC = null; float bestV = 0;
            foreach (var c in Ring(g, Math.Max(0, u.minLaunchRange), Math.Max(0, u.maxLaunchRange)))
            {
                float v = 0;
                foreach (var t in Ring(c, Math.Max(0, u.minIlluminationRange), Math.Max(0, u.maxIlluminationRange)))
                {
                    if (ctx.vision.Contains(t)) continue;
                    v += 20;
                    foreach (var (ru, rg, _) in ctx.remembered) if (rg == t) v += 900 + ru.cost * 0.05f;
                    if (ctx.capturables.Contains(t)) v += 40;
                }
                if (v > bestV) { bestV = v; bestC = c; }
            }
            if (bestC != null && bestV >= 300)
                plans.Add(new Plan { dest = g, act = Act.Illuminate, actGrid = bestC, score = bestV - ThreatLoss(u, g, u.health, stealthAtDest && !customReveal), preReveal = customReveal && customLocked, note = $"照明{bestC.GridIndex}" });
        }

        // 自爆
        if (u.canExplode && u.currentExplodeAmmo > 0 && customOk)
        {
            float v = 0; int kills = 0;
            foreach (var t in ctx.mine.Concat(ctx.enemies).Distinct())
            {
                if (t == null || !IsInstanceValid(t) || t.grid == null) continue;
                var tg = t == u ? g : t.grid;
                int d = Dist(g, tg);
                if (d < u.explosionMinRange || d > u.explosionMaxRange) continue;
                if (t == u && u.explosionDestroysSelf) continue;
                bool isEnemy = t.team != u.team;
                bool include = u.explosionTargetMode switch { 1 => isEnemy, 2 => !isEnemy, _ => true };
                if (!include) continue;
                int dmg = gameManager.CalculateExplosionDamage(u, t);
                if (dmg > 0)
                {
                    int real = Math.Min(dmg, u.explosionCanKill ? t.health : Math.Max(0, t.health - 1));
                    float tv = real / (float)t.maxHealth * UnitValue(t);
                    if (u.explosionCanKill && dmg >= t.health) { tv += UnitValue(t) * killBonusRatio; if (isEnemy) kills++; }
                    v += IsEnemy(t.team) ? tv : -tv * 1.2f;
                }
                else if (dmg < 0 && !IsEnemy(t.team))
                    v += Math.Min(-dmg, t.maxHealth - t.health) / (float)t.maxHealth * UnitValue(t);
            }
            if (u.explosionAffectsWeapons)
                foreach (var w in ctx.enemyWeapons)
                {
                    int d = Dist(g, w.grid);
                    if (d < u.explosionMinRange || d > u.explosionMaxRange) continue;
                    if (u.explosionTargetMode == 2) continue;
                    int dmg = gameManager.CalculateExplosionDamage(u, w);
                    if (dmg > 0) v += Math.Min(dmg, w.health) / (float)Math.Max(1, w.maxHealth) * Math.Max(w.cost, 8000);
                }
            if (u.explosionDestroysSelf) v -= UnitValue(u);
            if (v > 500)
                plans.Add(new Plan { dest = g, act = Act.Explode, score = v, preReveal = customReveal && customLocked, note = $"自爆 价值{v:F0} 击杀{kills}" });
        }

        // 卸载（运输单位）
        if (u.canTransportUnits && u.transportedUnits.Count > 0 && !(stealthAtDest && !u.canLoadUnloadWhileStealthed && !freeToggle))
        {
            var unloads = new List<(Infantry, Grids)>();
            float v = 0;
            var used = new HashSet<Grids>();
            foreach (var cargo in u.transportedUnits.ToList())
            {
                if (cargo == null || !IsInstanceValid(cargo)) continue;
                Grids bestT = null; float bestTV = float.NegativeInfinity;
                foreach (var t in Ring(g, 1, 1))
                {
                    if (used.Contains(t) || StepCost(cargo, t) < 0 || !u.CanUnloadToGrid(t)) continue;
                    if (t.infantries.Any(o => o != null && IsInstanceValid(o) && !o.isTransported)) continue;
                    float tv = 0;
                    if (cargo.captureAbility != CaptureAbility.CannotCapture)
                    {
                        if (ctx.capturables.Contains(t) && !ctx.captureClaims.ContainsKey(t)) tv += 2500;
                        var f = DistanceField(cargo, "cap", ctx.capturables);
                        int dd = FieldAt(f, t);
                        tv += dd <= cargo.defaultMovePoints ? 1200 : 0;
                    }
                    if (ctx.enemies.Any(e => CanHitAtDistance(cargo, Dist(t, e.grid)))) tv += 700;
                    tv -= ThreatLoss(cargo, t, cargo.health, false) * 0.8f;
                    if (tv > bestTV) { bestTV = tv; bestT = t; }
                }
                if (bestT != null && bestTV > 400) { unloads.Add((cargo, bestT)); used.Add(bestT); v += bestTV; }
            }
            if (unloads.Count > 0)
                plans.Add(new Plan
                {
                    dest = g, act = Act.Unload, unloads = unloads, score = v - ThreatLoss(u, g, u.health, false) * 0.6f,
                    preReveal = stealthAtDest && !u.canLoadUnloadWhileStealthed, note = $"卸载{unloads.Count}"
                });
        }
    }

    private float SupplyNeed(Infantry t)
    {
        float v = 0;
        if (t.maxPrimaryAmmo > 0 && t.maxPrimaryAmmo < 99 && t.currentPrimaryAmmo < t.maxPrimaryAmmo)
            v += (1f - t.currentPrimaryAmmo / (float)t.maxPrimaryAmmo) * UnitValue(t) * 0.25f + (t.currentPrimaryAmmo == 0 ? 800 : 0);
        if (t.consumeFuel && t.maxFuel > 0 && t.fuel < t.maxFuel)
        {
            float frac = 1f - t.fuel / (float)t.maxFuel;
            v += frac * UnitValue(t) * 0.15f;
            if (t.fuel <= Math.Max(t.lowFuelThreshold, t.dailyFuelConsumption * 2 + t.stealthFuelCostPerDay * 2)) v += UnitValue(t) * 0.4f;   // 快坠毁了
        }
        if (t.canIlluminate && t.currentFlareAmmo <= 0) v += 300;
        if (t.canExplode && t.currentExplodeAmmo <= 0) v += 300;
        return v;
    }

    // —— 装载 / 合流 ——
    private Plan EvalLoad(Infantry u, Grids g, Infantry transport, bool reveal)
    {
        // 只有离目标很远时才值得上车
        float far = 0;
        if (u.captureAbility != CaptureAbility.CannotCapture && ctx.capturables.Count > 0)
            far = FieldAt(DistanceField(u, "cap", ctx.capturables), u.grid);
        else if (ctx.enemies.Count > 0)
            far = FieldAt(DistanceField(u, "enemy", ctx.enemies.Select(e => e.grid)), u.grid);
        if (far <= u.defaultMovePoints * 2 || far >= 999) return null;
        float score = 400 + (far - u.defaultMovePoints * 2) * 120 - ThreatLoss(transport, g, transport.health, transport.isStealthed) * 0.3f;
        return new Plan { dest = g, act = Act.Load, target = transport, score = score, preReveal = reveal, note = "上车" };
    }

    private Plan EvalMerge(Infantry u, Grids g, Infantry target, bool reveal)
    {
        if (target.health >= target.maxHealth && u.health >= u.maxHealth) return null;
        if (u.health > u.maxHealth * 0.5f && target.health > target.maxHealth * 0.5f) return null;
        int combined = u.health + target.health;
        int kept = Math.Min(target.maxHealth, combined);
        float gain = (kept - Math.Max(u.health, target.health)) / (float)target.maxHealth * target.cost * 0.6f;
        float refund = Math.Max(0, combined - target.maxHealth) / (float)target.maxHealth * target.cost * 0.5f;
        float score = gain + refund + 200 - ThreatLoss(target, g, kept, target.isStealthed);
        return new Plan { dest = g, act = Act.Merge, target = target, score = score, preReveal = reveal, note = "合流" };
    }

    // —— 隐身切换 + 待机位置 ——
    private void EvalStealthAndWait(Infantry u, Grids g, bool moved, bool stealthAtDest, bool reveal, bool freeToggle, List<Plan> plans)
    {
        float pos = PositionScore(u, g);
        float riskVisible = ThreatLoss(u, g, u.health, false);
        float riskHidden = u.canStealth ? ThreatLoss(u, g, u.health, true) : riskVisible;
        float riskNow = stealthAtDest ? riskHidden : riskVisible;

        var wait = new Plan { dest = g, act = Act.Wait, score = pos - riskNow, preReveal = reveal, note = "待机" };
        if (freeToggle && u.canStealth)
        {
            if (!stealthAtDest && WantsHide(u, g, u.health)) { wait.postHide = true; wait.score = pos - riskHidden; wait.note = "待机+隐身"; }
            else if (stealthAtDest && WantsReveal(u, g)) { wait.postHide = false; wait.preReveal = true; wait.score = pos - riskVisible + 300; wait.note = "待机+显形省油"; }
        }
        plans.Add(wait);

        // 切换算独立行动：隐身/显形就是这回合的行动
        if (u.canStealth && CanToggle(u) && u.stealthToggleCostsAction)
        {
            if (!stealthAtDest && WantsHide(u, g, u.health))
                plans.Add(new Plan { dest = g, act = Act.Hide, score = pos - riskHidden - StealthCost(u), preReveal = reveal, note = "隐身" });
            else if (stealthAtDest && WantsReveal(u, g))
                plans.Add(new Plan { dest = g, act = Act.Reveal, score = pos - riskVisible + 400, note = "显形省油" });
        }
    }

    private bool CanToggle(Infantry u)
    {
        if (!u.canStealth || u.IsStealthFormLocked) return false;
        if (u.stealthToggleCostsAction && (u.state == UnitState.Acted || u.hasActed)) return false;
        if (u.stealthAmmoMode == 1) return u.isStealthed ? u.currentRevealBullets > 0 : u.currentStealthBullets > 0;
        if (u.stealthAmmoMode == 2) return u.currentCamoBullets > 0;
        return true;
    }

    private float StealthCost(Infantry u)
    {
        float c = 0;
        if (u.stealthAmmoMode != 0) c += 400;   // 弹药有限
        if (u.consumeFuel && u.stealthFuelCostPerDay > 0) c += u.stealthFuelCostPerDay * 30;
        return c;
    }

    private bool WantsHide(Infantry u, Grids g, int hp)
    {
        if (!u.canStealth || u.isStealthed) return false;
        if (u.consumeFuel && u.stealthFuelCostPerDay > 0)
        {
            int perDay = u.dailyFuelConsumption + u.stealthFuelCostPerDay;
            if (u.fuel - perDay * 2 <= Math.Max(u.lowFuelThreshold, perDay)) return false;   // 油不够撑两天就别隐身
        }
        if (u.aiRole == 4) return true;   // 刺客：能隐就隐
        return ThreatLoss(u, g, hp, false) - ThreatLoss(u, g, hp, true) > StealthCost(u);
    }

    private bool WantsReveal(Infantry u, Grids g)
    {
        if (!u.isStealthed) return false;
        if (u.consumeFuel && u.stealthFuelCostPerDay > 0)
        {
            int perDay = u.dailyFuelConsumption + u.stealthFuelCostPerDay;
            bool critical = u.fuel - perDay <= Math.Max(u.lowFuelThreshold / 2, u.dailyFuelConsumption);
            bool safe = ThreatLoss(u, g, u.health, false) < UnitValue(u) * 0.1f;
            return critical || safe;
        }
        return false;
    }

    private float Terrain(Infantry u, Grids g) =>
        u.defenseBonusType == Infantry.DefenseBonusType.CanDefenseBonus ? g.TerrainDefenseBonus : 0f;

    /// <summary>站位价值：根据角色向目标推进 / 撤退修整 / 地形 / 不堵工厂</summary>
    private float PositionScore(Infantry u, Grids g)
    {
        float s = Terrain(u, g) * 300;

        // 不要堵住自家工厂
        if (g.city != null && g.city.canProduce && g.city.facilityTeam == aiTeam && g != u.grid) s -= 900;
        if (g.city != null && g.city.canProduce && g.city.facilityTeam == aiTeam && g == u.grid && gameManager.GetTeamFunds(aiTeam) >= 1000) s -= 900;

        // 撤退：残血 / 没弹 / 快没油
        bool lowHp = u.aiRetreatHpThreshold < 0 ? u.health <= u.maxHealth * 0.3f
                   : u.aiRetreatHpThreshold > 0 && u.health <= u.aiRetreatHpThreshold;
        bool noAmmo = u.maxPrimaryAmmo > 0 && u.maxPrimaryAmmo < 99 && u.currentPrimaryAmmo <= 0 && !u.CanUseSecondaryWeapon();
        bool lowFuel = u.consumeFuel && u.fuel <= Math.Max(u.lowFuelThreshold, (u.dailyFuelConsumption + (u.isStealthed ? u.stealthFuelCostPerDay : 0)) * 3);
        // 黑水晶优先：站进范围就能回血/补给（己方水晶由 AI 在全员走位后脉冲）
        float crystal = ctx.crystals.Count > 0 ? CrystalBonus(u, g) : 0;
        bool needCrystal = u.health < u.maxHealth * 0.8f || SupplyNeed(u) > 0;
        if (crystal > 0 || (ctx.crystals.Count > 0 && (lowHp || noAmmo || lowFuel || needCrystal)))
        {
            var cf = DistanceField(u, "crystal#" + u.GetInstanceId(), () => CrystalTilesFor(u).ToList());
            int c0 = FieldAt(cf, u.grid), c1 = FieldAt(cf, g);
            int reach = (lowHp || noAmmo || lowFuel) ? 998 : Math.Max(1, u.defaultMovePoints) * 2;   // 轻伤只在约两回合内可达时绕过去
            if (c0 < 999 && c0 <= reach && (lowHp || noAmmo || lowFuel || needCrystal))
                return s + (c0 - c1) * 300 + crystal * 2f + (c1 == 0 ? 2500 : 0);
        }
        s += crystal;

        if (lowHp || noAmmo || lowFuel)
        {
            var f = DistanceField(u, "haven", () => gameManager.gridManager.grids
                .Where(x => x?.city != null && x.city.facilityTeam == aiTeam && Heals(x.city, u))
                .Concat(ctx.mine.Where(m => m != u && m.canSupplyUnits).Select(m => m.grid)));
            int d0 = FieldAt(f, u.grid), d1 = FieldAt(f, g);
            if (d0 < 999)
            {
                s += (d0 - d1) * 250 + (d1 == 0 ? 1500 : 0);
                return s;
            }
        }

        if (u.canSupplyUnits && u.attackType == AttackType.NoAttack && !u.canTransportUnits)
        {
            var needy = ctx.mine.Where(m => m != u && SupplyNeed(m) > 0).Select(m => m.grid);
            return s + Approach(u, g, "needy#" + u.GetInstanceId(), needy, 120);
        }

        if (u.CanPerformRepair)
        {
            var hurt = ctx.mine.Where(m => m != u && m.health < m.maxHealth * 0.8f).Select(m => m.grid);
            return s + Approach(u, g, "hurt#" + u.GetInstanceId(), hurt, 120);
        }

        if (u.canTransportUnits)
        {
            if (u.transportedUnits.Count > 0)
                return s + Approach(u, g, "cap", ctx.capturables, 150);
            var riders = ctx.mine.Where(m => m != u && m.captureAbility != CaptureAbility.CannotCapture && u.CanTransportUnit(m)
                                             && FieldAt(DistanceField(m, "cap", ctx.capturables), m.grid) > m.defaultMovePoints * 2).Select(m => m.grid);
            if (ctx.mine.Any(m => m != u && m.captureAbility != CaptureAbility.CannotCapture)) return s + Approach(u, g, "riders#" + u.GetInstanceId(), riders, 100);
        }

        // 占领兵：去最近的未被认领据点
        if (u.aiRole == 3)   // 守备：贴近最受威胁的己方据点
        {
            var own = gameManager.gridManager.grids.Where(x => x?.city != null && x.city.facilityTeam == aiTeam).ToList();
            if (own.Count > 0 && ctx.enemies.Count > 0)
                own = own.OrderBy(x => ctx.enemies.Min(e => Dist(x, e.grid))).Take(2).ToList();
            if (own.Count > 0) return s + Approach(u, g, "guard", own, 120);
        }

        if (u.captureAbility != CaptureAbility.CannotCapture && ctx.capturables.Count > 0 && u.aiRole != 1)
        {
            var targets = CaptureTargetsFor(u);
            s += Approach(u, g, "cap#" + u.GetInstanceId(), targets, 140);
            if (ctx.capturables.Contains(g)) s += 300;
            return s;
        }

        // 战斗单位：向敌军推进（可见 > 记忆 > 敌方据点），远程停在射程边缘
        var goals = ctx.enemies.Select(e => e.grid).ToList();
        string key = "enemy";
        if (goals.Count == 0) { goals = ctx.remembered.Select(r => r.g).ToList(); key = "mem"; }
        if (goals.Count == 0)
        {
            goals = gameManager.gridManager.grids.Where(x => x?.city != null && IsEnemy(x.city.facilityTeam)).ToList();
            key = "efac";
        }
        if (goals.Count == 0) goals = ctx.capturables;
        if (IsIndirect(u) && ctx.enemies.Count > 0)
        {
            int best = ctx.enemies.Min(e => Math.Abs(Dist(g, e.grid) - MaxRange(u)));
            bool inRangeNextTurn = ctx.enemies.Any(e => CanHitAtDistance(u, Dist(g, e.grid)));
            return s - best * 120 + (inRangeNextTurn ? 600 : 0);
        }
        s += Approach(u, g, key, goals, 110);
        // 保护己方占领兵
        foreach (var m in ctx.mine)
            if (m != u && m.capturingGrid != null && Dist(m.grid, g) == 1) s += 150;
        return s;
    }

    private List<Grids> CaptureTargetsFor(Infantry u)
    {
        var t = ctx.capturables.Where(c => !ctx.captureClaims.TryGetValue(c, out var cl) || cl == u || !IsInstanceValid(cl)).ToList();
        return t.Count > 0 ? t : ctx.capturables;
    }

    private void ClaimCaptureTarget(Infantry u)
    {
        if (u.captureAbility == CaptureAbility.CannotCapture || u.grid == null) return;
        foreach (var k in moveClaims.Where(kv => kv.Value == u).Select(kv => kv.Key).ToList()) moveClaims.Remove(k);
        var f = DistanceField(u, "cap#" + u.GetInstanceId(), CaptureTargetsFor(u));
        Grids best = null; int bd = int.MaxValue;
        foreach (var c in CaptureTargetsFor(u))
        {
            // 反向距离场无法直接给出"哪个目标最近"，用曼哈顿距离挑选，再用距离场兜底
            int d = Dist(u.grid, c) * 2 + (ctx.captureClaims.ContainsKey(c) ? 50 : 0);
            if (d < bd) { bd = d; best = c; }
        }
        if (best != null && FieldAt(f, u.grid) < 999) moveClaims[best] = u;
    }

    private float Approach(Infantry u, Grids g, string key, IEnumerable<Grids> goals, float perStep)
    {
        var f = DistanceField(u, key, () => goals.Where(x => x != null).ToList());   // 惰性：缓存命中时不再枚举目标
        if (f.Count == 0) return 0;
        int d0 = FieldAt(f, u.grid), d1 = FieldAt(f, g);
        if (d1 >= 999) return -500;
        if (d0 >= 999) return -d1 * 10;
        return (d0 - d1) * perStep - d1 * 5;
    }

    private static bool Heals(Facility f, Infantry u)
    {
        if (f.healAmount <= 0 && f.healPercent <= 0) return false;
        if (f.supportedUnitTypesForHeal == null || f.supportedUnitTypesForHeal.Count == 0) return true;
        return f.supportedUnitTypesForHeal.Contains(u.UnitTypeId);
    }

    private static bool IsIndirect(Infantry u) => u.attackType != AttackType.NoAttack && (!u.canAttackAfterMoving || (u.useMinMaxAttackRange && u.minAttackRange > 1));

    // ==================== 执行 ====================
    private async void ExecutePlan(Plan p)
    {
        isBusy = true;
        var u = p.unit;
        float extra = 0;
        try
        {
            if (p.preReveal && u.isStealthed && CanToggle(u)) u.ToggleStealth();

            bool ambushed = false;
            if (p.dest != null && p.dest != u.grid)
            {
                var (final, amb, steps) = MoveAlong(u, p.path, p.act == Act.Load || p.act == Act.Merge);
                ambushed = amb || final != p.dest;
                extra = steps * moveStepTime;
                if (extra > 0)
                {
                    await ToSignal(GetTree().CreateTimer(extra), SceneTreeTimer.SignalName.Timeout);
                    if (!IsInstanceValid(this) || !isRunningAI) return;
                }
                if (!IsInstanceValid(u)) return;
            }

            if (ambushed)
            {
                Log("  遭遇伏击，行动中止");
                FinishUnit(u);
                return;
            }
            DoAction(p);
            if (IsInstanceValid(u) && p.act == Act.Wait) ClaimCaptureTarget(u);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[AI] 执行方案出错: {e}");
            if (IsInstanceValid(u)) FinishUnit(u);
        }
        finally
        {
            isBusy = false;
            if (IsInstanceValid(gameManager)) gameManager.fogOfWarManager?.OnUnitMoved();
            Next(stepDelay);
        }
    }

    private void DoAction(Plan p)
    {
        var u = p.unit;
        var gm = gameManager;
        switch (p.act)
        {
            case Act.Attack:
                if (p.target != null && IsInstanceValid(p.target) && p.target.health > 0)
                {
                    u.Attack(p.target);
                    gm.gridManager?.HideAttackRange();
                    gm.CallDeferred(nameof(GameManager.UpdateUnitLists));
                }
                break;
            case Act.AttackWeapon:
                if (p.weaponTarget != null && IsInstanceValid(p.weaponTarget) && !p.weaponTarget.isDestroyed)
                    u.AttackTarget(p.weaponTarget);
                break;
            case Act.Capture:
                if (u.CanCaptureCurrentGrid()) u.PerformCapture();
                break;
            case Act.Merge:
                if (p.target != null && IsInstanceValid(p.target) && gm.CanMerge(u, p.target))
                {
                    gm.MergeUnits(u, p.target);
                    return;   // 源单位已被移除
                }
                break;
            case Act.Load:
                if (p.target != null && IsInstanceValid(p.target) && p.target.LoadUnit(u))
                {
                    MarkActed(u);
                    return;
                }
                break;
            case Act.Unload:
                foreach (var (cargo, to) in p.unloads)
                {
                    if (cargo == null || !IsInstanceValid(cargo) || !u.CanUnloadToGrid(to)) continue;
                    if (to.infantries.Any(o => o != null && IsInstanceValid(o) && !o.isTransported)) continue;
                    if (u.UnloadUnit(cargo, to)) { MarkActed(cargo); handled.Add(cargo); }
                }
                break;
            case Act.Supply:
                u.PerformSupply();
                break;
            case Act.Repair:
                if (p.target != null && IsInstanceValid(p.target))
                {
                    u.PerformRepair(p.target);   // 通用修理模块（任意开启 canRepair 的单位）
                }
                break;
            case Act.Illuminate:
                if (p.actGrid != null && u.currentFlareAmmo > 0)
                {
                    u.currentFlareAmmo--;
                    gm.fogOfWarManager?.AddIllumination(p.actGrid, u.minIlluminationRange, u.maxIlluminationRange, u.flareDurationTurns);
                    gm.fogOfWarManager?.RefreshFog();
                }
                break;
            case Act.Explode:
                gm.ExecuteExplosion(u);
                if (!IsInstanceValid(u) || u.IsQueuedForDeletion() || u.health <= 0) return;
                break;
            case Act.Hide:
                if (!u.isStealthed) u.ToggleStealth();
                break;
            case Act.Reveal:
                if (u.isStealthed) u.ToggleStealth();
                break;
        }

        if (!IsInstanceValid(u) || u.IsQueuedForDeletion() || u.health <= 0) return;
        if (p.postHide && !u.isStealthed && CanToggle(u)) u.ToggleStealth();
        FinishUnit(u);
    }

    /// <summary>沿路径移动；遇到看不见的敌军（雾/隐身）立即截断。返回(最终格, 是否遭遇, 移动格数)</summary>
    private (Grids final, bool ambushed, int steps) MoveAlong(Infantry u, List<Grids> path, bool joining)
    {
        var start = u.grid;
        if (path == null || path.Count < 2) return (start, false, 0);
        int stop = path.Count - 1;
        bool ambushed = false;
        for (int i = 1; i < path.Count; i++)
        {
            var n = path[i];
            if (n.infantries.Any(o => o != null && IsInstanceValid(o) && !o.isTransported && o.team != u.team && o.team != TeamHelper.Player))
            {
                stop = i - 1; ambushed = true; break;
            }
        }
        // 终点需要能站（合流/上车终点由后续动作处理）
        if (!(joining && !ambushed))
            while (stop > 0 && !RealStayable(u, path[stop])) { stop--; ambushed = true; }

        var final = path[stop];
        if (final == start) return (start, ambushed, 0);

        int cost = 0, fuelCost = 0;
        for (int i = 1; i <= stop; i++)
        {
            int c = Math.Max(1, StepCost(u, path[i]));
            cost += c;
            if (path[i].gridType != GridType.TP) fuelCost += c;
        }

        // 离开正在占领的据点 → 占领中断
        if (u.capturingGrid != null && u.capturingGrid != final)
        {
            u.capturingGrid.city?.OnCaptureInterrupted(u);
            u.currentCaptureProgress = 0;
            u.capturingGrid = null;
        }

        start.infantries.Remove(u);
        if (start.infantry == u) start.infantry = start.infantries.FirstOrDefault();
        u.grid = final;
        if (!final.infantries.Contains(u)) final.infantries.Add(u);
        if (final.infantry == null) final.infantry = u;

        u.movePoints = Math.Max(0, u.movePoints - cost);
        if (u.consumeFuel) u.ConsumeFuel(fuelCost);
        u.isMoved = true;
        u.state = UnitState.Moved;
        u.originalGrid = start;
        u.movedThisTurnForAutoRepair = true;   // 与 GridManager.MoveTo 一致：行动结束时触发 autoRepairAfterMove

        // 动画
        var tween = u.CreateTween();
        for (int i = 1; i <= stop; i++) tween.TweenProperty(u, "position", path[i].Position, moveStepTime);
        FollowCamera(u, final);
        return (final, ambushed, stop);
    }

    private void FollowCamera(Infantry u, Grids g)
    {
        if (!followCamera || !u.IsVisibleInTree()) return;
        var cam = GetViewport()?.GetCamera2D();
        if (cam == null) return;
        var t = cam.CreateTween();
        t.TweenProperty(cam, "position", g.Position, 0.25f).SetTrans(Tween.TransitionType.Sine);
    }

    /// <summary>修理能力参数（字段已统一到 Infantry 基类）</summary>
    private static (bool can, bool afterMove, int min, int max, int mode, int amount) RepairParams(Infantry u)
        => (u.CanPerformRepair, u.canRepairAfterMove, u.minRepairRange, u.maxRepairRange, u.repairTargetMode, u.repairAmountPerAction);

    private void FocusCamera(Grids g)
    {
        if (!followCamera || g == null) return;
        var cam = GetViewport()?.GetCamera2D();
        if (cam == null) return;
        cam.CreateTween().TweenProperty(cam, "position", g.Position, 0.25f).SetTrans(Tween.TransitionType.Sine);
    }

    private void MarkActed(Infantry u)
    {
        u.isMoved = true;
        u.isAttacked = true;
        u.state = UnitState.Acted;
        u.originalGrid = null;
    }

    private void FinishUnit(Infantry u)
    {
        if (u == null || !IsInstanceValid(u) || u.IsQueuedForDeletion()) return;
        if (u.state != UnitState.Acted) MarkActed(u);
        u.SetWaitVisual(true);
    }

    // ==================== 生产 ====================
    private void Produce()
    {
        var gm = gameManager;
        int funds = gm.GetTeamFunds(aiTeam);
        var facilities = gm.gridManager.grids
            .Where(g => g?.city != null && g.city.canProduce && (g.city.facilityTeam == aiTeam || g.city.isPublicFacility)
                        && g.city.CanProduceNow() && g.infantries.Count == 0 && g.weapons.Count == 0
                        && g.city.producibleUnitNames != null && g.city.producibleUnitNames.Count > 0)
            .OrderBy(g => ctx.enemies.Count == 0 ? 0 : ctx.enemies.Min(e => Dist(g, e.grid)))   // 前线工厂优先
            .ToList();
        if (facilities.Count == 0) { Log($"生产：无可用工厂 资金{funds}"); return; }

        var myCounts = ctx.mine.GroupBy(u => u.UnitTypeId).ToDictionary(x => x.Key, x => x.Count());
        // 占领兵计数包含正在运输中的
        int capturers = (gm.unitManager?.AllUnits ?? new List<Infantry>())
            .Count(u => u != null && IsInstanceValid(u) && u.team == aiTeam && u.health > 0 && u.captureAbility != CaptureAbility.CannotCapture);
        int income = Math.Max(1000, gm.gridManager.grids.Count(x => x?.city != null && x.city.facilityTeam == aiTeam) * 1000);
        var known = ctx.enemies.Select(e => (e, e.health)).Concat(ctx.remembered.Select(r => (r.u, r.hp))).ToList();

        for (int i = 0; i < facilities.Count; i++)
        {
            var fg = facilities[i];
            int remainingFacilities = facilities.Count - i - 1;
            string bestName = null; float bestScore = float.NegativeInfinity;
            float saveScore = float.NegativeInfinity; string saveName = null;
            var dbg = new List<string>();
            foreach (var name in fg.city.producibleUnitNames)
            {
                if (!UnitProductionDatabase.HasUnit(name)) continue;
                int cost = UnitProductionDatabase.GetInfo(name).Cost;
                if (cost <= 0) continue;
                if (cost > funds)
                {
                    // 攒钱候选：两回合内买得起
                    if (cost <= funds + income * 2)
                    {
                        float ss = ProductionScore(name, cost, known, myCounts, capturers);
                        if (ss > saveScore) { saveScore = ss; saveName = name; }
                    }
                    continue;
                }
                float s = ProductionScore(name, cost, known, myCounts, capturers);
                // 钱多工厂少：每个工厂一回合只能出一个，钱越宽裕越该买贵的好兵
                s += cost * 0.35f * Math.Min(1f, funds / (float)(remainingFacilities + 1) / 15000f);
                // 留够钱给后面的工厂至少出一个步兵（需要占领时）
                if (ctx.capturables.Count > capturers && remainingFacilities > 0 && funds - cost < 1000 * Math.Min(remainingFacilities, 2)) s *= 0.7f;
                dbg.Add($"{name}:{s:F0}");
                if (s > bestScore) { bestScore = s; bestName = name; }
            }
            // 闲钱=零价值：只要不是明显亏本就出兵；钱攒得越多门槛越低
            float threshold = -3000f - funds * 0.1f;
            if (bestName != null && bestScore < 0 && saveName != null && saveScore > bestScore + 1500
                && remainingFacilities == 0 && ctx.capturables.Count <= capturers)
            { Log($"生产：{fg.GridIndex} 攒钱买 {saveName}（{saveScore:F0} > {bestName} {bestScore:F0}）"); continue; }
            if (bestName == null || bestScore < threshold) { Log($"生产：{fg.GridIndex} 不出兵 资金{funds} [{string.Join(",", dbg)}]"); continue; }
            if (gm.AIProduceUnit(fg.city, fg, bestName, aiTeam))
            {
                funds = gm.GetTeamFunds(aiTeam);
                myCounts[bestName] = myCounts.GetValueOrDefault(bestName) + 1;
                var cfg = UnitRegistry.Get(bestName);
                if (cfg != null && cfg.CaptureAbility != CaptureAbility.CannotCapture) capturers++;
                Log($"生产 {bestName} @{fg.GridIndex} 分{bestScore:F0} 余{funds}");
            }
        }
    }

    private float ProductionScore(string name, int cost, List<(Infantry e, int hp)> known, Dictionary<string, int> myCounts, int capturers)
    {
        var cfg = UnitRegistry.Get(name);
        float s = 0;

        // 1) 对敌军的攻防交换（查攻防表；有同类在场时用它的实例表）
        if (known.Count > 0)
        {
            float trade = 0;
            foreach (var (e, hp) in known)
            {
                if (e == null || !IsInstanceValid(e)) continue;
                // 按兵种ID+参照兵种查表（自定义兵种同样适用）；优先用场上同类/场景原型的实例攻防表
                var proto = ctx.mine.FirstOrDefault(m => m.UnitTypeId == name) ?? GetPrototype(name);
                string tpl = proto?.TemplateId ?? name;
                int dealt = Math.Max(Infantry.LookupDefaultMatrix(name, tpl, e.UnitTypeId, e.TemplateId), Infantry.LookupDefaultMatrix(name, tpl, e.UnitTypeId, e.TemplateId, true));
                if (proto != null) dealt = Math.Max(proto.GetPrimaryDamageFromMatrix(e), proto.GetSecondaryDamageFromMatrix(e));
                if (e.isStealthed && Array.IndexOf(e.stealthAttackableBy ?? Array.Empty<string>(), name) < 0) dealt = 0;
                int taken = Math.Max(Infantry.LookupDefaultMatrix(e.UnitTypeId, e.TemplateId, name, tpl), Infantry.LookupDefaultMatrix(e.UnitTypeId, e.TemplateId, name, tpl, true));
                if (proto != null) taken = Math.Max(taken, Math.Max(e.GetMatrixDamage(proto, false), e.GetMatrixDamage(proto, true)));
                trade += dealt / 100f * Math.Max(e.cost, 1000) * (hp / (float)e.maxHealth) - taken / 100f * cost * 0.6f;
            }
            s += trade / known.Count * 2.2f;
        }
        else
        {
            s += cost * 0.15f;   // 没情报：偏向均衡
        }

        // 2) 占领需求
        bool canCap = cfg != null && cfg.CaptureAbility != CaptureAbility.CannotCapture;
        int wantCap = Math.Min(ctx.capturables.Count, 2 + ctx.capturables.Count / 3);
        if (canCap && capturers < wantCap) s += 2600 - cost * 0.3f;
        if (canCap && capturers >= wantCap + 2) s -= 1200;

        // 3) 功能需求
        if (cfg != null)
        {
            if (cfg.CanIlluminate && ctx.fogOn && myCounts.GetValueOrDefault(name) == 0) s += 1800;
            if (cfg.CanTransportUnits && myCounts.GetValueOrDefault(name) == 0 && capturers >= 2 && ctx.capturables.Count >= 4) s += 900;
            // 纯运输（无武器）：已有运输或没有可运的占领兵时，不再买
            if (cfg.CanTransportUnits && !cfg.HasPrimaryWeapon && !cfg.HasSecondaryWeapon)
            {
                int transports = ctx.mine.Count(m => m.canTransportUnits && !m.hasPrimaryWeapon && !m.hasSecondaryWeapon);
                if (transports >= 1 + capturers / 4 || capturers == 0) s -= 4000;
            }
            if (cfg.CanSupplyUnits && ctx.mine.Count(m => SupplyNeed(m) > 500) >= 2 && myCounts.GetValueOrDefault(name) == 0) s += 1200;
            if (cfg.AttackType == AttackType.NoAttack && !cfg.CanIlluminate && !cfg.CanTransportUnits && !cfg.CanSupplyUnits) s -= 2000;
        }
        // 能力驱动：候选兵种可隐身、且已知敌军里没有它白名单内的兵种 → 对面没反隐手段
        var pr = GetPrototype(name);
        if (pr != null && pr.canStealth && known.Count > 0)
        {
            var by = pr.stealthAttackableBy ?? Array.Empty<string>();
            if (!known.Any(k => Array.IndexOf(by, k.e.UnitTypeId) >= 0)) s += 2500;
        }

        // 4) 多样性：同兵种越多越不想要
        int same = myCounts.GetValueOrDefault(name);
        s -= same * cost * 0.15f + (same >= 3 ? (same - 2) * 800 : 0);

        // 5) 高价兵要有对应价值
        if (known.Count == 0 && cost > 15000) s -= 2000;
        return s;
    }

    // 兵种原型缓存：实例化场景（不入树）读取 Export 能力字段，用于生产时按"能力"而非类型名判断
    private static readonly Dictionary<string, Infantry> prototypes = new();
    private static Infantry GetPrototype(string name)
    {
        if (prototypes.TryGetValue(name, out var cached)) return cached;
        Infantry inst = null;
        if (UnitProductionDatabase.Units.TryGetValue(name, out var info))
        {
            var scene = GD.Load<PackedScene>(info.ScenePath);
            if (scene != null) inst = scene.Instantiate() as Infantry;
        }
        prototypes[name] = inst;   // 不入场景树，_Ready 不执行，只读检查器字段
        return inst;
    }

    private void Log(string msg)
    {
        if (logDecisions) GD.Print("[AI] " + msg);
    }
}
