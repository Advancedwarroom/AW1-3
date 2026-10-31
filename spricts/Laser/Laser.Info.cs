// Laser.cs - 高级激光炮系统（修复版）
// 修复内容：
// 1. 多次攻击同步 remainingAttacks 和 attacksRemainingInCycle
// 2. 添加局内角度配置API（EnableAngle/DisableAngle/SetFireAngles等）
// 3. ✅ 移除 attackedThisRound 限制：允许同一回合多次攻击同一目标
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Laser · Info（由 Laser.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class Laser
{

    public string GetLaserFullInfo()
    {
        string info = "";
        info += $"[激光炮] {Name}\n";
        info += $"团队: {team}\n";
        info += $"血量: {health}/{maxHealth}\n";
        info += $"状态: {(hasActed ? TranslateGroup.T("已行动") : TranslateGroup.T("待机"))}\n";
        if (enableAutoAI)
        {
            info += $"\n=== AI自动操作 ===\n";
            info += $"AI模式: 已启用\n";
            info += $"自动旋转: {(aiAutoRotate ? TranslateGroup.T("是") : TranslateGroup.T("否"))}\n";
            info += $"用完所有攻击: {(aiUseAllAttacks ? TranslateGroup.T("是") : TranslateGroup.T("否"))}\n";
        }
        info += $"\n=== 角度系统 ===\n";
        info += $"启用角度: {string.Join(", ", enabledAngles.Select(a => $"{a}°"))}\n";
        info += $"当前发射: {string.Join(", ", currentFireAngles.Select(a => $"{a}°"))}\n";
        info += $"可旋转: {(canRotate ? "✓" : "✗")}\n";
        info += $"\n=== 射程 ===\n";
        info += $"射程: {(useInfiniteRange ? TranslateGroup.T("无限") : $"{maxLaserLength}格")}\n";
        info += $"\n=== 伤害配置 ===\n";
        if (useContactAreaDamage)
        {
            info += $"[接触面积衰减模式]\n";
            info += $"穿过中心: {maxContactDamagePercent * 100:F0}%\n";
            info += $"擦边角: {minContactDamagePercent * 100:F0}%\n";
        }
        if (useModifiedDamage)
        {
            info += $"[改良模式]\n";
            info += $"血量{modifiedHealthPercent * 100:F0}% {(modifiedHealthPercent > 0 ? TranslateGroup.T("扣") : TranslateGroup.T("回"))}\n";
            info += $"弹药: {modifiedAmmoPercent * 100:F0}%\n";
            info += $"燃料: {modifiedFuelPercent * 100:F0}%\n";
        }
        else
        {
            info += $"[传统模式]\n";
            info += $"固定伤害: {fixedDamagePercent * 100:F0}%\n";
        }
        if (useContactAreaDamage)
            info += $"(已叠加接触面积衰减)\n";
        info += $"可摧毁: {(CanDestroy ? TranslateGroup.T("是") : TranslateGroup.T("否（锁血1HP）"))}\n";
        info += $"\n=== 穿透系统 ===\n";
        info += $"每条线穿透上限: {(maxPiercePerLine > 0 ? $"{maxPiercePerLine}个" : TranslateGroup.T("无限"))}\n";
        info += $"目标模式: {targetSelectionMode}\n";
        info += $"\n=== 弹药 ===\n";
        if (useAmmoSystem)
            info += $"弹药: {currentAmmo}/{maxAmmo}\n";
        else
            info += $"弹药: 无限\n";
        info += $"\n=== 冷却 ===\n";
        if (useCooldownSystem)
        {
            info += $"冷却周期: {cooldownTurns}回合\n";
            info += $"每周期次数: {attacksPerCooldown}\n";
            info += $"存次数: {(storeAttacks ? TranslateGroup.T("是") : TranslateGroup.T("否"))}\n";
            info += $"剩余次数: {attacksRemainingInCycle}\n";
            info += $"冷却就绪: {(cooldownReady ? "✓" : "✗")}\n";
        }
        else
        {
            info += $"冷却: 无\n";
        }
        info += $"\n[操作说明]\n";
        info += $"• 左键点击任意射线格子: 所有线同时发射\n";
        info += $"• R键: 顺时针旋转\n";
        info += $"• 左滑/右滑: 旋转方向\n";
        return info;
    }
}
