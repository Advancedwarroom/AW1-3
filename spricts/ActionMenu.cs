// ActionMenu.cs - 完整修复版，移除所有unloadButton直接引用
using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;

public partial class ActionMenu : Control
{
    [ExportGroup("")]
    [Export] public Button moveButton;
    [Export] public Button attackButton;
    [Export] public Button waitButton;
    [Export] public Button infoButton; 
    [Export] public Button captureButton; 
    [Export] public Button closeInfoButton; 
    [Export] public Label unitInfoLabel; 
    [Export] public Button rotateButton; 
    [Export] public Button explodeButton; // ✅ 自爆按钮
    [Export] public Button illuminateButton; // ✅ 照明按钮
    [Export] public Button repairButton; // ✅ 修复按钮
    [Export] public Button stealthButton; // ✅ 隐身按钮
    [Export] public Button scentButton; // ✅ 嗅探按钮

    [Export] public Button healButton; // ✅ Crystal 黑水晶治疗按钮

    // APC专用按钮
    [Export] public Button loadButton;
    [Export] public PackedScene unloadButtonPreset;
    [Export] public VBoxContainer unloadContainer;
    [Export] public Button supplyButton;


    public Infantry currentUnit;
    private Weapon currentWeapon;
    private Infantry currentTransport;
    private bool isExplosionPreview = false; // ✅ 爆炸范围预览状态
    private bool isProcessingExplosion = false; // ✅ 防重入：防止OnExplodePressed被连续触发
    private bool isFlarePreview = false; // ✅ 照明弹射程预览状态
    private bool isIlluminationPreview = false; // ✅ 照明覆盖范围预览状态
    private bool isProcessingIllumination = false; // ✅ 防重入
    private bool isRepairPreview = false; // ✅ 修复范围预览状态
    private bool isProcessingRepair = false; // ✅ 防重入：防止OnRepairPressed被连续触发
    private Grids pendingIlluminationTarget = null; // ✅ 待确认的照明目标格子

    // ✅ 菜单拖动
    private bool isDraggingMenu = false;
    private Vector2 dragStartMousePos;
    private Vector2 dragStartMenuPos;

    [Export] public PackedScene unloadUnitButtonPreset;
public override void _Ready()
{
    ZIndex = 50;
    // 统一连接补给按钮信号（避免在各个显示方法中重复/遗漏连接）
    if (supplyButton != null)
    {
        var callable = new Callable(this, nameof(OnSupplyPressed));
        if (!supplyButton.IsConnected(BaseButton.SignalName.Pressed, callable))
        {
            supplyButton.Connect(BaseButton.SignalName.Pressed, callable);
        }
    }
    // ✅ 修复按钮信号连接（带安全检查）
    if (repairButton != null)
    {
        var repairCallable = new Callable(this, nameof(OnRepairPressed));
        if (!repairButton.IsConnected(BaseButton.SignalName.Pressed, repairCallable))
        {
            repairButton.Connect(BaseButton.SignalName.Pressed, repairCallable);
        }
    }
    // ⚠️ 注意：explodeButton 的 Pressed 信号已在编辑器中连接 OnExplodePressed
    // 不要在这里再用代码连接，否则会触发两次！
    // ✅ 照明按钮信号连接（带安全检查）
    if (illuminateButton != null)
    {
        var illumCallable = new Callable(this, nameof(OnIlluminatePressed));
        if (!illuminateButton.IsConnected(BaseButton.SignalName.Pressed, illumCallable))
        {
            illuminateButton.Connect(BaseButton.SignalName.Pressed, illumCallable);
        }
    }
    // ✅ 动态创建 Crystal 治疗按钮（如果场景中没有）
    if (healButton == null)
    {
        var container = GetNodeOrNull<VBoxContainer>("ButtonContainer");
        if (container != null)
        {
            healButton = new Button();
            healButton.Name = "HealButton";
            TranslateGroup.SetText(healButton, "治疗");
            healButton.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            container.AddChild(healButton);
            healButton.Pressed += OnHealPressed;
        }
    }

    // ✅ 订阅语言切换信号
    if (TranslateGroup.Instance != null)
    {
        TranslateGroup.Instance.LanguageChanged += OnLanguageChanged;
    }
}

