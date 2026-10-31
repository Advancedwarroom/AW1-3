// ActionMenu.cs - 完整修复版，移除所有unloadButton直接引用
using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;

// ActionMenu · Supply（由 ActionMenu.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class ActionMenu
{


private bool HasSupplyableUnits(Infantry transport)
{
    // 检查单位是否有补给能力
    if (!transport.canSupplyUnits) return false;
    
    var supplyableUnits = transport.GetSupplyRangeUnits();
{
    foreach (var unit in supplyableUnits)
    {
        bool needsAmmo = unit.hasPrimaryWeapon && unit.primaryHasLimitedAmmo 
                        && unit.currentPrimaryAmmo < unit.maxPrimaryAmmo;
        bool needsFuel = unit.consumeFuel && unit.fuel < unit.maxFuel;
        bool needsFlare = unit.canIlluminate && unit.currentFlareAmmo < unit.maxFlareAmmo;
        if (needsAmmo || needsFuel || needsFlare) return true;
    }

    var cannonsNeedingAmmo = transport.GetBlackCannonsNeedingAmmo();
    if (cannonsNeedingAmmo.Count > 0) return true;

    return false;
}
}

private void OnSupplyPressed()
{
    GetViewport().SetInputAsHandled();
    var supplier = currentTransport ?? currentUnit;
    if (supplier != null)
    {
        supplier.OnSupplySelected();
        Hide();
    }
}
}
