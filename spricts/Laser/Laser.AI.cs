// Laser.cs - 高级激光炮系统（修复版）
// 修复内容：
// 1. 多次攻击同步 remainingAttacks 和 attacksRemainingInCycle
// 2. 添加局内角度配置API（EnableAngle/DisableAngle/SetFireAngles等）
// 3. ✅ 移除 attackedThisRound 限制：允许同一回合多次攻击同一目标
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Laser · AI（由 Laser.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class Laser
{

    // ========== AI自动操作 ==========

    public void ExecuteAIAutoAttack()
    {
        if (!enableAutoAI || hasActed || isAIExecuting) return;
        var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm == null || !gm.IsTurnPhaseValid(team)) return;
        if (!CanAttack()) { MarkAIActed(); return; }

        isAIExecuting = true;
        aiCurrentRotationAttempts = 0;
        aiMarkedActed = false;
        AIAttackSequence();
    }

    // ✅ P0/P-1 兵器强制 AI：绕过 phase 和 enableAutoAI 检查
    public override void ExecuteAI()
    {
        if (hasActed || isAIExecuting) return;
        if (!CanAttack()) { MarkAIActed(); return; }
        isAIExecuting = true;
        aiCurrentRotationAttempts = 0;
        aiMarkedActed = false;
        AIAttackSequence();
    }

    private void AIAttackSequence()
    {
        if (!IsInstanceValid(this) || hasActed || aiMarkedActed)
        { isAIExecuting = false; return; }

        if (!CanAttack())
        { MarkAIActed(); isAIExecuting = false; return; }

        var allTargets = GetAllValidTargets();
        var validTargets = allTargets.Where(t => IsInstanceValid(t.Target)).ToList();

        if (validTargets.Count == 0)
        {
            if (aiAutoRotate && aiCurrentRotationAttempts < aiMaxRotationAttempts)
            {
                aiCurrentRotationAttempts++;
                var bestAngle = AIFindBestAngle();
                if (bestAngle.HasValue && !currentFireAngles.Contains(bestAngle.Value))
                {
                    AIRotateToAngle(bestAngle.Value);
                    var timer = GetTree().CreateTimer(0.2f);
                    timer.Timeout += () => AIAttackSequence();
                    return;
                }
            }
            MarkAIActed();
            isAIExecuting = false;
            return;
        }

        AIExecuteGlobalAttack(validTargets);

        if (aiUseAllAttacks && CanAttack())
        {
            var timer = GetTree().CreateTimer(0.4f);
            timer.Timeout += () => AIAttackSequence();
        }
        else
        { MarkAIActed(); isAIExecuting = false; }
    }

    private float? AIFindBestAngle()
    {
        if (enabledAngles.Count == 0) return null;
        var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.gridManager?.map == null) return null;

        var originalAngles = new List<float>(currentFireAngles);
        float? bestAngle = null;
        int bestTargetCount = 0;

        foreach (var angle in enabledAngles)
        {
            currentFireAngles = new List<float> { angle };
            rayCache.Remove(angle);
            var targets = GetAllValidTargets().Where(t => IsInstanceValid(t.Target)).ToList();
            if (targets.Count > bestTargetCount)
            {
                bestAngle = angle;
                bestTargetCount = targets.Count;
            }
        }
        currentFireAngles = originalAngles;
        return bestAngle;
    }

    private void AIRotateToAngle(float targetAngle)
    {
        if (currentFireAngles.Contains(targetAngle)) return;
        if (enabledAngles.Count == 0) return;
        float currentAngle = currentFireAngles.Count > 0 ? currentFireAngles[0] : enabledAngles[0];
        int currentIdx = enabledAngles.IndexOf(currentAngle);
        int targetIdx = enabledAngles.IndexOf(targetAngle);
        if (currentIdx < 0 || targetIdx < 0) return;
        int count = enabledAngles.Count;
        int diff = targetIdx - currentIdx;
        if (diff > count / 2) diff -= count;
        if (diff < -count / 2) diff += count;
        bool clockwise = diff > 0;
        int steps = Math.Abs(diff);
        for (int i = 0; i < steps; i++) RotateLaser(clockwise);
    }

    private void AIExecuteGlobalAttack(List<LaserTargetInfo> targets)
    {
        if (!IsInstanceValid(this)) return;
        if (!ConsumeFireAmmo()) return;   // ✅ 8弹药：支持任意弹种消耗映射
        if (useAmmoSystem) UpdateAmmoVisual();
        if (useCooldownSystem)
        {
            attacksRemainingInCycle--;
            remainingAttacks = attacksRemainingInCycle;
        }
        else
        {
            remainingAttacks--;
        }

        foreach (var t in targets)
        {
            if (t.Target is Infantry infantry)
                ApplyDamageToTarget(infantry, t.BaseDamagePercent);
            else if (t.Target is Weapon weapon)
                ApplyDamageToWeapon(weapon, t.BaseDamagePercent);
        }

        if (aiShowEffects)
        {
            var cam = GetViewport()?.GetCamera2D();
            if (cam != null)
            {
                var tween = cam.CreateTween().SetParallel(false);
                for (int i = 0; i < 3; i++)
                {
                    var rnd = new Vector2(GD.Randf() * 6f - 3f, GD.Randf() * 6f - 3f);
                    tween.TweenProperty(cam, "position", cam.Position + rnd, 0.05f);
                }
                tween.TweenProperty(cam, "position", cam.Position, 0.05f);
            }
            foreach (var angleGroup in targets.GroupBy(t => t.Angle))
            {
                float angle = angleGroup.Key;
                var angleTargets = angleGroup.ToList();
                if (angleTargets.Count == 0) continue;
                Vector2 endPoint = CalculateLaserEffectEndPoint(angle, angleTargets);
                var laserLine = new LaserLineEffect();
                laserLine.Setup(grid.Position + new Vector2(16, 16), endPoint, GetTeamColor());
                GetTree()?.CurrentScene?.AddChild(laserLine);
                foreach (var t in angleTargets)
                {
                    var hit = new LaserHitEffect();
                    hit.Position = t.Target.GlobalPosition;
                    GetTree()?.CurrentScene?.AddChild(hit);
                    int damage = 0;
                    if (t.Target is Infantry inf)
                        damage = Mathf.RoundToInt(inf.maxHealth * t.BaseDamagePercent);
                    else if (t.Target is Weapon wp)
                        damage = Mathf.RoundToInt(wp.maxHealth * t.BaseDamagePercent);
                    damage = Mathf.Max(1, damage);
                    AIShowDamageNumber(t.Target.GlobalPosition, damage);
                }
            }
        }
    }

    private void AIShowDamageNumber(Vector2 position, int damage)
    {
        var label = new Label();
        label.Text = $"-{damage}";
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new Color(1, 0.3f, 0.3f));
        label.Position = position + new Vector2(0, -15);
        label.ZIndex = 1000;
        GetTree()?.CurrentScene?.AddChild(label);
        var tween = CreateTween();
        tween.TweenProperty(label, "position:y", position.Y - 40, 0.8f)
             .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(label, "modulate:a", 0f, 0.8f);
        tween.TweenCallback(Callable.From(() => { if (IsInstanceValid(label)) label.QueueFree(); }));
    }

    private void MarkAIActed()
    {
        if (aiMarkedActed) return;
        aiMarkedActed = true;
        hasActed = true;
        SetVisualDark();
        var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.selectedWeapon == this) gm.selectedWeapon = null;
        gm?.gridManager?.ClearWeaponRange();
    }
}
