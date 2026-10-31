// Laser.cs - 高级激光炮系统（修复版）
// 修复内容：
// 1. 多次攻击同步 remainingAttacks 和 attacksRemainingInCycle
// 2. 添加局内角度配置API（EnableAngle/DisableAngle/SetFireAngles等）
// 3. ✅ 移除 attackedThisRound 限制：允许同一回合多次攻击同一目标
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Laser · TurnAmmo（由 Laser.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class Laser
{

    // ========== ✅ 核心修复：回合开始同步 remainingAttacks ==========

    public override void OnTurnStart()
    {
        base.OnTurnStart();
        // 不再限制同一回合重复攻击同一目标

        if (useCooldownSystem)
        {
            if (!cooldownReady)
            {
                turnsSinceLastAttack++;
                totalTurnsPassed++;
                if (turnsSinceLastAttack >= cooldownTurns)
                {
                    turnsSinceLastAttack = 0;
                    cooldownReady = true;
                    if (storeAttacks)
                        attacksRemainingInCycle = Mathf.Min(attacksPerCooldown + attacksRemainingInCycle, attacksPerCooldown * 2);
                    else
                        attacksRemainingInCycle = attacksPerCooldown;
                }
            }
            else if (attacksRemainingInCycle <= 0 && !storeAttacks)
            {
                cooldownReady = false;
                turnsSinceLastAttack = 0;
            }

            // ✅ 修复：同步 remainingAttacks 为 cooldown 实际可用次数
            if (cooldownReady && attacksRemainingInCycle > 0)
                remainingAttacks = attacksRemainingInCycle;
            else
                remainingAttacks = 0;
        }
        else
        {
            // 无冷却系统：使用 maxAttacksPerTurn
            remainingAttacks = maxAttacksPerTurn;
            attacksRemainingInCycle = maxAttacksPerTurn;
        }

        UpdateAmmoVisual();
        UpdateLaserVisual();

        if (enableAutoAI && !hasActed)
        {
            var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
            if (gm != null && gm.IsTurnPhaseValid(team))
            {
                var timer = GetTree().CreateTimer(0.5f);
                timer.Timeout += () => ExecuteAIAutoAttack();
            }
        }
    }

    public override void OnTurnEnd()
    {
        base.OnTurnEnd();
        if (useCooldownSystem && !storeAttacks && attacksRemainingInCycle > 0)
            attacksRemainingInCycle = 0;
    }

    private void InitializeCooldownState()
    {
        hasActed = false;
        remainingAttacks = maxAttacksPerTurn;

        if (useCooldownSystem)
        {
            turnsSinceLastAttack = 0;
            cooldownReady = true;
            attacksRemainingInCycle = attacksPerCooldown;
            totalTurnsPassed = 0;
            // ✅ 同步初始值
            remainingAttacks = attacksPerCooldown;
        }
        else
        {
            attacksRemainingInCycle = maxAttacksPerTurn;
        }
    }

    public bool HasAmmoAndCooldown()
    {
        if (!HasFireAmmo()) return false;
        if (useCooldownSystem && !cooldownReady) return false;
        if (useCooldownSystem && attacksRemainingInCycle <= 0) return false;
        return true;
    }

    public bool ConsumeAmmo()
    {
        if (!ConsumeFireAmmo()) return false;
        UpdateAmmoVisual();
        return true;
    }

    public override bool ResupplyAmmo()
    {
        if (!base.ResupplyAmmo()) return false;   // ✅ 基类处理白名单+旧池
        UpdateAmmoVisual();
        return true;
    }
}
