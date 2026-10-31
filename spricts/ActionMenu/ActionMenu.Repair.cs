// ActionMenu.cs - 完整修复版，移除所有unloadButton直接引用
using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;

// ActionMenu · Repair（由 ActionMenu.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class ActionMenu
{

    // ========== ✅ 修复按钮处理 ==========
    private void OnRepairPressed()
    {
        if (isProcessingRepair) return;
        if (currentUnit == null || !currentUnit.CanPerformRepair) return;

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm == null) return;

        if (!isRepairPreview)
        {
            // 第一阶段：显示修复范围预览（粉色）
            isRepairPreview = true;
            gm.gridManager?.ShowRepairRange(currentUnit);

            // 设置修复范围内格子的点击回调
            foreach (var grid in gm.gridManager.repairRange)
            {
                grid.OnClickGrid = to => OnRepairTargetSelected(to);
                gm.gridManager.OverrideUnitInput(grid, true);   // ✅ 点到有单位（含敌军）的格子也落到格子回调
            }
            // 范围外点击取消
            foreach (var grid in gm.gridManager.grids)
            {
                if (!gm.gridManager.repairRange.Contains(grid))
                {
                    grid.OnClickEmpty = () => CancelRepairPreview();
                    grid.OnClickGrid = to => CancelRepairPreview();
                }
            }

            // 隐藏其他按钮
            moveButton.Visible = false;
            attackButton.Visible = false;
            captureButton.Visible = false;
            infoButton.Visible = false;
            if (loadButton != null) loadButton.Visible = false;
            if (unloadContainer != null) unloadContainer.Visible = false;
            if (supplyButton != null) supplyButton.Visible = false;
            if (explodeButton != null) explodeButton.Visible = false;
            if (illuminateButton != null) illuminateButton.Visible = false;
            TranslateGroup.SetText(repairButton, "取消修复");
            waitButton.Visible = true;
        }
        else
        {
            // 点击"取消修复"
            CancelRepairPreview();
        }
    }

    private void OnRepairTargetSelected(Grids target)
    {
        if (target == null || currentUnit == null) return;
        // ✅ 通用：任何开启修理模块的单位（此前强转 RepairBoat，其他单位点了无反应）
        if (!currentUnit.CanPerformRepair) return;

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm == null) return;

        // ✅ 按 repairTargetMode 选目标：0=所有 1=友方 2=敌方（扣血） 3=自己；多个时优先最残血
        var targetUnit = target.infantries
            .Where(u => u != null && IsInstanceValid(u) && currentUnit.IsRepairTargetAllowed(u))
            .Where(u => Infantry.IsVisibleTo(u, currentUnit.team, gm.gridManager.grids))   // 看不见的隐身敌人不能被点中
            .OrderBy(u => u.health)
            .FirstOrDefault();
        if (targetUnit == null) return;

        // 执行修复
        bool repaired = currentUnit.PerformRepair(targetUnit);
        if (!repaired) return;

        // 清理状态
        isRepairPreview = false;
        RestoreRepairRangeInput(gm);
        gm.gridManager?.HideAttackRange(); // 修复范围使用 attackRangeIcon

        // 标记单位已行动
        currentUnit.hasActed = true;
        currentUnit.isMoved = true;
        currentUnit.isAttacked = true;
        currentUnit.state = UnitState.Acted;
        currentUnit.originalGrid = null;
        currentUnit.SetWaitVisual(true);

        gm.ClearSelectedInfantry();
        Hide();
    }

    private void RestoreRepairRangeInput(GameManager gm)
    {
        if (gm?.gridManager == null) return;
        foreach (var grid in gm.gridManager.repairRange)
            gm.gridManager.OverrideUnitInput(grid, false);
    }

    public void CancelRepairPreview()
    {
        if (isProcessingRepair) return;
        if (isRepairPreview)
        {
            isRepairPreview = false;
            var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
            RestoreRepairRangeInput(gm);
            gm?.gridManager?.HideAttackRange(); // 修复范围使用 attackRangeIcon

            // 恢复按钮状态
            if (currentUnit != null)
            {
                ShowMenu(currentUnit, currentUnit.isMoved);
            }
        }
    }
}
