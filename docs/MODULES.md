# 格战原创模块图鉴（AI-KIMI-ENG 作品名录）


> 代码已按模块拆分为 partial class，`Infantry.cs` 的各模块现位于 `spricts/Infantry/Infantry.<模块>.cs`，见 [CODE_LAYOUT.md](CODE_LAYOUT.md)。

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

## 模块六：雾战系统（10字段+攻防表覆盖）

**文件**：`Infantry.cs`（ExportGroup"雾战模块"）
**字段**：fogMaxHpBonus / fogMaxFuelBonus / fogFlatDefenseBonus / fogPrimaryAmmoMaxBonus / fogExplodeAmmoMaxBonus / fogFlareAmmoMaxBonus / fogStealthBulletMaxBonus / fogRevealBulletMaxBonus / fogCamoBulletMaxBonus / fogMatrixOverride（Dictionary 攻防表覆盖）
**规则**：全部可为负=减成；**不改实际数值，只改生效上限/判定**——雾里 HP/油料/弹药上限浮动 + 雾中攻防表独立一套
**配套**：迷雾三工单（探索记忆层/多阵营各自视角/森林礁岩隐蔽）+ 林地礁岩隐蔽（AW规则：无贴身敌人时藏于森林/礁岩不可见）

## 模块七：截断遭遇系统（11字段裁决层）

**文件**：`Infantry.cs`（ExportGroup"截断遭遇"）/ `GameManager.cs`
**字段**：开关 / 截断反击参数 / 反射弧过长阈值 / 双角色三模式（1主动出击/2两败俱伤/3反将被动）/ 伏击手权重（平权=被截断方先执行）/ 允许多次截断出击（开=双方各执行，关=只执行权重高者）等
**配套**：攻击单向反击参数独立调节 + 人间截断者遭遇确认菜单（回合中回合，全局冻结）+ 军团P0参与撮合
**测试点**：反将被动模式="对方打我我只能反击"；两败俱伤=双方同发互兑

## 模块八：手画路径移动

**文件**：`GridManager.cs` / `GameManager.cs`
**规则**：手指画航线 → 航点序列 + 路程计费（真实耗油）+ 绕圈合法 + 无效格截断红标 + 相机拖图修复（手势判定/防闪光升级到0帧窗口）
**定位**：电子棋自由度的实体棋回归——官方给格子，玩家画路线

## 模块九：群体狂热模块（16字段光环引擎）

**文件**：`Infantry.cs`（ExportGroup"群体狂热模块"）
**字段**：开关 / x-y检查范围 / 白名单（仅注册表单位种类）/ 特殊态仅同种类 / 敌我友三态（0全部1仅友2仅敌）/ 最大层数（看到7只也只享3层）/ 加减成百分比（负=减成）/ 破上限开关+新上限 / **五路效果对象**（弹药上限/燃油上限/血量上限/真实攻击力/固定防御力）/ 持续N回合（再符合=覆盖不叠加）
**原型**：Wargroove 狗群侧翼加成 → 泛化为任意单位可用的通用增益光环
**注**：机械狗非生物，默认不开启

## 模块十：逃跑模块（野化）

**文件**：`Infantry/Infantry.Flee.cs`（TryFlee）/ `GameManager.cs`（死亡结算5处挂钩）/ `Laser.cs`（三处击杀结算挂钩）
**字段**：canFleeOnDeath（致命伤触发）/ 逃跑目标格 x-y 环
**规则**：致命伤 → x-y环内随机空格逃跑并野化（**落地满血中立、永久存续、原主人可攻击**）；无处可逃才照常死亡。**致命伤被逃跑抵消，代价是脱离编制而非血量**——Wargroove"狗被打败会逃跑"的原教旨继承
**一次性规则（2026-11-01 人类指挥官裁决）**：野化为**终身一次性**——已野化的单位不再二次野化（"他都跑过一次了"），致命伤再临 = 正常死亡。
**实现**：`0c03b98` 已入库——TryFlee 成功后自动置 `canFleeOnDeath = false`；该字段是 Export 字段，随存档保存，读档后同样不会二次野化。

