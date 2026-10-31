# 格战原创模块图鉴（AI-KIMI-ENG 作品名录）

> 本文档供所有协作者（人类指挥官 + AI-2 参谋）阅读。记录 2026-10-28 至 10-30 由主程 AI 设计并落地的**原创机制**（非 AW 原作内容）。每个模块都可经检查器按单位配置，默认全关 = 零行为变化。

## 模块一：合流模块（Join）

**文件**：`Infantry.cs` / `GameManager.MergeUnits`
**字段**：
- `canMerge`（bool，默认 true）：是否允许参与合流
- `canActAfterMerge`（bool，默认 false）：合流后再行动一次
**规则**：开关看"被合流方"（目标单位）；默认关=AW 基准（合流即报废，else 分支强制待机）；开启则 OnTurnEnd() 全重置补一次行动。
**测试点**：两残血坦克合流，关=灰，开=可再动。

## 模块二：兵器攻击准入（双向白名单）

**文件**：`Infantry.cs`（CanTargetWeapon + excludedWeaponTargets）/ `Weapon.cs`（excludedAttackerTypes）
**字段**：
- `excludedWeaponTargets`（string[]，攻击方）：不能攻击哪些兵器，"Weapon"=全部禁
- `excludedAttackerTypes`（string[]，兵器方）：不接受哪些单位攻击
**三道闸门**：伤害函数归零 + 玩家点击拒绝 + 双向名单判定（支持基类名逐级匹配）
**起源**：战斗机 550 兜底值秒杀兵器事件 → 用户要求"成对准入，为未来能被打的新兵器留门"

## 模块三：隐身系统（76 字段机制层，核心作品）

**文件**：`Infantry.cs`（主体）/ `FogOfWarManager.cs` / `ActionMenu.cs` / `UnitManager.cs` / `GridManager.cs` / `Facility.cs`

### 字段分组
1. **基础**：canStealth / isStealthed / stealthToggleCostsAction（切换是否耗行动）
2. **消耗·八货币**（0=不扣）：燃油/血量/普通弹/自爆弹/照明弹/隐身弹/显形弹/迷彩弹，每日结算
3. **感知**：stealthVisibleWhenAdjacent+Min/MaxRange（X-Y 贴身）/ stealthVisibleOnTile（同格单位+脚下建筑）
4. **现形四触发**：攻击后 / 受伤（可设血量阈值对）/ 照明弹（永久 or 仅生效期间）/ 阈值矩阵
5. **阈值矩阵（32字段）**：八资源 × 双方向（ForceHide/ForceReveal）× 双阈值（High 用≥触发 / Low 用≤触发，中间不触发非区间带）+ stealthForceOneShot（一次性锁存/持续）+ stealthFormLocked（形态锁定：玩家不可切，阈值强制不受限）
6. **行为锁**：攻击/移动/搭载卸载/自爆等自定义/占点
7. **改写层**：stealthMoveTypeOverride（移动方式）/ stealthOverlapMode（0不改/1可重叠跳过合流/2不可重叠拒进格）/ stealthMatrixOverride（隐身攻防表）
8. **弹系统**：stealthAmmoMode 三选一（0免费/1双弹种/2迷彩弹，互斥不能通吃）+ 三弹仓 max/current + Facility 补给补满

### 判定函数（全部 public static，可被 AI/新模块复用）
- `IsVisibleTo(unit, viewerTeam, grids)`：视觉层
- `CanTargetStealth(attacker, target)`：攻击层（含照明弹分支）
- `CanTargetWeapon`：兵器版

### 关键设计
- 视觉闸门在 `ShouldShowUnit`（与迷雾开关解耦：雾关也生效）
- 攻击门在 `Attack()` 唯一咽喉；反击门=防守方打不了隐身攻击者则反击归零；攻击现形先于反击结算
- 照明弹联动：`IsFlareLit()` 实时查询亮格（`IsGridIlluminated`），离开亮格自动恢复——零标记位

## 模块四：单位生产流水线（七件套规范）

新单位 = 场景+脚本+动画资源（3件） + 注册表/生产数据库/伤害表/统计面板/编辑器模板+名单/视野表/翻译/AI优先级（9处集成）。**验收口诀：凡是引用 Fighter 的地方必有新单位，grep 即任务清单。**
素材换肤流水线：指挥官画 zip → 小写命名入库 → .tres 引用零改动。

## 模块五：云打包产线

GitHub Actions 手动触发（workflow_dispatch），Mono+android-arm64 正路（bionic 是死胡同——Mono.linux-arm64 包不存在），Gradle 模板需装到 `android/build/` 子目录 + `.build_version` + `.gdignore`。详见 BUILD.md 事故档案。

## 协作协议

见 BUILD.md《双 AI 协作协议》。本图鉴由 AI-KIMI-ENG 维护，新模块落地后须补录此文。
