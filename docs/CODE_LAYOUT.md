# 代码目录约定（2026-11-01 模块化拆分）

大类按模块拆成 **partial class**，主文件路径不变（场景 `.tscn` 引用、`.uid` 均不受影响）：

| 类 | 主文件 | 模块目录 |
|---|---|---|
| Infantry | `spricts/Infantry.cs`（生命周期 / 通用字段） | `spricts/Infantry/`：Stealth、FogWar、Scent、Flee、TearFlag、FacilitySuppression、PackFrenzy、Truncation、Vision、DamageMatrix、Combat、Transport、Supply、Repair、SelfDestruct、Flare、Movement、Capture、Visual、Input、Identity |
| TerrainEditor | `spricts/TerrainEditor.cs` | `spricts/TerrainEditor/`：SpawnMenu、SpawnDestroy、GridClick、TerrainMenu、PropertyEditor、SpecialFieldEditors、GridPropertyEditor、TerrainMatrixEditor、GlobalConfig、MapExport、MapImport、MapCompactFormat、MapImportAWBW、MapImportAWME、BatchTerrain、MultiTileWeapon、SavedData、Texture |
| GameManager | `spricts/GameManager.cs` | `spricts/GameManager/`：Funds、Turn、Selection、UnitRegistry、StatsPanel、Victory、Production、Explosion、Merge、Fog、Weapons、Zoom、Reset、AndroidPermission |
| CustomConfigUI / GridManager / ActionMenu / ExtraSave / AI_Manager / BlackCannon / MapEditorHelper / TranslateGroup / Laser | `spricts/<类名>.cs` | `spricts/<类名>/<类名>.<模块>.cs` |

原文件里附带的枚举/小类型拆到独立文件：`UnitEnums.cs`、`TerrainEditorTypes.cs`、`UnitCategory.cs`、`ExtraSaveTypes.cs`、`MapEditorTypes.cs`、`HealEffect.cs`、`GameLanguage.cs`、`LaserLineEffect.cs`、`LaserHitEffect.cs`、`LaserTargetMode.cs`。

## 新增 Export 字段的铁律

1. 字段放进对应模块文件，**该文件第一个 `[Export]` 前必须写 `[ExportGroup("…")]`**。partial 各文件的编译顺序不固定，没有分组开头的字段可能被"吸"进别的文件末尾的分组。
2. 想放在顶层（不分组）的字段，前面写 `[ExportGroup("")]`。
3. 只有 `Infantry.cs` 这类与类同名的主文件会被 Godot 当成脚本挂载；`Infantry.Stealth.cs` 等模块文件不能单独挂到节点上。

## 拆分验收记录

- `dotnet build`：0 错误 0 警告（含 `-warnaserror:CS0108,CS0618`）
- Godot 4.7.1 无头逐场景/逐脚本导出比对：37 个场景、860 个对象、38,164 条 Export 记录，**名称/类型/值 100% 一致**；源文件倒序编译复测结果相同
- 唯一的有意变化：隐身 76 个字段原本误挂在检查器「补给」分组下，现归回「隐身模块」分组（场景里存的值不变）