**联动**：死亡截断铁律（模块十三）为野化让路

## 模块十一：拆旗模块

**文件**：`Infantry.cs` / `Building.cs`（双进度）
**字段**：canTearFlag / tearFlagPower（拆旗力；撕旗值=占领值20 → 5力四回合、步兵10两回合）
**规则**：与占领**并行的第二条进度**；占领按钮可见性扩展；两条进度各自独立（进度/冻结/遭遇清零）/ 满20转中立；程序化撕旗动画（帧序列非贴图）
**定位**：AW 只有"占"，格战有"占+拆"两手——攻守双方围绕同一块地打进度战

## 模块十二：设施压制（经济战光环）

**文件**：`Infantry.cs`（ExportGroup"设施压制"）/ `Facility.cs`
**字段**：开关 / x-y环（含自身格）/ **四路干扰**：资金（制止|百分比±）、掩体（制止|百分比）、生产（制止|百分比，作用于生产资金消耗）、补给（只制止）
**定位**：站在敌楼里压制周边经济——不消灭单位，消耗国力

## 模块十三：战斗法则补丁包

- **死亡截断铁律**：同发互兑原子化结算 + 跨发死亡取消后续拳（多段连击首杀=后段作废）；野化为该铁律的第一个例外
- **反击按挨打后血量**：原按挨打前，现修正
- **Oozium 覆盖去硬编码**：吞并判定导出化
- **修理通用化**：修理船行为泛化
- **不写代码造新兵种**（#14~#17）：注册表+模板流水线实战验收

---

## 原型归属勘误（2026-11-01）

- **机械狗**：原型 = Wargroove 的狗（快速廉价骚扰位）；嗅探模块 = 格战原创生态位（Wargroove 无战争迷雾，原作无可嗅之隐）
- **群体狂热模块**：原型 = Wargroove 狗群侧翼加成 → 泛化成模块九
- **逃跑/野化**：原型 = Wargroove"狗败逃不死亡" → 泛化成模块十
- **Wargroove 贡献三基因**：狗本体 / 侧翼加成 / 败逃机制；格战将其全部模块化为可插拔系统

*模块六~十三由 AI-KIMI-ENG 根据 10-31 提交记录补录；机制本体由 AI-2（主程）实现，人类指挥官裁决。*
---

## 模块十四：港口与海军线（2026-11-01）

**文件**：`Port.cs`（建筑，继承 City）/ `Battleship.cs` `Cruiser.cs` `Submarine.cs` `Lander.cs` `Carrier.cs` `Gunboat.cs` / `Infantry.Movement.cs`（GetMoveCostOnGrid）
**数值**：AW3/AWDS 原作值（战列舰 28000/2-6 间接、巡洋 18000/对空105反潜90、潜艇 20000/下潜油5、登陆艇 12000/载2、航母 30000/3-8 对空）；炮艇为 DoR 跨作智能适配原创值（5000/移动7/弹1/载1步兵系）
**规则**：
- 港口与原作一致：可占领、1000/回合收入、维修补给舰船、生产海军；海军单位可靠泊（`GetMoveCostOnGrid` 覆盖：海军/气垫进港口格=1，移动范围/路径/AI 寻路/逃离全走此口）
- 潜艇下潜/上浮 = 直接复用隐身模块，按钮文字可检查器改（`stealthHideButtonText`/`stealthShowButtonText`，默认隐身/显形，潜艇填下潜/上浮）；下潜贴身可见（相邻1格），只有巡洋舰和潜艇能打水下潜艇（stealthAttackableBy）
- 登陆艇装卸限制（原作）：只能浅滩（BEACH）和港口
- 贴图：原版 GBA 地图贴图（AWBW 提取，16×16，Base2D ×2）：`asscets/units/{Battleship,Cruiser,Submarine,Lander,Carrier}/`，红=Player1 / blue=Player2；炮艇为 DoR 原图按 GBA 橙星/蓝月调色板重上色（24×24，Base2D ×4/3）；港口 `art/Port/Port0~2.png`（中立/P1/P2，32×44，offset -4 底部与机场对齐）
**编辑器**：设施模板列表 + 紧凑格式缩写 Prt + 生成器搜索框（按显示名/描述/场景路径过滤，单位/兵器/设施通用）