    // ✅ 菜单拖动支持
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                if (GetGlobalRect().HasPoint(GetGlobalMousePosition()))
                {
                    isDraggingMenu = true;
                    dragStartMousePos = GetGlobalMousePosition();
                    dragStartMenuPos = GlobalPosition;
                }
            }
            else
            {
                isDraggingMenu = false;
            }
        }
    }

    public override void _Process(double delta)
    {
        if (isDraggingMenu && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            GlobalPosition = dragStartMenuPos + (GetGlobalMousePosition() - dragStartMousePos);
        }
    }
    private AnimatedSprite2D GetUnitAnim(Infantry u)
    {
        // 1. 基类 animSprite（迁移后所有单位统一走这里）
        if (u.animSprite != null && IsInstanceValid(u.animSprite))
            return u.animSprite;

        // 2. 反射获取子类可能自定义的动画字段（如 MdTank.MdSprite）
        var animField = u.GetType().GetField("animSprite", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (animField != null)
        {
            var anim = animField.GetValue(u) as AnimatedSprite2D;
            if (anim != null && IsInstanceValid(anim)) return anim;
        }
        var mdField = u.GetType().GetField("MdSprite", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (mdField != null)
        {
            var md = mdField.GetValue(u) as AnimatedSprite2D;
            if (md != null && IsInstanceValid(md)) return md;
        }

        // 3. 递归查找子节点（备用）
        foreach (var child in u.GetChildren())
        {
            if (child is AnimatedSprite2D anim) return anim;
            foreach (var grandChild in (child as Node)?.GetChildren() ?? new Godot.Collections.Array<Node>())
                if (grandChild is AnimatedSprite2D deepAnim) return deepAnim;
        }

        return null;
    }

private void ForceCloseAllRanges(GameManager gm)
{
    if (gm?.gridManager == null) return;

    // 关闭移动范围
    gm.gridManager.CloseRange();

    // 关闭攻击范围
    gm.gridManager.HideAttackRange();

    // 关闭兵器范围
    gm.gridManager.ClearWeaponRange();

    // 清除所有回调
    gm.gridManager.ClearAllEmptyCallbacks();

    // 强制隐藏所有格子的图标
    foreach (var grid in gm.gridManager.grids)
    {
        grid.pathIcon?.Hide();
        grid.attackRangeIcon?.Hide();
        grid.OnClickGrid = null;
        grid.OnClickEmpty = null;
    }


}
private List<Grids> GetAdjacentGrids(Grids center)
    {
        var result = new List<Grids>();
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.gridManager == null || center == null) return result;

        Vector2I[] offsets = {
            new Vector2I(0, 1),
            new Vector2I(-1, 0),
            new Vector2I(0, -1),
            new Vector2I(1, 0)
        };

        foreach (var offset in offsets)
        {
            var pos = center.GridIndex + offset;
            if (gm.unitManager.IsValidGrid(pos))
            {
                var grid = gm.gridManager.map[pos.X, pos.Y];
                if (grid != null) result.Add(grid);
            }
        }

        return result;
    }


    private void SetupEmptyClickToCloseMenuOnly(Weapon weapon)
    {
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.gridManager == null) return;

        foreach (var grid in gm.gridManager.grids)
        {
            grid.OnClickEmpty = () => 
            {
                Hide();
                gm.gridManager.ClearWeaponRange();

                if (gm.selectedWeapon == weapon)
                {
                    gm.selectedWeapon = null;
                    weapon.SetVisualNormal();
                }
            };
        }
    }


    public void ShowMenu(Infantry unit, bool afterMove = false, bool onlyShowMenu = false)
    {
        var container = GetNode<VBoxContainer>("unloadContainer");
        foreach (var child in container.GetChildren())
        child.QueueFree();
        bool canSupply = !unit.hasActed && HasSupplyableUnits(unit);
        supplyButton.Visible = canSupply;
        currentTransport = null;

        // ✅ 究极自由：搭载菜单对所有可搭载单位显示！
        // 检查单位是否有搭载能力（通过属性而非类型）
        bool isTransportUnit = unit.canTransportUnits && unit.maxTransportCapacity > 0;
        bool hasNoWeapons = !unit.hasPrimaryWeapon && !unit.hasSecondaryWeapon;


        currentWeapon = null;

        // ✅ 切换单位时重置爆炸预览状态，防止状态残留
        if (currentUnit != unit)
        {
            isExplosionPreview = false;
            isFlarePreview = false;
            isIlluminationPreview = false;
            pendingIlluminationTarget = null;
        }

        currentUnit = unit;

        if (!IsNodeReady()) return;
        if (currentUnit == null) return;

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.gridManager?.ClearWeaponRange();
        gm?.gridManager?.HideAttackRange();

        if (gm?.gridManager != null)
        {
            if (afterMove)
            {
                gm.gridManager.SetupEmptyClickForRollback(unit);
            }
            else
            {
                gm.gridManager.SetEmptyClickToCloseMenuOnly(unit);
            }
        }

        // ✅ 统一判定：可攻击类型 + 未攻击 + 移动后允许 + 有弹药 + 射程内有可伤害目标
        attackButton.Visible = CanShowAttackButton(unit);

        bool canCapture = unit.CanCaptureCurrentGrid();
        bool isCapturing = unit.currentCaptureProgress > 0 && unit.capturingGrid == unit.grid;

        bool allowCaptureAfterMove = unit?.allowCaptureAfterMove ?? true;

        moveButton.Visible = !afterMove && !unit.isMoved && unit.CanMove();
        bool canTear = unit.CanTearCurrentGrid();
        bool isTearing = unit.currentTearProgress > 0 && unit.tearingGrid == unit.grid;
        captureButton.Visible = (canCapture || isCapturing || canTear || isTearing) && !unit.isAttacked && (!unit.isMoved || allowCaptureAfterMove);
        rotateButton?.Hide();

        waitButton.Visible = true;
        infoButton.Visible = true;
        if (loadButton != null) loadButton.Visible = false;
        if (healButton != null) healButton.Visible = false;
        if (unloadContainer != null) unloadContainer.Visible = false;
        if (explodeButton != null)
        {
            // ✅ 爆炸按钮：单位可自爆、未行动，且（一次性自爆 或 有自爆弹）时显示
            bool hasExplodeAmmo = !unit.explosionDestroysSelf ? unit.currentExplodeAmmo > 0 : true;
            explodeButton.Visible = unit.canExplode && !unit.hasActed && unit.state != UnitState.Acted && hasExplodeAmmo;
            TranslateGroup.SetText(explodeButton, isExplosionPreview ? "确认爆炸" : "爆炸");
        }

        if (repairButton != null)
        {
            // ✅ 修复按钮：任何开启修复模块的单位，且未行动时显示
            bool canRepairNow = unit.CanPerformRepair && !unit.hasActed && unit.state != UnitState.Acted
                                && (!unit.isMoved || unit.canRepairAfterMove);
            repairButton.Visible = canRepairNow;
            if (isRepairPreview)
                TranslateGroup.SetText(repairButton, "确认修复");
            else
                TranslateGroup.SetText(repairButton, "修复");
        }

        if (illuminateButton != null)
        {
            // ✅ 照明按钮：任何开启照明模块的单位，且还有照明弹时显示
            bool canIlluminateNow = unit.canIlluminate && unit.currentFlareAmmo > 0
                                && !unit.hasActed && unit.state != UnitState.Acted
                                && (!unit.isMoved || unit.canIlluminateAfterMove);
            illuminateButton.Visible = canIlluminateNow;
            if (isIlluminationPreview)
                TranslateGroup.SetText(illuminateButton, "确认照明");
            else if (isFlarePreview)
                TranslateGroup.SetText(illuminateButton, "取消照明");
            else
                TranslateGroup.SetText(illuminateButton, "照明");
        }

        // ✅ 隐身按钮：canStealth 单位显示；形态锁定隐藏；耗行动模式要求未行动
        if (stealthButton == null && illuminateButton != null)
        {
            stealthButton = new Button();
            stealthButton.Pressed += OnStealthPressed;
            illuminateButton.GetParent().AddChild(stealthButton);
            scentButton = new Button();
            scentButton.Pressed += OnScentPressed;
            illuminateButton.GetParent().AddChild(scentButton);
        }
        if (stealthButton != null)
        {
            bool canToggleStealth = unit.canStealth && !unit.IsStealthFormLocked
                && (!unit.stealthToggleCostsAction || (!unit.hasActed && unit.state != UnitState.Acted));
            stealthButton.Visible = canToggleStealth;
            TranslateGroup.SetText(stealthButton, unit.isStealthed ? "显形" : "隐身");
        }
        if (scentButton != null)
        {
            scentButton.Visible = unit.scentEnabled && !unit.hasActed && unit.state != UnitState.Acted;
            TranslateGroup.SetText(scentButton, "嗅探");
        }

        if (isCapturing)
        {
            moveButton.Visible = false;
            attackButton.Visible = false;
        }

  pendingTransportSelection.Clear();

    bool canMountAPC = CheckCanMountTransport(unit);

    // APC自身不显示装载按钮（保持不变）
    if (isTransportUnit)
{
    ShowTransportMenu(unit, afterMove);
    return;
}


    // 处理装载按钮显示逻辑
    if (loadButton != null)
    {
        loadButton.Visible = canMountAPC;

        // ✅ 修复：安全断开再连接，防止断开不存在的连接报错
        var callable = new Callable(this, nameof(OnLoadPressed));
        if (loadButton.IsConnected(BaseButton.SignalName.Pressed, callable))
        {
            loadButton.Disconnect(BaseButton.SignalName.Pressed, callable);
        }
        loadButton.Connect(BaseButton.SignalName.Pressed, callable);
    }

    waitButton.Visible = true;
        var unitGlobalPos = unit.GlobalPosition;
        Position = new Vector2(unitGlobalPos.X - 40, unitGlobalPos.Y - Size.Y - 60);
        Show();
    }
