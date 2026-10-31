using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

// Infantry · Supply（由 Infantry.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class Infantry
{

    [ExportGroup("补给")]
    [Export] public string[] supplyBlacklist = System.Array.Empty<string>();  // 雾战独立攻防表（固定数值，禁小数）
    [Export] public bool useDefaultConfig = true;           // ✅ 是否使用代码默认值（false=完全由Inspector控制，不执行ApplyUnitSpecificDefaults）
    [Export] public bool canDevour = false;
    [Export] public bool canDevourAllies = true;   // 吞噬友军（AW原作史莱姆不分敌我；关掉则只吞噬敌军）                // 是否能吞噬敌人（Oozium特性）
    [Export] public bool canCapture = true;                // 是否能占领城市
    [Export] public UnitCategory unitCategory = UnitCategory.Other;  // 手动指定分类

    [Export] public int cost = 1000;  // 单位造价（可在Inspector中修改，非硬编码）

// ========== ✅ 究极自由：补给范围计算（从 APC 迁移） ==========
public virtual List<Grids> CalculateSupplyRange()
{
    var result = new List<Grids>();
    var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
    if (gm?.gridManager == null || grid == null) return result;

    var allRange = gm.gridManager.FindRange(grid, maxSupplyRange, false);

    if (minSupplyRange > 1)
    {
        var blindZone = gm.gridManager.FindRange(grid, minSupplyRange - 1, false);
        allRange = allRange.Where(g => !blindZone.Contains(g)).ToList();
    }

    allRange = allRange.Where(g => g != grid).ToList();

    return allRange;
}

// ========== ✅ 究极自由：获取补给范围内的友方单位（从 APC 迁移） ==========
public virtual List<Infantry> GetSupplyRangeUnits()
{
    var result = new List<Infantry>();
    var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
    if (gm?.gridManager == null) return result;
    
    var supplyRange = CalculateSupplyRange();
    
    foreach (var g in supplyRange)
    {
        if (g == null) continue;
        
        foreach (var unit in g.infantries)
        {
            if (unit != null && IsInstanceValid(unit) && unit.team == this.team && unit != this)
            {
                result.Add(unit);
            }
        }
    }
    
    return result;
}

// ========== ✅ 究极自由：补给黑炮弹药（从 APC 迁移） ==========
public virtual bool ResupplyBlackCannon(Weapon weapon)
{
    return ResupplyWeapon(weapon);
}

// ========== ✅ 究极自由：获取范围内需要弹药补给的黑炮（从 APC 迁移） ==========
public virtual List<Weapon> GetBlackCannonsNeedingAmmo()
{
    return GetWeaponsNeedingAmmo();
}

// ========== ✅ 究极自由：执行补给（从 APC 迁移） ==========
public virtual void PerformSupply()
{
    if (!canSupplyUnits) return;  // 没有补给能力的单位不能执行补给
{
    bool anySupplied = false;

    // 1. 补给单位
    var targetUnits = GetSupplyRangeUnits();
    foreach (var unit in targetUnits)
    {
        if (supplyBlacklist != null && supplyBlacklist.Length > 0
            && System.Array.IndexOf(supplyBlacklist, unit.UnitTypeId) >= 0) continue;
        bool supplied = false;
        string supplyText = "";
        Color supplyColor = new Color(0.2f, 0.9f, 0.3f);

        // 补给弹药
        if (unit.hasPrimaryWeapon && unit.primaryHasLimitedAmmo && 
            unit.currentPrimaryAmmo < unit.maxPrimaryAmmo)
        {
            unit.currentPrimaryAmmo = unit.maxPrimaryAmmo;
            supplied = true;
            supplyText = "Ammo+";
        }

        // ✅ 补给照明弹（通用：任何开启照明模块的单位）
        if (unit.canIlluminate && unit.currentFlareAmmo < unit.maxFlareAmmo)
        {
            unit.currentFlareAmmo = unit.maxFlareAmmo;
            if (!supplied)
            {
                supplyText = "Flare+";
                supplyColor = new Color(0.9f, 0.9f, 0.2f);
            }
            else
            {
                supplyText += " Flare+";
            }
            supplied = true;
        }
        if (unit is Flare flareUnit && flareUnit.canIlluminate && 
            flareUnit.currentFlareAmmo < flareUnit.maxFlareAmmo)
        {
            flareUnit.currentFlareAmmo = flareUnit.maxFlareAmmo;
            if (!supplied)
            {
                supplyText = "Flare+";
                supplyColor = new Color(0.9f, 0.9f, 0.2f);
            }
            else
            {
                supplyText += " Flare+";
            }
            supplied = true;
        }

        // 补给燃料
        if (unit.consumeFuel && unit.fuel < unit.maxFuel)
        {
            unit.fuel = unit.maxFuel;
            if (!supplied)
            {
                supplyText = "Fuel+";
                supplyColor = new Color(0.9f, 0.7f, 0.2f);
            }
            else
            {
                supplyText = "Ammo+ Fuel+";
            }
            supplied = true;
        }

        if (supplied)
        {
            anySupplied = true;
            unit.UpdateHpLabel();
            ShowSupplyEffect(unit, supplyText, supplyColor);
        }
    }

    var weaponsNeedingAmmo = GetWeaponsNeedingAmmo();
    foreach (var weapon in weaponsNeedingAmmo)
    {
        if (ResupplyWeapon(weapon))
        {
            anySupplied = true;
        }
    }

    if (anySupplied)
    {
        ShowTransportSupplyEffect();
    }
}
}
private void ShowSupplyEffect(Infantry unit, string text, Color color)
{
    var effect = new SupplyEffect();
    if (grid != null)
    {
        var relativePos = unit.GlobalPosition - this.GlobalPosition;
        effect.Setup(relativePos, text, color);
        AddChild(effect);
    }
}

private void ShowTransportSupplyEffect()
{
    var effect = new GridSupplyEffect();
    AddChild(effect);
}

// ========== ✅ 究极自由：补给按钮回调（从 APC 迁移） ==========
public virtual void OnSupplySelected()
{
    PerformSupply();
    
    isMoved = true;
    isAttacked = true;
    hasActed = true;
    state = UnitState.Acted;
    originalGrid = null;
    
    SetWaitVisual(true);
    
    var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
    gm?.ClearSelectedInfantry();
}

// ========== ✅ 究极自由：回合开始自动补给（从 APC 迁移） ==========
public virtual void OnTurnStartAutoSupply()
{
    PerformSupply();
}
}