## 模块十五：统一弹药系统（AmmoKind）

**文件**：`Infantry/Infantry.Ammo.cs`
**设计**：全部弹药种类（主武器/自爆/照明/隐身弹/显形弹/迷彩弹/生产弹/建造弹）走同一套接口：`GetAmmo/GetMaxAmmo/HasAmmo/TryConsumeAmmo/AddAmmo/RefillAmmo/RefillAllAmmo`。旧消耗点（武器开火、自爆、照明、隐身切换、AI 照明）全部改走 `TryConsumeAmmo` 单一咽喉；设施/补给单位补给走 `RefillAmmo`。
**新增弹种**：生产弹/建造弹（见模块十六/十七），设施补给表新增 `productionAmmoSupply`/`constructionAmmoSupply` + 独立兵种白名单。

## 模块十六：内部服务（舱内单位：内部补给 + 内部回血）

**文件**：`Infantry/Infantry.InternalService.cs`
**字段**（ExportGroup「内部服务（舱内单位）」）：
- `internalSupplyEnabled` + `internalSupplyWhitelist`（独立白名单，空=全部舱内单位）
- `internalHealEnabled` + `internalHealAmount`（每回合回 x HP，**可为负=扣血，扣到 0 以下=舱内单位损毁**）+ `internalHealWhitelist`（与补给/搭载白名单各自独立）
**规则**：每个大回合开始结算（挂在回合燃料结算同处）；补给=燃料+全弹药补满（走统一弹药系统）。航母（给舰载机）/巡洋舰（AW3 可载2直升机）即内置用户。

## 模块十七：生产模块（单位级，11 字段规范）

**文件**：`Infantry/Infantry.Production.cs` / `ActionMenu/ActionMenu.Production.cs`（按钮与目标选择）
**字段**（ExportGroup「生产模块」）：①canProduceUnits ②位置模式（关闭/内部/外部/可选内外）③外部范围 x-y ④自动随机放置 vs 手选目标格 ⑤生产白名单 ⑥生产弹药（决定按钮显示，开工耗1发）⑦总进度 N ⑧每回合进度 Z（如 20/10=两回合）⑨内部生产时可否移动（**=移动会不会打断进度**，关=移动即中断；外部一律移动即中断）⑩自动化（开=点一次后每回合自动推进，只显示中断按钮，生产不消耗行动——照常攻击/操作/待机；关=生产占用回合，单位待机）⑪冷却回合数
**内部生产依赖**：点「内部生产」自动开启搭载模块（仅需搭载，白名单可空——产出单位直接进舱可待着，出舱后不在搭载白名单就装不回去）。
**完工**：内部→直接进舱；外部→放到目标格（手选时高亮有效格）。

## 模块十八：建造模块（与生产对称）

**文件**：`Infantry/Infantry.Construction.cs` / `FacilityDatabase.cs`
**字段**：与生产模块一一对应（canBuildFacilities/位置模式/范围/自动放置/建造白名单（建筑类型名）/建造弹药/进度 N/Z/内部建造时可否移动/自动化/冷却）。
**规则**：内部建造与搭载模块**无绑定**——建好的建筑存舱内数据，用「📦 部署建筑」按钮放到相邻空格；外部建造直接放目标格。可建建筑登记在 `FacilityDatabase`（City/Base/AirPort/Port）。

## 模块十九：检查器优先审计（2026-11-01 顺手修）

- `City`/`AirPort`/`Base`/`Port`：补给兵种表、生产名单改为「仅当未配置时补默认」（默认列表抽成 `DefaultSupplyUnitTypes()` 虚方法，子类重写），改检查器不再被 _Ready 覆盖
- `RepairBoat`：删掉 _Ready 里整套硬编码强制覆盖（移动/燃料/修理字段），改为「0=未配置才补默认」
- 舰船六类 + 港口场景值全部写进 .tscn 检查器，代码零硬编码