private List<Infantry> pendingTransportSelection = new List<Infantry>(); 

    private bool isCaptureProcessing = false;

    public void OnCapturePressed()
    {
        GetViewport().SetInputAsHandled();
        if (isCaptureProcessing) return;
        if (currentUnit == null) return;

        isCaptureProcessing = true;

        try 
        {
            // 拆旗分流：可拆旗且本格不可占领且无占领进度时，执行拆旗
            if (currentUnit.canTearFlag && !currentUnit.CanCaptureCurrentGrid() && currentUnit.currentCaptureProgress <= 0)
                currentUnit.PerformTearFlag();
            else
                currentUnit.PerformCapture();
            Hide();

            var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
            if (gm != null)
            {
                foreach (var grid in gm.gridManager.grids)
                {
                    grid.OnClickEmpty = null;
                }
                gm.ClearSelectedInfantry();
            }
        }
        finally 
        {
            var timer = GetTree().CreateTimer(0.5f);
            timer.Timeout += () => isCaptureProcessing = false;
        }
    }

    public void OnMovePressed()
    {
        GetViewport().SetInputAsHandled();
        if (currentUnit != null && currentWeapon == null)
        {
            var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
            if (currentTransport != null && currentUnit == null)
            {
                gm?.gridManager?.ClearWeaponRange();
                gm?.gridManager?.ShowMoveRange(currentTransport);
                Hide();
                return;
            }

            if (gm != null)
            {
                gm.gridManager?.ClearWeaponRange();
                foreach (var grid in gm.gridManager.grids)
                {
                    grid.OnClickEmpty = null;
                }
                gm.gridManager.ShowMoveRange(currentUnit);
            }
        }
        Hide();
    }

    private void OnAttackPressed()
    {
        GetViewport().SetInputAsHandled();
        if (currentWeapon != null && currentUnit == null)
        {
            currentWeapon.ShowAttackRange();
        }
        else if (currentUnit != null && currentWeapon == null)
        {
            currentUnit.OnAttackSelected();
        }
        Hide();
    }

    private void OnHealPressed()
    {
        GetViewport().SetInputAsHandled();
        if (currentWeapon is Crystal crystal)
        {
            // 显示治疗范围，等待用户点击范围内格子确认
            crystal.ShowAttackRange();
        }
        Hide();
    }

    private void OnWaitPressed()
    {
        GetViewport().SetInputAsHandled();
        if (currentWeapon != null && currentUnit == null)
        {
            if (currentWeapon is BlackCannon cannon)
                cannon.DoWait();
            currentWeapon = null;
        }
        else if (currentUnit != null && currentWeapon == null)
        {
            currentUnit.OnWaitSelected();
        }
        Hide();
    }

    public void OnInfoPressed()
    {
        GetViewport().SetInputAsHandled();
        if (currentWeapon != null && currentUnit == null)
        {
            ShowWeaponInfo(currentWeapon);
            return;
        }

        if (currentUnit != null && currentWeapon == null)
        {
            ShowUnitInfo();
            return;
        }

        TranslateGroup.SetText(unitInfoLabel, "未选择单位");
        unitInfoLabel.Visible = true;
        closeInfoButton.Visible = true;
    }

    private void OnLanguageChanged(int langCode)
    {
        // ✅ 语言切换时，如果信息面板开着，重新刷新
        if (unitInfoLabel != null && unitInfoLabel.Visible)
        {
            if (currentUnit != null && currentWeapon == null)
            {
                ShowUnitInfo();
            }
            else if (currentWeapon != null && currentUnit == null)
            {
                ShowWeaponInfo(currentWeapon);
            }
        }
    }

    private void ShowUnitInfo()
    {
        ZIndex = 200;
        if (currentUnit == null) return;

        string infoText = $"{TranslateGroup.T("单位名称")}: {currentUnit.Name}\n";
        infoText += $"{TranslateGroup.T("团队")}: {currentUnit.team}\n";
        infoText += $"{TranslateGroup.T("最大生命值")}: {currentUnit.maxHealth}\n";
        infoText += $"{TranslateGroup.T("生命值")}: {currentUnit.health}\n";
        infoText += $"{TranslateGroup.T("攻击力")}: {currentUnit.baseAttack}\n";
        infoText += $"{TranslateGroup.T("实时主武器攻击力")}: {currentUnit.attack}\n";
        infoText += $"{TranslateGroup.T("实时副武器攻击力")}: {currentUnit.secondaryAtk}\n";
        infoText += $"{TranslateGroup.T("防御力")}: {currentUnit.defense}\n";
        string defenseBonusStr = (int)currentUnit.defenseBonusType switch
        {
            1 => TranslateGroup.T("受地形加成"),
            0 => TranslateGroup.T("不受地形加成"),
            _ => TranslateGroup.T("未知")
        };
        infoText += $"{TranslateGroup.T("防御类型")}: {defenseBonusStr}\n";
        infoText += $"{TranslateGroup.T("移动力")}: {currentUnit.movePoints}\n";

        // ✅ 从 UnitRegistry 读取单位显示名，零硬编码适配新单位
        string unitType = currentUnit.DisplayLabel;

        infoText += $"{TranslateGroup.T("单位类型")}: {unitType}\n";

if (currentUnit.canTransportUnits && currentUnit.maxTransportCapacity > 0)
{
    infoText += $"\n=== {TranslateGroup.T("可运输单位配置信息")} ===\n";

    // 1. 可搭载单位种类
    string loadableTypes = currentUnit.canTransportUnitTypes != null && currentUnit.canTransportUnitTypes.Count > 0
        ? string.Join(", ", currentUnit.canTransportUnitTypes)
        : TranslateGroup.T("无");
    infoText += $"{TranslateGroup.T("可搭载单位")}: {loadableTypes}\n";

    // 2. 远程搭载
    infoText += $"{TranslateGroup.T("远程搭载")}: {(currentUnit.CanFarMounting ? TranslateGroup.T("✓ 已开启") : TranslateGroup.T("✗ 未开启"))}\n";
    if (currentUnit.CanFarMounting)
    {
        infoText += $"{TranslateGroup.T("远程搭载距离")}: {currentUnit.minFarMountingDistance}-{currentUnit.maxFarMountingDistance}格\n";
    }

    // 3. 已搭载单位列表 - 用 maxTransportCapacity
    infoText += $"\n[{TranslateGroup.T("已搭载单位")}] {currentUnit.transportedUnits.Count}/{currentUnit.maxTransportCapacity}\n";
    if (currentUnit.transportedUnits.Count > 0)
    {
        foreach (var transportedUnit in currentUnit.transportedUnits)
        {
            if (transportedUnit != null && IsInstanceValid(transportedUnit))
            {
                // ✅ 从 UnitRegistry 读取装载单位显示名
                string loadedType = transportedUnit.DisplayLabel;
                int hpBars = Mathf.CeilToInt(transportedUnit.health / 10f);
                infoText += $"  • {transportedUnit.Name} ({loadedType}) HP:{hpBars}\n";
            }
        }
    }
    else
    {
        infoText += TranslateGroup.T("  (空)\n");
    }

    infoText += $"\n";
}

        string overlapType = currentUnit.overlapType switch
        {
            UnitOverlapType.NonOverlapping => "NonOverlapping",
            UnitOverlapType.Overlapping => "Overlapping",
            UnitOverlapType.Oozium => "Oozium",
            _ => "Unknown"
        };
        infoText += $"{TranslateGroup.T("范围类型")}: {overlapType}\n";

        string primaryWeaponStr;
        if (!currentUnit.hasPrimaryWeapon)
            primaryWeaponStr = TranslateGroup.T("无");
        else if (currentUnit.HasInfinitePrimaryAmmo)
            primaryWeaponStr = $"{TranslateGroup.T("有")} (∞{TranslateGroup.T("无限")})";
        else
            primaryWeaponStr = $"{TranslateGroup.T("有")} ({TranslateGroup.T("弹药")}: {currentUnit.currentPrimaryAmmo}/{currentUnit.maxPrimaryAmmo})";

        string secondaryWeaponStr = currentUnit.hasSecondaryWeapon ? TranslateGroup.T("有") : TranslateGroup.T("无");

        string primaryEffect = "";
        if (currentUnit.hasPrimaryWeapon)
        {
            var effects = new System.Collections.Generic.List<string>();
            if (currentUnit.primaryAntiArmor) effects.Add(TranslateGroup.T("反装甲"));
            if (currentUnit.primaryAntiInfantry) effects.Add(TranslateGroup.T("反步兵"));
            primaryEffect = $"[{string.Join("/", effects)}]";
        }

        string secondaryEffect = "";
        if (currentUnit.hasSecondaryWeapon)
        {
            var effects = new System.Collections.Generic.List<string>();
            if (currentUnit.secondaryAntiArmor) effects.Add(TranslateGroup.T("反装甲"));
            if (currentUnit.secondaryAntiInfantry) effects.Add(TranslateGroup.T("反步兵"));
            secondaryEffect = $"[{string.Join("/", effects)}]";
        }

        infoText += $"{TranslateGroup.T("主武器")}: {primaryWeaponStr} {primaryEffect}\n";
        infoText += $"{TranslateGroup.T("副武器")}: {secondaryWeaponStr} {secondaryEffect}\n";

        string attackRangeStr = currentUnit.useMinMaxAttackRange 
            ? $"{currentUnit.minAttackRange}-{currentUnit.maxAttackRange}" 
            : currentUnit.attackRange.ToString();
        infoText += $"{TranslateGroup.T("攻击范围")}: {attackRangeStr}\n";

        string state = currentUnit.state switch
        {
            UnitState.Idle => "Idle",
            UnitState.Moved => "Moved",
            UnitState.Acted => "Acted",
            _ => "Unknown"
        };
        infoText += $"{TranslateGroup.T("状态")}: {state}\n";

        string posText = currentUnit.grid != null 
            ? $"(X:{currentUnit.grid.GridIndex.X},Y:{currentUnit.grid.GridIndex.Y})" 
            : TranslateGroup.T("未绑定格子");
        string moveTypeStr = currentUnit.moveType switch
        {
            MoveType.Infantry => TranslateGroup.T("步兵移动"),
            MoveType.Mech => TranslateGroup.T("机甲移动"),
            MoveType.Oozium => TranslateGroup.T("Oozium移动"),
            MoveType.Treads => TranslateGroup.T("履带移动"), 
            MoveType.Tire => TranslateGroup.T("轮胎移动"),
            MoveType.AeroSpacer => TranslateGroup.T("空天移动"),
            MoveType.AirPlane => TranslateGroup.T("飞机移动"),
            MoveType.AirShip => TranslateGroup.T("飞艇移动"),
            MoveType.Drone => TranslateGroup.T("无人机移动"),
            MoveType.HeliCopter => TranslateGroup.T("直升机移动"),
            MoveType.Hover => TranslateGroup.T("登陆艇移动"),
            MoveType.Naval => TranslateGroup.T("海军移动"),
            MoveType.LAVAHOVER => TranslateGroup.T("岩浆登陆者移动"),
            MoveType.LAVARUNNER => TranslateGroup.T("岩浆行者移动"),
            MoveType.SpaceShiper => TranslateGroup.T("星舰移动"),
            MoveType.PipeRunner => TranslateGroup.T("管道行者移动"),
            MoveType.Train => TranslateGroup.T("普通火车移动"),
            MoveType.GasTrain => TranslateGroup.T("蒸汽机移动"),
            MoveType.FASTER => TranslateGroup.T("高铁移动"),
            MoveType.Missile => TranslateGroup.T("导弹飞行"),
            _ => "Unknown"
        };
        infoText += $"{TranslateGroup.T("移动方式")}: {moveTypeStr}\n";
        string fuelText = currentUnit.consumeFuel 
            ? $"{currentUnit.fuel}/{currentUnit.maxFuel} ({TranslateGroup.T("阈值")}:{currentUnit.lowFuelThreshold}, {TranslateGroup.T("日耗")}:{currentUnit.dailyFuelConsumption})" 
            : TranslateGroup.T("无限");
        infoText += $"{TranslateGroup.T("燃料")}: {fuelText}\n";

        // ✅ 自爆系统信息
        if (currentUnit.canExplode)
        {
            infoText += $"\n=== {TranslateGroup.T("自爆系统")} ===\n";
            infoText += $"{TranslateGroup.T("爆炸范围")}: {currentUnit.explosionMinRange}-{currentUnit.explosionMaxRange}格\n";
            string damageModeStr = currentUnit.explosionDamageMode switch
            {
                0 => $"{TranslateGroup.T("固定值")} {currentUnit.explosionFixedValue}",
                1 => $"{currentUnit.explosionPercentValue * 100}%{TranslateGroup.T("最大")}HP",
                2 => TranslateGroup.T("攻击公式"),
                _ => TranslateGroup.T("未知")
            };
            infoText += $"{TranslateGroup.T("伤害模式")}: {damageModeStr}\n";
            string targetModeStr = currentUnit.explosionTargetMode switch
            {
                0 => TranslateGroup.T("所有单位"),
                1 => TranslateGroup.T("仅敌方"),
                2 => TranslateGroup.T("仅友方"),
                _ => TranslateGroup.T("未知")
            };
            infoText += $"{TranslateGroup.T("目标筛选")}: {targetModeStr}\n";
            infoText += $"{TranslateGroup.T("摧毁自身")}: {(currentUnit.explosionDestroysSelf ? "✓" : "✗")}\n";
            infoText += $"{TranslateGroup.T("可击杀")}: {(currentUnit.explosionCanKill ? "✓" : TranslateGroup.T("✗ (保留1HP)"))}\n";
            infoText += $"{TranslateGroup.T("影响兵器")}: {(currentUnit.explosionAffectsWeapons ? "✓" : "✗")}\n";
            infoText += $"{TranslateGroup.T("回血可超上限")}: {(currentUnit.explosionCanExceedMaxHealth ? "✓" : "✗")}\n";
        }

        // ✅ 照明模块信息
        if (currentUnit.canIlluminate)
        {
            infoText += $"\n=== {TranslateGroup.T("照明模块")} ===\n";
            infoText += $"{TranslateGroup.T("照明弹")}: {currentUnit.currentFlareAmmo}/{currentUnit.maxFlareAmmo}\n";
            infoText += $"{TranslateGroup.T("投射射程")}: {currentUnit.minLaunchRange}-{currentUnit.maxLaunchRange}格\n";
            infoText += $"{TranslateGroup.T("照明覆盖")}: {currentUnit.minIlluminationRange}-{currentUnit.maxIlluminationRange}格\n";
            infoText += $"{TranslateGroup.T("持续回合")}: {currentUnit.flareDurationTurns}大回合\n";
            infoText += $"{TranslateGroup.T("移动后可照明")}: {(currentUnit.canIlluminateAfterMove ? "✓" : "✗")}\n";
        }

        if (currentUnit.grid != null)
        {
            infoText += $"\n=== {TranslateGroup.T("自爆系统")} ===\n";
            infoText += $"{TranslateGroup.T("爆炸范围")}: {currentUnit.explosionMinRange}-{currentUnit.explosionMaxRange}格\n";
            string damageModeStr = currentUnit.explosionDamageMode switch
            {
                0 => $"{TranslateGroup.T("固定值")} {currentUnit.explosionFixedValue}",
                1 => $"{currentUnit.explosionPercentValue * 100}%{TranslateGroup.T("最大")}HP",
                2 => TranslateGroup.T("攻击公式"),
                _ => TranslateGroup.T("未知")
            };
            infoText += $"{TranslateGroup.T("伤害模式")}: {damageModeStr}\n";
            string targetModeStr = currentUnit.explosionTargetMode switch
            {
                0 => TranslateGroup.T("所有单位"),
                1 => TranslateGroup.T("仅敌方"),
                2 => TranslateGroup.T("仅友方"),
                _ => TranslateGroup.T("未知")
            };
            infoText += $"{TranslateGroup.T("目标筛选")}: {targetModeStr}\n";
            infoText += $"{TranslateGroup.T("摧毁自身")}: {(currentUnit.explosionDestroysSelf ? "✓" : "✗")}\n";
            infoText += $"{TranslateGroup.T("可击杀")}: {(currentUnit.explosionCanKill ? "✓" : TranslateGroup.T("✗ (保留1HP)"))}\n";
            infoText += $"{TranslateGroup.T("影响兵器")}: {(currentUnit.explosionAffectsWeapons ? "✓" : "✗")}\n";
            infoText += $"{TranslateGroup.T("回血可超上限")}: {(currentUnit.explosionCanExceedMaxHealth ? "✓" : "✗")}\n";
        }

        if (currentUnit.grid != null)
        {
            infoText += $"\n=== {TranslateGroup.T("地形信息")} ===\n";
            infoText += $"{TranslateGroup.T("地形类型")}: {currentUnit.grid.gridType}\n";
            string terrainDefenseText = currentUnit.grid.GetTerrainDefenseDescription();
            if ((int)currentUnit.defenseBonusType == 0)
                infoText += $"{TranslateGroup.T("地形防御")}: {terrainDefenseText} (单位不受加成)\n";
            else
                infoText += $"{TranslateGroup.T("地形防御")}: {terrainDefenseText}\n";

            // ✅ 新增：显示格子自定义伤害系统信息
            infoText += $"\n=== {TranslateGroup.T("格子自定义效果")} ===\n";

            // 血量相关
            infoText += $"[{TranslateGroup.T("血量系统")}]\n";
            infoText += $"  {TranslateGroup.T("可摧毁单位")}: {(currentUnit.grid.canDestroyUnit ? "✓" : TranslateGroup.T("✗ (锁血1点)"))}\n";
            if (currentUnit.grid.fixedDamagePerTurn != 0)
                infoText += $"  {TranslateGroup.T("固定伤害")}/回合: {currentUnit.grid.fixedDamagePerTurn} ({(currentUnit.grid.fixedDamagePerTurn > 0 ? TranslateGroup.T("扣血") : TranslateGroup.T("回血"))})\n";
            if (currentUnit.grid.fixedAttackPerTurn != 0)
                infoText += $"  {TranslateGroup.T("固定攻击")}/回合: {currentUnit.grid.fixedAttackPerTurn} ({(currentUnit.grid.fixedAttackPerTurn > 0 ? TranslateGroup.T("扣血(经防御)") : TranslateGroup.T("回血"))})\n";
            infoText += $"  {TranslateGroup.T("可超血量上限")}: {(currentUnit.grid.canOverMaxHealth ? "✓" : "✗")}\n";

            // 弹药相关
            infoText += $"\n[{TranslateGroup.T("弹药系统")}]\n";
            if (currentUnit.grid.fixedAmmoChangePerTurn != 0)
            {
                string ammoChangeText = currentUnit.grid.fixedAmmoChangePerTurn > 0 ? $"+{currentUnit.grid.fixedAmmoChangePerTurn} ({TranslateGroup.T("补充")})" : $"{currentUnit.grid.fixedAmmoChangePerTurn} ({TranslateGroup.T("消耗")})";
                infoText += $"  {TranslateGroup.T("弹药变化")}/回合: {ammoChangeText}\n";
            }
            else
            {
                infoText += $"  {TranslateGroup.T("弹药变化")}/回合: 无\n";
            }
            infoText += $"  {TranslateGroup.T("可超弹药上限")}: {(currentUnit.grid.ammoCanOverMax ? "✓" : "✗")}\n";
            infoText += $"  {TranslateGroup.T("可归零")}: {(currentUnit.grid.ammoCanReachZero ? "✓" : TranslateGroup.T("✗ (强制剩1)"))}\n";

            // 燃料相关
            infoText += $"\n[{TranslateGroup.T("燃料系统")}]\n";
            if (currentUnit.grid.fixedFuelChangePerTurn != 0)
            {
                string fuelChangeText = currentUnit.grid.fixedFuelChangePerTurn > 0 ? $"+{currentUnit.grid.fixedFuelChangePerTurn} ({TranslateGroup.T("补充")})" : $"{currentUnit.grid.fixedFuelChangePerTurn} ({TranslateGroup.T("消耗")})";
                infoText += $"  {TranslateGroup.T("燃料变化")}/回合: {fuelChangeText}\n";
            }
            else
            {
                infoText += $"  {TranslateGroup.T("燃料变化")}/回合: 无\n";
            }
            infoText += $"  {TranslateGroup.T("可超燃料上限")}: {(currentUnit.grid.fuelCanOverMax ? "✓" : "✗")}\n";
            infoText += $"  {TranslateGroup.T("可归零")}: {(currentUnit.grid.fuelCanReachZero ? "✓" : TranslateGroup.T("✗ (强制剩1)"))}\n";
        }

        if (currentUnit.grid?.city != null)
        {
            string ownerText = currentUnit.grid.city.facilityTeam switch
            {
                "Player0" => TranslateGroup.T("中立（双方共用）"),
                "Player1" => TranslateGroup.T("🔴 Player1 占领"),
                "Player2" => TranslateGroup.T("🔵 Player2 占领"),
                _ => TranslateGroup.T("无归属（无法补给）")
            };
            infoText += $"{TranslateGroup.T("CITY归属")}: {ownerText}\n";
        }

        string captureAbility = currentUnit.captureAbility == CaptureAbility.CanCapture ? TranslateGroup.T("可以占领") : TranslateGroup.T("不能占领");
        infoText += $"{TranslateGroup.T("占领能力")}: {captureAbility}\n";
        if (currentUnit.captureAbility == CaptureAbility.CanCapture && currentUnit.grid?.city != null)
        {
            infoText += $"{TranslateGroup.T("占领力")}: {currentUnit.GetCapturePower()}\n";
            infoText += $"{TranslateGroup.T("占领消耗")}: {currentUnit.grid.city?.capturePointsRequired ?? 20}\n";
            if (currentUnit.currentCaptureProgress > 0)
            {
                infoText += $"{TranslateGroup.T("当前占领进度")}: {currentUnit.currentCaptureProgress}/{currentUnit.grid.city?.capturePointsRequired ?? 20}\n";
                if (currentUnit.capturingGrid != null)
                {
                    infoText += $"{TranslateGroup.T("占领目标")}: ({currentUnit.capturingGrid.GridIndex.X},{currentUnit.capturingGrid.GridIndex.Y})\n";
                }
            }
        }
        
        // ========== ✅ 新增：战争迷雾视野信息 ==========
        infoText += $"{TranslateGroup.T("视野能力")} ";

        // 基础视野
        int actualVision = currentUnit.ActualVisionRange;
        infoText += $"{TranslateGroup.T("基础视野")}: {actualVision}{TranslateGroup.T("格")}";
        if (!currentUnit.useConfigVision && currentUnit.visionRange >= 0)
            infoText += $" ({TranslateGroup.T("自定义")}:{currentUnit.visionRange})";
        else
            infoText += $" ({TranslateGroup.T("配置表")})";
        infoText += "\n";

        // 地形视野加成状态
        if (currentUnit.overrideGlobalTerrainBonus)
        {
            infoText += $"{TranslateGroup.T("地形加成")}: {TranslateGroup.T("使用专属矩阵")} ({currentUnit.unitTerrainVisionBonus?.Count ?? 0}{TranslateGroup.T("项")})\n";
        }
        else
        {
            infoText += $"{TranslateGroup.T("地形加成")}: {TranslateGroup.T("使用全局默认")}\n";
        }

        // 如果当前在格子上，显示当前地形的实际加成
        if (currentUnit.grid != null)
        {
            int terrainBonus = currentUnit.GetTerrainVisionBonus(currentUnit.grid.gridType);
            string sign = terrainBonus > 0 ? "+" : "";
            infoText += $"{TranslateGroup.T("当前地形")}({currentUnit.grid.gridType}): {sign}{terrainBonus}{TranslateGroup.T("格")}\n";
            infoText += $"{TranslateGroup.T("实际视野")}: {actualVision + terrainBonus}{TranslateGroup.T("格")}\n";
        }

infoText += $"{TranslateGroup.T("所在位置")}: {posText}\n";

        unitInfoLabel.Text = infoText;
        unitInfoLabel.Visible = true;
        closeInfoButton.Visible = true;
    }

    public void OnCloseInfoPressed()
    {
        GetViewport().SetInputAsHandled();
        unitInfoLabel.Visible = false;
        closeInfoButton.Visible = false;
    }

