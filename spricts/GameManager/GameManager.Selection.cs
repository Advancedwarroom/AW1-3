// GameManager.cs - 添加完整的单位统计系统
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// GameManager · Selection（由 GameManager.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class GameManager
{

    // 清空选中
    public void ClearSelectedInfantry()
    {
        selectedInfantry = null;
    }

    // 选择单位
    public void OnSelectPiece(Infantry infantry)
    {
        // ========== ✅ 编辑模式迷雾控制 ==========
        if (terrainEditor != null && terrainEditor.IsEditMode)
        {
            fogOfWarManager?.SetFogOfWarEnabled(false);
        }
        else
        {
            fogOfWarManager?.SetFogOfWarEnabled(fogOfWarManager?.isFogOfWarEnabled ?? true);
        }

        // ✅ 编辑模式下禁止操作单位
        if (terrainEditor != null && terrainEditor.ShouldBlockUnitOperations())
        {
            return;
        }

        if (infantry == null)
        {
            gridManager.CloseRange();
            gridManager.HideAttackRange();
            gridManager.ClearWeaponRange();
            (GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu)?.Hide();
            HideProductionMenu();
            selectedInfantry = null;
            selectedWeapon = null; 
            return;
        }
            if (infantry.isAttacked || infantry.state == UnitState.Acted)
        {
        HideProductionMenu();
        return;
        }

        bool isValid = IsTurnPhaseValid(infantry.team);

        if (!infantry.isMoved && isValid)
        {        
            selectedWeapon = null; // 清除兵器选择
            gridManager.ClearWeaponRange(); 

            selectedInfantry = infantry;
            HideProductionMenu();
            var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
            actionMenu?.ShowMenu(infantry);
        }
        else
        {
        }
    }

    // 执行攻击
    public void OnAttack(Infantry attacker, Infantry target)
    {
        attacker.Attack(target);

        gridManager.HideAttackRange();
        attacker.actionMenu?.Hide();
        ClearSelectedInfantry();

        // ✅ 攻击后更新统计（可能有单位死亡）
        CallDeferred(nameof(UpdateUnitLists));
    }

public void RollbackMove()
{
    HideProductionMenu();

    if (selectedInfantry == null)
    {
        return;
    }

    if (selectedInfantry.originalGrid == null)
    {
        return;
    }    
    if (selectedInfantry.isAttacked || selectedInfantry.state == UnitState.Acted)
    {
        return;
    }



    var unit = selectedInfantry;
    var fromGrid = unit.grid;
    var toGrid = unit.originalGrid;

    if (fromGrid == null || toGrid == null)
    {
        selectedInfantry = null;
        return;
    }

    // 计算实际消耗（Oozium 也通过 GetMoveCost 返回 1，无需特殊分支）
    int moveCost = 0;
    var path = BuildPath(fromGrid, toGrid, unit, ignoreMovePoints: true);
    bool isFirst = true;
    foreach (var g in path)
    {
        if (isFirst) 
        {
            isFirst = false;
            continue;
        }
        moveCost += unit.GetMoveCost(g.gridType);
    }

    // 恢复移动力
    unit.movePoints += moveCost;

    // 恢复燃料
    if (unit.consumeFuel)
    {
        int fuelCost = moveCost;
        unit.fuel += fuelCost;
    }

    // 从当前格子移除
    if (fromGrid != null)
    {
        fromGrid.infantries.Remove(unit);
        if (fromGrid.infantry == unit)
        {
            fromGrid.infantry = fromGrid.infantries.Count > 0 ? fromGrid.infantries[0] : null;
        }
    }

    // 回退到原位置
    unit.Position = toGrid.Position;
    unit.grid = toGrid;

    // 添加到原格子的列表
    if (!toGrid.infantries.Contains(unit))
    {
        toGrid.infantries.Add(unit);
    }

    // 如果原格子没有主单位，设自己为主单位
    if (toGrid.infantry == null)
    {
        toGrid.infantry = unit;
    }

    // 清空记录，重置状态
    unit.originalGrid = null;
    unit.state = UnitState.Idle;
    unit.isMoved = false;
    unit.isAttacked = false;

    // ✅ 关键修复：恢复视觉为原色（白色）
    unit.SetWaitVisual(false);
    unit.StopBreath();  // 先停止之前的呼吸动画

    // ✅ 通用：恢复 animSprite 或 sprite 的颜色
    if (unit.animSprite != null && IsInstanceValid(unit.animSprite))
    {
        unit.animSprite.Modulate = unit.normal;
    }
    else if (unit.sprite != null)
    {
        unit.sprite.Modulate = unit.normal;
    }

    // 重新启动呼吸动画
    unit.StartBreath();

    // ✅ 回退完成后立即刷新战争迷雾
    fogOfWarManager?.OnUnitMoved();

    // 关闭UI
    gridManager?.CloseRange();
    gridManager?.ClearAllEmptyCallbacks();
    var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
    actionMenu?.Hide();

    selectedInfantry = null;
}
private List<Grids> BuildPath(Grids from, Grids to, Infantry infantry = null, bool ignoreMovePoints = false)
{
    if (gridManager == null) return new List<Grids>();
    return gridManager.BuildPath(from, to, infantry, ignoreMovePoints);
}


    // ✅ 回合中回合：人类伏击手的临场决策（确认=攻击 / 取消=放过）
    public void RequestEncounterDecision(Infantry mover, Infantry blocker)
    {
        encounterPause = true;
        var dlg = new ConfirmationDialog();
        dlg.Title = "遭遇战！";
        dlg.DialogText = $"{blocker.GetType().Name} 截断了 {mover.GetType().Name} 的移动\n是否发起攻击？";
        dlg.OkButtonText = "攻击";
        dlg.CancelButtonText = "放过";
        AddChild(dlg);
        dlg.PopupCentered();
        dlg.Confirmed += () =>
        {
            encounterPause = false;
            dlg.QueueFree();
            Infantry.ExecuteTruncationStrikes(mover, blocker, mover.truncCounterEnabled, true);
        };
        dlg.Canceled += () =>
        {
            encounterPause = false;
            dlg.QueueFree();
            Infantry.ExecuteTruncationStrikes(mover, blocker, mover.truncCounterEnabled, false);
        };
    }
}
