// Laser.cs - 高级激光炮系统（修复版）
// 修复内容：
// 1. 多次攻击同步 remainingAttacks 和 attacksRemainingInCycle
// 2. 添加局内角度配置API（EnableAngle/DisableAngle/SetFireAngles等）
// 3. ✅ 移除 attackedThisRound 限制：允许同一回合多次攻击同一目标
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Laser · Visual（由 Laser.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class Laser
{

    // ========== 视觉更新 ==========

    public void UpdateLaserVisual()
    {
        if (_animSprite == null) return;
        string animPrefix = team == "Player2" ? "Laser2" : "Laser1";
        string animName = animPrefix;
        if (!HasAmmoAndCooldown())
            animName += "_disabled";
        else if (hasActed)
            animName += "_acted";
        if (_animSprite.SpriteFrames != null && _animSprite.SpriteFrames.HasAnimation(animName))
            _animSprite.Play(animName);
        else if (_animSprite.SpriteFrames != null && _animSprite.SpriteFrames.HasAnimation(animPrefix))
            _animSprite.Play(animPrefix);
    }

    public void UpdateAmmoVisual()
    {
        if (_ammoLabel == null) return;
        if (useAmmoSystem)
        {
            _ammoLabel.Text = $"{currentAmmo}/{maxAmmo}";
            _ammoLabel.Modulate = currentAmmo <= 0 ? Colors.Red :
                                (currentAmmo <= maxAmmo / 3 ? Colors.Yellow : Colors.White);
            _ammoLabel.Show();
        }
        else
        {
            _ammoLabel.Text = "∞";
            _ammoLabel.Modulate = Colors.Green;
            _ammoLabel.Show();
        }
    }
}
