// BlackCannon.cs - 黑炮：三角形范围，固定百分比伤害，支持自定义目标选择和改良伤害模式
// ✅ 新增：完整弹药系统 + 多回合冷却系统（还原原版高级战争原型炮/完全体差异）
using Godot;
using System.Collections.Generic;
using System.Linq;

// BlackCannon · AmmoCooldown（由 BlackCannon.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class BlackCannon
{
    public override bool CanAttack() => !hasActed && remainingAttacks > 0 && HasAmmoAndCooldown();



// ✅ 新增：初始化冷却状态（开局默认冷却完毕）
private void InitializeCooldownState()
{
    // 基础状态（由 base.OnTurnStart() 提供）
    hasActed = false;
    remainingAttacks = maxAttacksPerTurn;
    
    if (useCooldownSystem)
    {
        // ✅ 开局默认冷却完毕，可以立即攻击
        turnsSinceLastAttack = 0;
        cooldownReady = true;
        attacksRemainingInCycle = attacksPerCooldown;
        totalTurnsPassed = 0;
    }
    else
    {
        // 无冷却系统，使用默认攻击次数
        turnsSinceLastAttack = 0;
        cooldownReady = true;
        attacksRemainingInCycle = maxAttacksPerTurn;
    }
    
    UpdateAmmoVisual();
}

    // ========== ✅ 新增：弹药与冷却综合检查 ==========
    public bool HasAmmoAndCooldown()
    {
        // 1. 检查弹药
        if (!HasFireAmmo()) return false;   // ✅ 8弹药许可

        // 2. 检查冷却
        if (useCooldownSystem)
        {
            if (!cooldownReady)
            {
                return false;
            }

            if (attacksRemainingInCycle <= 0)
            {
                return false;
            }
        }

        return true;
    }

    // ========== ✅ 新增：消耗弹药 ==========
    public bool ConsumeAmmo()
    {
        if (!ConsumeFireAmmo()) return false;   // ✅ 8弹药消耗映射
        UpdateAmmoVisual();
        return true;
    }

    // ========== ✅ 新增：补给弹药（APC调用） ==========
    public override bool ResupplyAmmo()
    {
        int oldAmmo = currentAmmo;
        if (!base.ResupplyAmmo()) return false;   // ✅ 基类处理白名单+旧池
        UpdateAmmoVisual();
        return currentAmmo != oldAmmo;
    }

    

    // ========== ✅ 新增：弹药视觉更新 ==========
    public void UpdateAmmoVisual()
    {
        // 如果有弹药标签则更新（可选UI组件）
        var ammoLabel = GetNodeOrNull<Label>("AmmoLabel");
        if (ammoLabel != null)
        {
            if (useAmmoSystem)
            {
                ammoLabel.Text = $"{currentAmmo}/{maxAmmo}";
                ammoLabel.Modulate = currentAmmo <= 0 ? Colors.Red : 
                                    (currentAmmo <= maxAmmo / 3 ? Colors.Yellow : Colors.White);
                ammoLabel.Show();
            }
            else
            {
                ammoLabel.Text = "∞";
                ammoLabel.Modulate = Colors.Green;
                ammoLabel.Show();
            }
        }

        // 低弹药闪烁图标（类似Infantry的noAmmoIcon）
        var noAmmoIcon = GetNodeOrNull<AnimatedSprite2D>("NoAmmoIcon");
        if (noAmmoIcon != null)
        {
            if (!useAmmoSystem)
            {
                noAmmoIcon.Visible = false;
            }
            else if (currentAmmo <= 0)
            {
                noAmmoIcon.Visible = true;
                if (noAmmoIcon.SpriteFrames != null && noAmmoIcon.SpriteFrames.HasAnimation("noammo"))
                    noAmmoIcon.Play("noammo");
            }
            else if (currentAmmo <= maxAmmo / 3)
            {
                // 低弹药闪烁
                noAmmoIcon.Visible = (Time.GetTicksMsec() / 500) % 2 == 0;
                if (noAmmoIcon.SpriteFrames != null && noAmmoIcon.SpriteFrames.HasAnimation("lowammo"))
                    noAmmoIcon.Play("lowammo");
            }
            else
            {
                noAmmoIcon.Visible = false;
            }
        }
    }

    // ========== ✅ 新增：补给特效 ==========
    private void ShowAmmoResupplyEffect()
    {
        var effect = new SupplyEffect();
        effect.Setup(Vector2.Zero, "Ammo+", new Color(0.9f, 0.7f, 0.2f));
        AddChild(effect);
    }

    // ========== ✅ 新增：回合开始处理（冷却+弹药系统） ==========
public override void OnTurnStart()
{
    base.OnTurnStart(); // hasActed=false, remainingAttacks=maxAttacksPerTurn

    if (useCooldownSystem)
    {
        // ✅ 关键：只有冷却未就绪时才增加计数
        if (!cooldownReady)
        {
            turnsSinceLastAttack++;
            totalTurnsPassed++;
            
            // 检查是否完成冷却
            if (turnsSinceLastAttack >= cooldownTurns)
            {
                // 新周期开始！
                turnsSinceLastAttack = 0;
                cooldownReady = true;
                
                // 刷新攻击次数
                if (storeAttacks)
                {
                    attacksRemainingInCycle = Mathf.Min(attacksPerCooldown + attacksRemainingInCycle, attacksPerCooldown * 2);
                }
                else
                {
                    attacksRemainingInCycle = attacksPerCooldown;
                }
            }
            else
            {
            }
        }
        else
        {
            // 冷却已就绪，检查是否还有剩余次数
            if (attacksRemainingInCycle <= 0 && !storeAttacks)
            {
                // 次数用完且不留存，需要重新冷却
                cooldownReady = false;
                turnsSinceLastAttack = 0; // 从0开始计数，下次调用时变成1
            }
            else
            {
            }
        }
    }
    
    UpdateAmmoVisual();
    
    // 根据弹药和冷却状态设置视觉
    if (!HasAmmoAndCooldown())
    {
        if (animSprite != null)
            animSprite.Modulate = new Color(0.7f, 0.7f, 0.7f, 1f);
    }
    else
    {
        SetVisualNormal();
    }
}

    // ========== ✅ 新增：回合结束处理 ==========
    public override void OnTurnEnd()
    {
        base.OnTurnEnd();

        if (useCooldownSystem)
        {
            if (!storeAttacks)
            {
                // 不存次数：回合结束清空剩余次数
                if (attacksRemainingInCycle > 0)
                {
                    attacksRemainingInCycle = 0;
                }
            }
            else
            {
                // 存次数：保留到下一周期（已在OnTurnStart中处理累加）
            }
        }
    }
}
