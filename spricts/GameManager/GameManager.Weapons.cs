// GameManager.cs - 添加完整的单位统计系统
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// GameManager · Weapons（由 GameManager.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class GameManager
{


public void OnSelectWeapon(Weapon weapon)
{
    if (weapon == null || weapon.hasActed || weapon.isDestroyed) return; // ✅ 已摧毁的兵器不可选择
    if (!IsTurnPhaseValid(weapon.team)) return;

    // ✅ 关键：清除单位选择
    selectedInfantry = null;
    gridManager.HideAttackRange(); // 清除单位攻击范围
    gridManager.CloseRange(); // 清除移动范围

    selectedWeapon = weapon;
    HideProductionMenu();

    var menu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
    menu?.ShowWeaponMenu(weapon); // 这会清除 currentUnit
}

    // ✅ 修改：移除兵器现在通过WeaponManager处理
public void RemoveWeapon(Weapon weapon)
{
    if (weapon == null) return;

    // ✅ 多格兵器：必须彻底清理所有占据格子的引用，不能只做单格清理
    if (weapon.isMultiTile)
    {
        if (weaponManager != null && weaponManager.AllWeapons.Contains(weapon))
        {
            weaponManager.RemoveWeapon(weapon);
            UpdateUnitLists();
            return;
        }
        // 备用：手动清理多格引用
        foreach (var og in weapon.occupiedGrids.ToList())
        {
            if (og != null && IsInstanceValid(og))
            {
                og.weapons.Remove(weapon);
                if (og.weapon == weapon)
                    og.weapon = og.weapons.Count > 0 ? og.weapons[0] : null;
            }
        }
        weapon.occupiedGrids.Clear();
        weapons.Remove(weapon);
        if (selectedWeapon == weapon) selectedWeapon = null;
        if (weapon != null && IsInstanceValid(weapon))
            weapon.QueueFree();
        UpdateUnitLists();
        return;
    }

    // ✅ 修复：先检查 weaponManager 是否为 null，避免递归时重复操作
    if (weaponManager != null && weaponManager.AllWeapons.Contains(weapon))
    {
        // 使用临时变量避免递归
        var wm = weaponManager;
        // 直接从 GameManager 的列表移除
        weapons.Remove(weapon);
        if (selectedWeapon == weapon)
            selectedWeapon = null;

        // 通知 WeaponManager 清理（但不触发回调）
        wm.AllWeapons.Remove(weapon);
        if (weapon.grid != null)
        {
            weapon.grid.weapons.Remove(weapon);
            if (weapon.grid.weapon == weapon)
                weapon.grid.weapon = null;
        }
        weapon.QueueFree();
    }
    else
    {
        // 备用：直接处理
        weapons.Remove(weapon);
        if (selectedWeapon == weapon)
            selectedWeapon = null;
        if (weapon != null && IsInstanceValid(weapon))
            weapon.QueueFree();
    }

    UpdateUnitLists();
}
}
