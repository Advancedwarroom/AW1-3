// ActionMenu.cs - 完整修复版，移除所有unloadButton直接引用
using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;

// ActionMenu · Attack（由 ActionMenu.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class ActionMenu
{

    // ✅ 攻击按钮统一判定：可攻击类型 + 未攻击 + 移动后允许 + 有弹药 + 射程内有可伤害目标
    private bool CanShowAttackButton(Infantry unit)
    {
        if (unit == null) return false;
        if (unit.attackType == AttackType.NoAttack) return false;
        if (unit.isAttacked) return false;
        if (!unit.CanAttackAfterMove()) return false;
        if (!unit.CanUsePrimaryWeapon() && !unit.CanUseSecondaryWeapon()) return false;
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        return gm != null && HasAttackableEnemyInRange(unit, gm);
    }

    // 射程内是否存在“实际能造成伤害”的敌人（infantry 用伤害矩阵判定，weapon 维持队伍判定）
    private bool HasAttackableEnemyInRange(Infantry unit, GameManager gm)
    {
        if (gm?.gridManager == null || unit.grid == null) return false;

        bool hasEnemyInRange;
        if (unit.useMinMaxAttackRange)
        {
            hasEnemyInRange = CheckMinMaxRangeEnemy(unit, gm);
        }
        else if (unit.attackRange > 1)
        {
            // 当 attackRange > 1 时，使用 FindRange 检查更大范围
            var extendedRange = gm.gridManager.FindRange(unit.grid, unit.attackRange, false);
            hasEnemyInRange = extendedRange.Any(g =>
                g.HasAttackableEnemyInfantry(unit) || g.HasEnemyWeapon(unit.team)
            );
            if (!hasEnemyInRange && unit.overlapType == UnitOverlapType.Overlapping)
                if (unit.grid.HasAttackableEnemyInfantry(unit) || unit.grid.HasEnemyWeapon(unit.team))
                    hasEnemyInRange = true;
        }
        else
        {
            // 默认4邻域
            var range = gm.gridManager.GetAttackNeighbours(unit.grid);
            hasEnemyInRange = range.Any(g =>
                g.HasAttackableEnemyInfantry(unit) || g.HasEnemyWeapon(unit.team)
            );
            if (!hasEnemyInRange && unit.overlapType == UnitOverlapType.Overlapping)
                if (unit.grid.HasAttackableEnemyInfantry(unit) || unit.grid.HasEnemyWeapon(unit.team))
                    hasEnemyInRange = true;
        }
        return hasEnemyInRange;
    }

    private bool CheckMinMaxRangeEnemy(Infantry unit, GameManager gm)
    {
        if (unit.grid == null) return false;

        int minRange = unit.minAttackRange;
        int maxRange = unit.maxAttackRange;

        foreach (var grid in gm.gridManager.grids)
        {
            if (grid == null || grid == unit.grid) continue;

            int distance = Mathf.Abs(grid.GridIndex.X - unit.grid.GridIndex.X) 
                         + Mathf.Abs(grid.GridIndex.Y - unit.grid.GridIndex.Y);

            if (distance >= minRange && distance <= maxRange)
            {
                if (grid.HasAttackableEnemyInfantry(unit) || grid.HasEnemyWeapon(unit.team))
                    return true;
            }
        }
        return false;
    }
}