private void ShowWeaponInfo(Weapon weapon)
    {
        ZIndex = 200;
        string info = "";

        info += $"[{TranslateGroup.T("兵器")}] {weapon.Name}\n";
        info += $"{TranslateGroup.T("团队")}: {weapon.team}\n";

        // ✅ 修复：兵器血量显示格式（1-99格，不是百分比）
        int hpBars = Mathf.CeilToInt((float)weapon.health / 10f);
        hpBars = Mathf.Clamp(hpBars, 1, 99);
        info += $"{TranslateGroup.T("血量")}: {weapon.health}/{weapon.maxHealth} (显示{hpBars}格)\n";

        info += $"{TranslateGroup.T("状态")}: {(weapon.hasActed ? TranslateGroup.T("已行动") : TranslateGroup.T("待机"))}\n";
        info += $"{TranslateGroup.T("每回合攻击次数")}: {weapon.remainingAttacks}/{weapon.maxAttacksPerTurn}\n";
        info += $"{TranslateGroup.T("可旋转")}: {(weapon.canRotate ? TranslateGroup.T("是") : TranslateGroup.T("否"))}\n";
        info += $"{TranslateGroup.T("可攻击兵器")}: {(weapon.CanAttackWeapon ? TranslateGroup.T("是") : TranslateGroup.T("否"))}\n";

        if (weapon is BlackCannon cannon)
        {
            info += $"{TranslateGroup.T("黑炮专用配置")}";
            info += $"{TranslateGroup.T("射程深度")}: {cannon.maxAttackDepth}格\n";
            info += $"{TranslateGroup.T("伤害模式")}: {(cannon.useModifiedDamage ? TranslateGroup.T("改良模式") : TranslateGroup.T("传统模式"))}\n";

            // ✅ 新增：弹药系统完整信息
            info += $"[{TranslateGroup.T("弹药系统")}]";
            if (cannon.useAmmoSystem)
            {
                info += $"{TranslateGroup.T("弹药")}: {cannon.currentAmmo}/{cannon.maxAmmo}\n";
                info += $"{TranslateGroup.T("补给状态")}: {(cannon.currentAmmo >= cannon.maxAmmo ? TranslateGroup.T("已满") : TranslateGroup.T("可补给"))}\n";
            }
            else
            {
                info += $"{TranslateGroup.T("弹药")}: 无限 (∞)\n";
            }

            // ✅ 新增：冷却系统完整信息
            info += $"[{TranslateGroup.T("冷却系统")}]";
            if (cannon.useCooldownSystem)
            {
                info += $"{TranslateGroup.T("冷却周期")}: {cannon.cooldownTurns}回合\n";
                info += $"{TranslateGroup.T("周期攻击次数")}: {cannon.attacksPerCooldown}次\n";
                info += $"{TranslateGroup.T("存次数")}: {(cannon.storeAttacks ? TranslateGroup.T("是") : TranslateGroup.T("否"))}\n";
                info += $"{TranslateGroup.T("当前周期进度")}: {cannon.turnsSinceLastAttack}/{cannon.cooldownTurns}回合\n";
                info += $"{TranslateGroup.T("周期剩余次数")}: {cannon.attacksRemainingInCycle}次\n";
                info += $"{TranslateGroup.T("冷却就绪")}: {(cannon.cooldownReady ? TranslateGroup.T("✓ 是") : TranslateGroup.T("✗ 否"))}\n";

                if (!cannon.cooldownReady)
                {
                    info += $"{TranslateGroup.T("还需等待")}: {cannon.cooldownTurns - cannon.turnsSinceLastAttack}回合\n";
                }
            }
            else
            {
                info += $"{TranslateGroup.T("冷却")}: {TranslateGroup.T("无（每回合重置）")}";
                info += $"{TranslateGroup.T("每回合次数")}: {weapon.maxAttacksPerTurn}{TranslateGroup.T("次")}";
            }

            // ✅ 新增：综合攻击状态
            info += $"[{TranslateGroup.T("攻击状态")}]";
            info += $"{TranslateGroup.T("可攻击")}: {(cannon.CanAttack() ? TranslateGroup.T("✓ 是") : TranslateGroup.T("✗ 否"))}\n";
            if (!cannon.CanAttack())
            {
                if (cannon.useAmmoSystem && cannon.currentAmmo <= 0)
                    info += $"{TranslateGroup.T("原因")}: 弹药耗尽\n";
                if (cannon.useCooldownSystem && !cannon.cooldownReady)
                    info += $"{TranslateGroup.T("原因")}: 冷却中 ({cannon.turnsSinceLastAttack}/{cannon.cooldownTurns}回合)\n";
                if (cannon.useCooldownSystem && cannon.attacksRemainingInCycle <= 0)
                    info += $"{TranslateGroup.T("原因")}: 周期次数已用完\n";
            }

            // 伤害配置
            if (!cannon.useModifiedDamage)
            {
                info += $"[{TranslateGroup.T("传统伤害")}]";
                info += $"{TranslateGroup.T("固定伤害")}: {cannon.fixedDamagePercent * 100:F0}%\n";
                info += $"{TranslateGroup.T("可摧毁")}: {(cannon.CanDestroy ? TranslateGroup.T("是") : TranslateGroup.T("否"))}\n";
            }
            else
            {
                info += $"[{TranslateGroup.T("改良伤害")}]";
                info += $"{TranslateGroup.T("血量变化")}: {cannon.modifiedHealthPercent * 100:F0}% ({(cannon.modifiedHealthPercent > 0 ? TranslateGroup.T("扣血") : cannon.modifiedHealthPercent < 0 ? TranslateGroup.T("回血") : TranslateGroup.T("无变化"))})\n";
                info += $"{TranslateGroup.T("弹药变化")}: {cannon.modifiedAmmoPercent * 100:F0}% ({(cannon.modifiedAmmoPercent > 0 ? TranslateGroup.T("消耗") : cannon.modifiedAmmoPercent < 0 ? TranslateGroup.T("补充") : TranslateGroup.T("无变化"))})\n";
                info += $"{TranslateGroup.T("燃料变化")}: {cannon.modifiedFuelPercent * 100:F0}% ({(cannon.modifiedFuelPercent > 0 ? TranslateGroup.T("消耗") : cannon.modifiedFuelPercent < 0 ? TranslateGroup.T("补充") : TranslateGroup.T("无变化"))})\n";
                info += $"{TranslateGroup.T("可超血量上限")}: {(cannon.canOverMaxHp ? TranslateGroup.T("是") : TranslateGroup.T("否"))}\n";
                info += $"{TranslateGroup.T("可摧毁")}: {(cannon.CanDestroy ? TranslateGroup.T("是") : TranslateGroup.T("否"))}\n";
            }

            info += $"[{TranslateGroup.T("其他配置")}]";
            info += $"{TranslateGroup.T("朝向")}: {cannon.direction}\n";
            info += $"{TranslateGroup.T("可攻击兵器")}: {(cannon.CanAttackWeapon ? TranslateGroup.T("是") : TranslateGroup.T("否"))}\n";
            info += $"{TranslateGroup.T("目标选择")}: {cannon.targetSelectionMode switch 
            { 
                BlackCannon.TargetSelectionMode.AllSelect => TranslateGroup.T("所有单位"),
                BlackCannon.TargetSelectionMode.OnlyEnemyUnits => TranslateGroup.T("仅敌方单位"),
                BlackCannon.TargetSelectionMode.OnlyUserUnits => TranslateGroup.T("仅己方单位"),
                _ => TranslateGroup.T("未知")
            }}\n";

            info += $"[{TranslateGroup.T("操作说明")}]";
            info += $"• {TranslateGroup.T("左键点击")}: {TranslateGroup.T("选择/显示攻击范围")}";
            info += $"• {TranslateGroup.T("R键")}: {TranslateGroup.T("旋转方向")}";
            info += $"• {TranslateGroup.T("攻击范围")}: {TranslateGroup.T("三角形区域")}，{TranslateGroup.T("深度")}{cannon.maxAttackDepth}{TranslateGroup.T("格")}";

            if (cannon.useAmmoSystem)
            {
                info += $"• {TranslateGroup.T("需要APC在范围内补给弹药")}";
            }
        }

