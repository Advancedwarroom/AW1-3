# 不写代码造新兵种

## 步骤

1. 复制一个最接近的现有单位场景（如 `Prefabs/mech.tscn`），放到 `res://Prefabs/CustomUnits/`，改个文件名（如 `flamer.tscn`）。
   - 复制后删掉文件头 `[gd_scene ... uid="..."]` 里的 uid，避免和原场景冲突（或在编辑器里"另存为"）。
2. 在检查器「兵种身份（自定义兵种）」分组填写：
   - `unitTypeId`：唯一兵种ID（如 `Flamer`），必填。
   - `unitDisplayName`：显示名（如 `火焰兵`）。
   - `templateType`：参照兵种（如 `Mech`），攻防表、视野、默认配置查不到本ID时按它兜底。
3. 在检查器里调整其余能力字段：移动、燃料、弹药、造价 `cost`、修理模块、隐身模块、运输、AI档案等。
4. 想让某工厂能造：在该设施的 `producibleUnitNames` 里加上 `Flamer`。

启动时自动注册进生产数据库（日志：`[自定义兵种] 注册 Flamer（火焰兵）`），地图编辑器单位面板出现在「自定义」分类。

## 攻防表

查表顺序（先命中先用）：

1. `本ID_目标ID`（如 `Flamer_Infantry`），可写在单位自己的 `attackMatrix` / `secondaryAttackMatrix` / `stealthMatrixOverride` 里
2. `参照_目标ID`
3. `本ID_目标参照`
4. `参照_目标参照`（如 `Mech_Infantry`，全局默认表）

所以别的单位打 Flamer 时，没有专门条目就按"打 Mech"算，不会出现打不动的情况。
需要专门数值时，在攻击方的实例表里写 `Infantry_Flamer`。

## 名单类字段统一用兵种ID

`stealthAttackableBy`、`excludedAttackerTypes`、`canTransportUnitTypes`、黑水晶治疗/补给类型过滤、设施补给类型、AI `aiTargetWeights` 都按 `unitTypeId` 匹配（留空时=脚本类名，原有兵种行为不变）。

## 限制

- 脚本行为（如 Oozium 吞噬、BlackBoat 特殊逻辑）跟随所选场景的脚本；要全新的"行为"仍需写代码，但所有已模块化的能力（修理、隐身、运输、补给、自爆、照明等）都能直接在检查器里组合。
- `UnitCategory` 统计分类跟随脚本类。
