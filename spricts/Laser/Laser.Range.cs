// Laser.cs - 高级激光炮系统（修复版）
// 修复内容：
// 1. 多次攻击同步 remainingAttacks 和 attacksRemainingInCycle
// 2. 添加局内角度配置API（EnableAngle/DisableAngle/SetFireAngles等）
// 3. ✅ 移除 attackedThisRound 限制：允许同一回合多次攻击同一目标
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Laser · Range（由 Laser.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class Laser
{

    // ========== 攻击范围显示 ==========

    public override void ShowAttackRange()
    {
        if (enableAutoAI && hasActed) return;

        var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.gridManager == null) return;

        if (!HasAmmoAndCooldown())
        {
            ShowAttackRangeAsDisabled(gm);
            return;
        }

        gm.gridManager.ClearWeaponRange();
        Grids.IsForceActionMode = true;

        var allRayGrids = new HashSet<Grids>();
        var targetableGrids = new HashSet<Grids>();
        var fog = gm.fogOfWarManager;
        bool isFogEnabled = fog != null && fog.isFogOfWarEnabled;

        foreach (float angle in currentFireAngles)
        {
            var rayGrids = CalculateRayGrids(angle);
            int pierceCount = 0;
            foreach (var rayInfo in rayGrids)
            {
                var g = rayInfo.Grid;
                if (isFogEnabled && fog != null && !fog.IsGridVisible(g)) continue;
                allRayGrids.Add(g);
                if (maxPiercePerLine > 0 && pierceCount >= maxPiercePerLine) break;
                var targets = GetValidTargetsInGrid(g);
                if (targets.Count > 0)
                {
                    targetableGrids.Add(g);
                    pierceCount += targets.Count;
                }
            }
        }

        foreach (var g in allRayGrids)
        {
            g.pathIcon?.Show();
            var gridTargets = GetValidTargetsInGrid(g);
            // 不再限制同一回合重复攻击同一目标
            var hasTargets = gridTargets.Count > 0;

            if (targetableGrids.Contains(g) && hasTargets)
                g.pathIcon.Modulate = new Color(1, 0.2f, 0.2f, 0.7f);
            else if (targetableGrids.Contains(g) && !hasTargets)
                g.pathIcon.Modulate = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            else
                g.pathIcon.Modulate = new Color(0.8f, 0.3f, 0.3f, 0.3f);

            g.IsInWeaponRange = true;
        }

        foreach (var g in allRayGrids)
        {
            g.OnClickGrid = to => ExecuteGlobalAttack();
            gm.gridManager.OverrideUnitInput(g, true);
        }

        foreach (var g in gm.gridManager.grids)
        {
            if (!allRayGrids.Contains(g))
            {
                g.OnClickEmpty = () => {
                    gm.gridManager.ClearWeaponRange();
                    Grids.IsForceActionMode = false;
                    if (!hasActed)
                    {
                        var menu = GetTree()?.GetFirstNodeInGroup("action_menu") as ActionMenu;
                        menu?.ShowWeaponMenu(this);
                    }
                };
            }
        }
    }

    private void ShowAttackRangeAsDisabled(GameManager gm)
    {
        var fog = gm.fogOfWarManager;
        bool isFogEnabled = fog != null && fog.isFogOfWarEnabled;

        foreach (float angle in currentFireAngles)
        {
            var rayGrids = CalculateRayGrids(angle);
            foreach (var rayInfo in rayGrids)
            {
                var g = rayInfo.Grid;
                if (isFogEnabled && fog != null && !fog.IsGridVisible(g)) continue;
                g.pathIcon?.Show();
                g.pathIcon.Modulate = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                g.IsInWeaponRange = true;
            }
        }
        foreach (var g in gm.gridManager.grids)
        {
            if (!CalculateAttackRange().Contains(g))
            {
                g.OnClickEmpty = () => {
                    gm.gridManager.ClearWeaponRange();
                    var menu = GetTree()?.GetFirstNodeInGroup("action_menu") as ActionMenu;
                    menu?.ShowWeaponMenu(this);
                };
            }
        }
    }
}