else if (weapon is Laser laser)
        {
            info = laser.GetLaserFullInfo();
        }
        unitInfoLabel.Text = info;
        unitInfoLabel.Visible = true;
        closeInfoButton.Visible = true;
    }    public void ShowMoveChoiceDialog(Infantry movingUnit, Infantry targetUnit, Grids targetGrid, Infantry transport = null)
    {
        // 先隐藏当前菜单
        Hide();

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;

        // 创建弹窗面板
        var dialog = new Control();
        dialog.Name = "MoveChoiceDialog";
        dialog.ZIndex = 500;
        dialog.SetAnchorsPreset(Control.LayoutPreset.Center);

        // 背景遮罩
        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0, 0, 0, 0.5f);
        dialog.AddChild(bg);

        // 弹窗面板
        var panel = new Panel();
        panel.CustomMinimumSize = new Godot.Vector2(300, 200);
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.2f, 0.2f, 0.3f, 0.95f);
        style.SetCornerRadiusAll(12);
        panel.AddThemeStyleboxOverride("panel", style);
        dialog.AddChild(panel);

        // 标题
        var title = new Label();
        title.Text = $"{TranslateGroup.T("目标有")} {targetUnit.Name}";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", Colors.Yellow);
        title.Position = new Godot.Vector2(0, 10);
        title.Size = new Godot.Vector2(300, 30);
        panel.AddChild(title);

        // 按钮容器
        var vbox = new VBoxContainer();
        vbox.Position = new Godot.Vector2(50, 50);
        vbox.Size = new Godot.Vector2(200, 140);
        vbox.AddThemeConstantOverride("separation", 10);
        panel.AddChild(vbox);

        // 取消按钮
        var cancelBtn = new Button();
        TranslateGroup.SetText(cancelBtn, "❌ 取消");
        cancelBtn.CustomMinimumSize = new Godot.Vector2(200, 35);
        cancelBtn.Pressed += () => {
            dialog.QueueFree();
            // 保持当前移动范围显示，什么都不做
        };
        vbox.AddChild(cancelBtn);

        // 确认移动按钮
        var confirmBtn = new Button();
        TranslateGroup.SetText(confirmBtn, "✅ 确认移动");
        confirmBtn.CustomMinimumSize = new Godot.Vector2(200, 35);
        confirmBtn.Pressed += () => {
            dialog.QueueFree();
            // 执行移动
            var gridMgr = gm?.gridManager;
            if (gridMgr != null)
            {
                gridMgr.MoveTo(movingUnit, targetGrid);
            }
        };
        vbox.AddChild(confirmBtn);

        // 切换单位按钮
        var switchBtn = new Button();
        switchBtn.Text = $"🔄 {TranslateGroup.T("切换到")}{targetUnit.Name}";
        switchBtn.CustomMinimumSize = new Godot.Vector2(200, 35);
        switchBtn.Pressed += () => {
            dialog.QueueFree();
            // 关闭当前范围，切换到新单位
            gm?.gridManager?.CloseRange();
            gm?.gridManager?.HideAttackRange();
            gm?.gridManager?.ClearWeaponRange();
            Hide();
            gm?.ClearSelectedInfantry();
            gm?.OnSelectPiece(targetUnit);
        };
        vbox.AddChild(switchBtn);

        // 搭载按钮（仅当APC可用时）
        if (transport != null && transport.CanTransportUnit(movingUnit))
        {
            var mountBtn = new Button();
            mountBtn.Text = $"🚛 {TranslateGroup.T("搭载到")}{transport.Name}";
            mountBtn.CustomMinimumSize = new Godot.Vector2(200, 35);
            mountBtn.Pressed += () => {
                dialog.QueueFree();
                // 显示搭载菜单
                ShowTransportLoadMenu(transport, movingUnit);
                gm?.gridManager?.CloseRange();
            };
            vbox.AddChild(mountBtn);
        }

        GetTree().CurrentScene.AddChild(dialog);

    }

    // ✅ 重写 Hide 确保隐藏时取消所有预览
    public new void Hide()
    {
        CancelExplosionPreview();
        CancelIlluminationPreview();
        CancelRepairPreview();
        base.Hide();
    }
}
