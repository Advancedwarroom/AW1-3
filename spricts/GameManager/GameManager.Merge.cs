// GameManager.cs - 添加完整的单位统计系统
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// GameManager · Merge（由 GameManager.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class GameManager
{

    // ========== ✅ 合流功能：两个同类型友方单位合并 ==========
    public void MergeUnits(Infantry source, Infantry target)
    {
        if (source == null || target == null) return;
        if (!source.canMerge || !target.canMerge) return;   // ✅ 合流模块：防御性检查
        if (source.GetType() != target.GetType()) return;
        if (source.team != target.team) return;
        if (source == target) return;

        // 合并血量
        int combinedHealth = source.health + target.health;
        int maxHealthCap = 100; // 最大100（10滴满血）
        int excessHealth = Mathf.Max(0, combinedHealth - maxHealthCap);
        target.health = Mathf.Min(maxHealthCap, combinedHealth);

        // 多余血量转化为资金（按单位造价比例：每10血 = cost/maxHealth 的资金）
        if (excessHealth > 0 && target.cost > 0)
        {
            int fundsGained = Mathf.RoundToInt((float)excessHealth / target.maxHealth * target.cost);
            int currentFunds = GetTeamFunds(target.team);
            SetTeamFunds(target.team, currentFunds + fundsGained);
        }

        // 弹药取最大值
        if (source.hasPrimaryWeapon && target.hasPrimaryWeapon)
        {
            target.currentPrimaryAmmo = Mathf.Max(source.currentPrimaryAmmo, target.currentPrimaryAmmo);
        }

        // 燃料取最大值
        if (source.consumeFuel && target.consumeFuel)
        {
            target.fuel = Mathf.Max(source.fuel, target.fuel);
        }

        // 更新目标单位显示
        target.UpdateHpLabel();

        // 移除源单位
        RemoveUnit(source);

        // 更新统计
        UpdateUnitLists();

        // ✅ 合流后再行动开关
        if (target.canActAfterMerge)
            target.OnTurnEnd();   // 开启：合流后获得一次全新行动
        else
        {
            // ✅ AW基准（修复）：合流后本回合不能再行动——此前目标若未行动，合流完还能操作
            target.isMoved = true;
            target.isAttacked = true;
            target.hasActed = true;
            target.state = UnitState.Acted;
            target.SetWaitVisual(true);
        }
    }

    // ========== ✅ 检查是否可以合流 ==========
    public bool CanMerge(Infantry source, Infantry target)
    {
        if (source == null || target == null) return false;
        if (!source.canMerge || !target.canMerge) return false;   // ✅ 合流模块：任一方关闭即不可合流
        if (source.GetType() != target.GetType()) return false;
        if (source.team != target.team) return false;
        if (source == target) return false;
        // 目标必须是残血的
        if (target.health >= target.maxHealth) return false;
        // 不能是运输单位（避免和搭载冲突）——但 RepairBoat 允许合流
        if (target.canTransportUnits && target.maxTransportCapacity > 0 && target is not RepairBoat) return false;
        return true;
    }
}
