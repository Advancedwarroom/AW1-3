// GameManager.cs - 添加完整的单位统计系统
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public enum UnitCategory
{
    Infantry,   
    Tank,       
    MdTank,     
    Vehicle,   
    Mech,       
    Oozium,     

    Rocket,     
    FlyBomb,    // 飞弹/自爆单位
    Artillery,
    APC, 
    Recon,
    AntiAir,
    AntiTank,
    Other       // 其他未分类单位
}

public partial class GameManager : Node
{
    [Export] public GridManager gridManager;
    [Export] public UnitManager unitManager;
    [Export] public WeaponManager weaponManager;
    [Export] public FogOfWarManager fogOfWarManager;
    [Export] public TerrainEditor terrainEditor;
    [ExportGroup("胜利结算")]
    [Export] public Control victoryPanel; 
    [Export] public RichTextLabel victoryTitleLabel; 
    [Export] public RichTextLabel  victorySubtitleLabel; 
    [Export] public Button restartButton;
    private bool gameEnded = false;

    public List<Weapon> weapons = new List<Weapon>(); 
    // 各类单位列表
    public Godot.Collections.Array<Infantry> infantryUnits = new();
    public Godot.Collections.Array<Infantry> mechUnits = new();
    public Godot.Collections.Array<Infantry> ooziumUnits = new();
    public Godot.Collections.Array<Infantry> tankUnits = new();
    public Godot.Collections.Array<Infantry> mdTankUnits = new();
    public Godot.Collections.Array<Infantry> vehicleUnits = new();
    public Godot.Collections.Array<Infantry> artilleryUnits = new(); 
    public Godot.Collections.Array<Infantry> antiairUnits = new(); 
    public Godot.Collections.Array<Infantry> antiTankUnits = new();
    public Godot.Collections.Array<Infantry> apcUnits = new();
    public Godot.Collections.Array<Infantry> reconUnits = new();
    public Godot.Collections.Array<Infantry> flyBombUnits = new();
    public Godot.Collections.Array<Infantry> flareUnits = new();
    public Godot.Collections.Array<Infantry> bikeUnits = new();

    // 回合管理
    public int turns = 1;
    public int turnPhase = 1;
    public const int totalPhases = 3;

    // 资金系统
    [Export] public int p1Funds = 0;
    [Export] public int p2Funds = 0;
    [Export] public int p1FundsMax = 999999;
    [Export] public int p2FundsMax = 999999;

    // 当前选中的单位
    public Infantry selectedInfantry = null;
    public Weapon selectedWeapon = null;
    public bool isSelectingAttackTarget = false;

    // UI 引用
    public Label turnEndLabel;
    public Label TurnLabel;
    public RichTextLabel UnitLists;

    // ✅ 新增：缩放滑块和统计面板收缩
    public HSlider zoomSlider;
    public Button unitListsToggleBtn;
    public Control unitListsPanel;
    private bool unitListsExpanded = true;
    private CameraTouchController cameraController;

    public Dictionary<Infantry, UnitCategory> unitCategories = new();
    public enum StatsMode { Units, Facilities, Funds, Value }
    public StatsMode statsMode = StatsMode.Units;
    private Button unitStatsBtn;
    private Button facilityStatsBtn;
    private Button fundsStatsBtn;
    private Button valueStatsBtn;
    private Button nextPhaseButton;

    // ✅ 生产菜单拖拽
    private bool isDraggingProductionMenu = false;
    private Vector2 dragOffsetProduction = Vector2.Zero;
    private Control currentDraggedProductionMenu = null;

    // ✅ 导入地图时屏蔽胜利检查
    public bool blockVictoryCheck = false;

    public override void _Ready()
    {   

        AddToGroup("game_manager");
        CallDeferred(nameof(DeferredInit));
        turnEndLabel = GetTree().CurrentScene.GetNodeOrNull<Label>("TurnEndLabel");
        TurnLabel = GetTree().CurrentScene.GetNodeOrNull<Label>("TurnLabel");
        UnitLists = GetTree().CurrentScene.GetNodeOrNull<RichTextLabel>("UnitLists");

        // 确保 GridManager 先初始化
        gridManager?.Init();

        // 延迟一帧确保所有节点就绪
        CallDeferred(nameof(DeferredUnitInit));

        UpdateTurnLabel();
        UpdateUnitLists();  // ✅ 初始化单位统计
        
        // ✅ 创建统计切换按钮
        CreateStatsToggleButtons();
        SetProcessInput(true);
            if (restartButton != null)
    {
        restartButton.Pressed += OnRestartPressed;

        // ========== ✅ 战争迷雾初始化 ==========
        if (fogOfWarManager == null)
        {
            fogOfWarManager = GetTree().GetFirstNodeInGroup("fog_of_war_manager") as FogOfWarManager;
        }

    }
    
    if (turnEndLabel != null) turnEndLabel.ZIndex = 200;
    if (TurnLabel != null) TurnLabel.ZIndex = 200;
    if (UnitLists != null) UnitLists.ZIndex = 200;
    gameEnded = false;
    
        if (fogOfWarManager == null)
    {
        fogOfWarManager = GetTree().GetFirstNodeInGroup("fog_of_war_manager") as FogOfWarManager;
    }
    
    // 延迟刷新战争迷雾，确保所有节点就绪
    if (fogOfWarManager != null && fogOfWarManager.isFogOfWarEnabled)
    {
        CallDeferred(nameof(DeferredFogRefresh));
    }
    
    // ✅ 确保 AI_Manager 存在
    var aiManager = GetTree().GetFirstNodeInGroup("ai_manager") as AI_Manager;
    if (aiManager == null)
    {
        aiManager = new AI_Manager();
        aiManager.Name = "AI_Manager";
        aiManager.gameManager = this;
        AddChild(aiManager);
        GD.Print("[GameManager] 动态创建 AI_Manager");
    }
    
    gameEnded = false;
}

// 新增方法
private void DeferredFogRefresh()
{
    fogOfWarManager?.RefreshFog();
    GD.Print($"[GameManager] 延迟刷新战争迷雾完成");
}
    // GameManager.cs - 在类顶部添加
// 已移除：三连击/右键切换回合逻辑，改用独立 UI 按钮

public override void _Input(InputEvent @event)
{
    var terrainEditor = GetTree().GetFirstNodeInGroup("terrain_editor") as TerrainEditor;
    if (terrainEditor != null && terrainEditor.ShouldBlockUnitOperations())
        return;

    if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
    {
        if (productionMenuPanel != null && productionMenuPanel.Visible)
        {
            HideProductionMenu();
            GetViewport().SetInputAsHandled();
            return;
        }
    }

    if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
    {
        // 左键/右键不再用于切换回合，使用专用 UI 按钮
    }
}

    private void OnRestartPressed()
{
     GetViewport().SetInputAsHandled();
    RestartGame();
}
    private void DeferredInit()
    {
        // 1. 获取UI引用（从Main.tscn）
        var mainScene = GetTree().CurrentScene;
        turnEndLabel = mainScene.GetNodeOrNull<Label>("TurnEndLabel");
        TurnLabel = mainScene.GetNodeOrNull<Label>("TurnLabel");
        UnitLists = mainScene.GetNodeOrNull<RichTextLabel>("UnitLists");

        // 2. 初始化GridManager
        gridManager?.Init();

        // 3. 初始化WeaponManager（如果存在）
        if (weaponManager != null)
        {
            weaponManager.RefreshWeaponList();

        }

        // 4. 初始化UnitManager（如果存在）
        if (unitManager != null)
        {
            unitManager.RefreshUnitList();

        }

        // 5. 更新UI
        UpdateTurnLabel();
        UpdateUnitLists();

        // ✅ 新增：创建缩放滑块和统计面板收缩按钮
        SetupZoomSlider();
        SetupUnitListsToggle();
    }


public void OnSelectWeapon(Weapon weapon)
{
    if (weapon == null || weapon.hasActed) return;
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
    public void DeferredUnitInit()
    {
        if (unitManager == null) return;

        // 强制刷新单位列表
        unitManager.RefreshUnitList();

        // ✅ 新增：从 AllUnits 中收集所有兵种到专用列表
        RefreshSpecializedUnitLists();

        foreach (var unit in unitManager.AllUnits)
        {
            // 确保绑定到格子
            if (unit.grid == null)
            {
                unitManager.BindUnitToGrid(unit, true);
            }

            // 设置点击回调（Area2D 信号在 Infantry._Ready() 中已连接）
            unit.OnClickPiece = OnSelectPiece;
        }

        // ✅ 初始化后更新统计
        UpdateUnitLists();
    }

    // ✅ 新增：刷新所有专用单位列表
    public void RefreshSpecializedUnitLists()
    {
        // 清空所有列表
        infantryUnits.Clear();
        mechUnits.Clear();
        ooziumUnits.Clear();
        tankUnits.Clear();
        mdTankUnits.Clear();
        vehicleUnits.Clear();
        artilleryUnits.Clear();
        antiairUnits.Clear();
        antiTankUnits.Clear();
        apcUnits.Clear();
        reconUnits.Clear();

        foreach (var unit in unitManager.AllUnits)
        {
            // 通过字典查询分类
            if (unitCategories.TryGetValue(unit, out var category))
            {
                switch (category)
                {
                    case UnitCategory.Infantry:
                        infantryUnits.Add(unit);
                        break;
                    case UnitCategory.Mech:
                        mechUnits.Add(unit);
                        break;
                    case UnitCategory.Oozium:
                        ooziumUnits.Add(unit);
                        break;
                    case UnitCategory.Tank:
                        tankUnits.Add(unit);
                        break;
                    case UnitCategory.MdTank:
                        mdTankUnits.Add(unit);
                        break;
                    case UnitCategory.Vehicle:
                        vehicleUnits.Add(unit);
                        break;
                    case UnitCategory.APC:  // ✅ APC分类
                        apcUnits.Add(unit);
                        break;
                    case UnitCategory.Recon:
                        reconUnits.Add(unit);
                        break;
                    case UnitCategory.AntiAir:
                        antiairUnits.Add(unit);
                        break;
                    case UnitCategory.AntiTank:
                        antiTankUnits.Add(unit);
                        break;
                    case UnitCategory.Artillery:
                        artilleryUnits.Add(unit);
                        break;
                    case UnitCategory.Rocket:
                        artilleryUnits.Add(unit);
                        break;
                    case UnitCategory.FlyBomb:
                        flyBombUnits.Add(unit);
                        break;
                }
            }
            else
            {
                // ✅ 自动识别未分类单位
                AutoCategorizeUnit(unit);
            }
        }

    }

    // ✅ 新增：自动识别单位类型
// GameManager.cs - 修复后的 AutoCategorizeUnit 方法
// GameManager.cs - AutoCategorizeUnit 方法
private void AutoCategorizeUnit(Infantry unit)
{
    UnitCategory category;

    switch (unit)
    {
        case APC _:         // ✅ 添加 APC 检查（必须在 Infantry 之前）
            category = UnitCategory.APC;
            apcUnits.Add(unit);
            GD.Print($"[AutoCategorizeUnit] {unit.Name} 识别为 APC");
            break;

        case Recon _:
            category = UnitCategory.Recon;
            reconUnits.Add(unit);
            GD.Print($"[AutoCategorizeUnit] {unit.Name} 识别为 Recon");
            break;

        case Mech _:
            category = UnitCategory.Mech;
            mechUnits.Add(unit);
            GD.Print($"[AutoCategorizeUnit] {unit.Name} 识别为 Mech");
            break;

        case Oozium _:
            category = UnitCategory.Oozium;
            ooziumUnits.Add(unit);
            GD.Print($"[AutoCategorizeUnit] {unit.Name} 识别为 Oozium");
            break;

        case MdTank _:
            category = UnitCategory.MdTank;
            mdTankUnits.Add(unit);
            GD.Print($"[AutoCategorizeUnit] {unit.Name} 识别为 MdTank");
            break;

        case LightTank _:
            category = UnitCategory.Tank;
            tankUnits.Add(unit);
            GD.Print($"[AutoCategorizeUnit] {unit.Name} 识别为 Tank");
            break;

        case Rocket _:
            category = UnitCategory.Rocket;  // 归入火炮类，或新建 Rocket 分类
            artilleryUnits.Add(unit);
            GD.Print($"[AutoCategorizeUnit] {unit.Name} 识别为 Rocket");
            break;
        case FlyBomb _:
            category = UnitCategory.FlyBomb;
            flyBombUnits.Add(unit);
            GD.Print($"[AutoCategorizeUnit] {unit.Name} 识别为 FlyBomb");
            break;
        case Flare _:
            category = UnitCategory.Vehicle;
            flareUnits.Add(unit);
            GD.Print($"[AutoCategorizeUnit] {unit.Name} 识别为 Flare");
            break;
        case Bike _:
            category = UnitCategory.Infantry;
            bikeUnits.Add(unit);
            GD.Print($"[AutoCategorizeUnit] {unit.Name} 识别为 Bike");
            break;
        case Artillery _:
            category = UnitCategory.Artillery;
            artilleryUnits.Add(unit);
            GD.Print($"[AutoCategorizeUnit] {unit.Name} 识别为 Artillery");
            break;
        case AntiAir _:         // ✅ 添加 APC 检查（必须在 Infantry 之前）
            category = UnitCategory.AntiAir;
            antiairUnits.Add(unit);
            GD.Print($"[AutoCategorizeUnit] {unit.Name} 识别为 AntiAir");
            break;

        case AntiTank _:
            category = UnitCategory.AntiTank;
            antiTankUnits.Add(unit);
            GD.Print($"[AutoCategorizeUnit] {unit.Name} 识别为 AntiTank");
            break;

        case Infantry _:
            category = UnitCategory.Infantry;
            infantryUnits.Add(unit);
            GD.Print($"[AutoCategorizeUnit] {unit.Name} 识别为 Infantry");
            break;

        default:
            category = UnitCategory.Other;
            GD.PrintErr($"[AutoCategorizeUnit] {unit.Name} 无法识别类型！");
            break;
    }

    unitCategories[unit] = category;
}



    // ✅ 创建统计面板切换按钮
    private void CreateStatsToggleButtons()
    {
        if (UnitLists == null) return;
        
        var canvasLayer = GetTree().CurrentScene.GetNodeOrNull<CanvasLayer>("CanvasLayer");
        if (canvasLayer == null) return;
        
        // 5个按钮排成一排（价值、单位、设施、资金、统计）
        // 每个按钮宽80px，统计按钮宽60px
        
        // 价值按钮
        valueStatsBtn = new Button();
        valueStatsBtn.Name = "ValueStatsBtn";
        valueStatsBtn.Text = "💎 价值";
        valueStatsBtn.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        valueStatsBtn.OffsetLeft = -400;
        valueStatsBtn.OffsetTop = 0;
        valueStatsBtn.OffsetRight = -320;
        valueStatsBtn.OffsetBottom = 32;
        valueStatsBtn.AddThemeFontSizeOverride("font_size", 14);
        valueStatsBtn.AddThemeColorOverride("font_color", Colors.White);
        var vStyle = new StyleBoxFlat();
        vStyle.BgColor = new Color(0.7f, 0.4f, 0.6f, 0.9f);
        vStyle.SetCornerRadiusAll(8);
        valueStatsBtn.AddThemeStyleboxOverride("normal", vStyle);
        var vStyleHover = new StyleBoxFlat();
        vStyleHover.BgColor = new Color(0.8f, 0.5f, 0.7f, 0.95f);
        vStyleHover.SetCornerRadiusAll(8);
        valueStatsBtn.AddThemeStyleboxOverride("hover", vStyleHover);
        valueStatsBtn.ZIndex = 200;
        valueStatsBtn.Pressed += () => { statsMode = StatsMode.Value; UpdateUnitLists(); };
        canvasLayer.AddChild(valueStatsBtn);
        
        // 单位按钮
        unitStatsBtn = new Button();
        unitStatsBtn.Name = "UnitStatsBtn";
        unitStatsBtn.Text = "📊 单位";
        unitStatsBtn.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        unitStatsBtn.OffsetLeft = -320;
        unitStatsBtn.OffsetTop = 0;
        unitStatsBtn.OffsetRight = -240;
        unitStatsBtn.OffsetBottom = 32;
        unitStatsBtn.AddThemeFontSizeOverride("font_size", 14);
        unitStatsBtn.AddThemeColorOverride("font_color", Colors.White);
        var uStyle = new StyleBoxFlat();
        uStyle.BgColor = new Color(0.3f, 0.5f, 0.7f, 0.9f);
        uStyle.SetCornerRadiusAll(8);
        unitStatsBtn.AddThemeStyleboxOverride("normal", uStyle);
        var uStyleHover = new StyleBoxFlat();
        uStyleHover.BgColor = new Color(0.4f, 0.6f, 0.8f, 0.95f);
        uStyleHover.SetCornerRadiusAll(8);
        unitStatsBtn.AddThemeStyleboxOverride("hover", uStyleHover);
        unitStatsBtn.ZIndex = 200;
        unitStatsBtn.Pressed += () => { statsMode = StatsMode.Units; UpdateUnitLists(); };
        canvasLayer.AddChild(unitStatsBtn);
        
        // 设施按钮
        facilityStatsBtn = new Button();
        facilityStatsBtn.Name = "FacilityStatsBtn";
        facilityStatsBtn.Text = "🏙️ 设施";
        facilityStatsBtn.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        facilityStatsBtn.OffsetLeft = -240;
        facilityStatsBtn.OffsetTop = 0;
        facilityStatsBtn.OffsetRight = -160;
        facilityStatsBtn.OffsetBottom = 32;
        facilityStatsBtn.AddThemeFontSizeOverride("font_size", 14);
        facilityStatsBtn.AddThemeColorOverride("font_color", Colors.White);
        var fStyle = new StyleBoxFlat();
        fStyle.BgColor = new Color(0.6f, 0.5f, 0.3f, 0.9f);
        fStyle.SetCornerRadiusAll(8);
        facilityStatsBtn.AddThemeStyleboxOverride("normal", fStyle);
        var fStyleHover = new StyleBoxFlat();
        fStyleHover.BgColor = new Color(0.7f, 0.6f, 0.4f, 0.95f);
        fStyleHover.SetCornerRadiusAll(8);
        facilityStatsBtn.AddThemeStyleboxOverride("hover", fStyleHover);
        facilityStatsBtn.ZIndex = 200;
        facilityStatsBtn.Pressed += () => { statsMode = StatsMode.Facilities; UpdateUnitLists(); };
        canvasLayer.AddChild(facilityStatsBtn);
        
        // 资金按钮
        fundsStatsBtn = new Button();
        fundsStatsBtn.Name = "FundsStatsBtn";
        fundsStatsBtn.Text = "💰 资金";
        fundsStatsBtn.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        fundsStatsBtn.OffsetLeft = -160;
        fundsStatsBtn.OffsetTop = 0;
        fundsStatsBtn.OffsetRight = -80;
        fundsStatsBtn.OffsetBottom = 32;
        fundsStatsBtn.AddThemeFontSizeOverride("font_size", 14);
        fundsStatsBtn.AddThemeColorOverride("font_color", Colors.White);
        var mStyle = new StyleBoxFlat();
        mStyle.BgColor = new Color(0.3f, 0.6f, 0.4f, 0.9f);
        mStyle.SetCornerRadiusAll(8);
        fundsStatsBtn.AddThemeStyleboxOverride("normal", mStyle);
        var mStyleHover = new StyleBoxFlat();
        mStyleHover.BgColor = new Color(0.4f, 0.7f, 0.5f, 0.95f);
        mStyleHover.SetCornerRadiusAll(8);
        fundsStatsBtn.AddThemeStyleboxOverride("hover", mStyleHover);
        fundsStatsBtn.ZIndex = 200;
        fundsStatsBtn.Pressed += () => { statsMode = StatsMode.Funds; UpdateUnitLists(); };
        canvasLayer.AddChild(fundsStatsBtn);

        // ✅ 切换回合按钮（替代右键/三连击）
        nextPhaseButton = new Button();
        nextPhaseButton.Name = "NextPhaseButton";
        nextPhaseButton.Text = "🔄 切换回合";
        nextPhaseButton.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        nextPhaseButton.OffsetLeft = -400;
        nextPhaseButton.OffsetTop = 36;
        nextPhaseButton.OffsetRight = -240;
        nextPhaseButton.OffsetBottom = 72;
        nextPhaseButton.AddThemeFontSizeOverride("font_size", 16);
        nextPhaseButton.AddThemeColorOverride("font_color", Colors.White);
        var npStyle = new StyleBoxFlat();
        npStyle.BgColor = new Color(0.9f, 0.3f, 0.2f, 0.9f);
        npStyle.SetCornerRadiusAll(8);
        nextPhaseButton.AddThemeStyleboxOverride("normal", npStyle);
        var npStyleHover = new StyleBoxFlat();
        npStyleHover.BgColor = new Color(1.0f, 0.4f, 0.3f, 0.95f);
        npStyleHover.SetCornerRadiusAll(8);
        nextPhaseButton.AddThemeStyleboxOverride("hover", npStyleHover);
        nextPhaseButton.ZIndex = 200;
        nextPhaseButton.Pressed += () => { GD.Print("🔄 点击切换回合按钮"); NextPhase(); };
        canvasLayer.AddChild(nextPhaseButton);
    }
    public void UpdateUnitLists()
    {
        if (UnitLists == null) return;
        switch (statsMode)
        {
            case StatsMode.Facilities:
                UpdateFacilityLists();
                return;
            case StatsMode.Funds:
                UpdateFundsLists();
                return;
            case StatsMode.Value:
                UpdateValueLists();
                return;
        }

        // ✅ 直接从源头实时计算，不依赖维护的列表
        var all = unitManager?.AllUnits ?? new List<Infantry>();
        
        // 四队伍计数器: P-1(Player), P0(Player0), P1(Player1), P2(Player2)
        int pn1Infantry = 0, p0Infantry = 0, p1Infantry = 0, p2Infantry = 0;
        int pn1Mech = 0, p0Mech = 0, p1Mech = 0, p2Mech = 0;
        int pn1Oozium = 0, p0Oozium = 0, p1Oozium = 0, p2Oozium = 0;
        int pn1Tank = 0, p0Tank = 0, p1Tank = 0, p2Tank = 0;
        int pn1MdTank = 0, p0MdTank = 0, p1MdTank = 0, p2MdTank = 0;
        int pn1Artillery = 0, p0Artillery = 0, p1Artillery = 0, p2Artillery = 0;
        int pn1Rocket = 0, p0Rocket = 0, p1Rocket = 0, p2Rocket = 0;
        int pn1APC = 0, p0APC = 0, p1APC = 0, p2APC = 0;
        int pn1Recon = 0, p0Recon = 0, p1Recon = 0, p2Recon = 0;
        int pn1AntiAir = 0, p0AntiAir = 0, p1AntiAir = 0, p2AntiAir = 0;
        int pn1AntiTank = 0, p0AntiTank = 0, p1AntiTank = 0, p2AntiTank = 0;
        int pn1FlyBomb = 0, p0FlyBomb = 0, p1FlyBomb = 0, p2FlyBomb = 0;
        int pn1Flare = 0, p0Flare = 0, p1Flare = 0, p2Flare = 0;
        int pn1Bike = 0, p0Bike = 0, p1Bike = 0, p2Bike = 0;

        foreach (var u in all)
        {
            if (u == null || !IsInstanceValid(u)) continue;
            string team = u.team;

            if (u is Mech) 
            { 
                if (team == "Player") pn1Mech++; else if (team == "Player0") p0Mech++;
                else if (team == "Player1") p1Mech++; else if (team == "Player2") p2Mech++;
            }
            else if (u is Oozium) 
            { 
                if (team == "Player") pn1Oozium++; else if (team == "Player0") p0Oozium++;
                else if (team == "Player1") p1Oozium++; else if (team == "Player2") p2Oozium++;
            }
            else if (u is FlyBomb) 
            { 
                if (team == "Player") pn1FlyBomb++; else if (team == "Player0") p0FlyBomb++;
                else if (team == "Player1") p1FlyBomb++; else if (team == "Player2") p2FlyBomb++;
            }
            else if (u is Flare) 
            { 
                if (team == "Player") pn1Flare++; else if (team == "Player0") p0Flare++;
                else if (team == "Player1") p1Flare++; else if (team == "Player2") p2Flare++;
            }
            else if (u is Bike) 
            { 
                if (team == "Player") pn1Bike++; else if (team == "Player0") p0Bike++;
                else if (team == "Player1") p1Bike++; else if (team == "Player2") p2Bike++;
            }
            else if (u is LightTank) 
            { 
                if (team == "Player") pn1Tank++; else if (team == "Player0") p0Tank++;
                else if (team == "Player1") p1Tank++; else if (team == "Player2") p2Tank++;
            }
            else if (u is MdTank) 
            { 
                if (team == "Player") pn1MdTank++; else if (team == "Player0") p0MdTank++;
                else if (team == "Player1") p1MdTank++; else if (team == "Player2") p2MdTank++;
            }
            else if (u is APC) 
            { 
                if (team == "Player") pn1APC++; else if (team == "Player0") p0APC++;
                else if (team == "Player1") p1APC++; else if (team == "Player2") p2APC++;
            }
            else if (u is Recon) 
            { 
                if (team == "Player") pn1Recon++; else if (team == "Player0") p0Recon++;
                else if (team == "Player1") p1Recon++; else if (team == "Player2") p2Recon++;
            }
            else if (u is Artillery) 
            { 
                if (team == "Player") pn1Artillery++; else if (team == "Player0") p0Artillery++;
                else if (team == "Player1") p1Artillery++; else if (team == "Player2") p2Artillery++;
            }
            else if (u is Rocket) 
            { 
                if (team == "Player") pn1Rocket++; else if (team == "Player0") p0Rocket++;
                else if (team == "Player1") p1Rocket++; else if (team == "Player2") p2Rocket++;
            }
            else if (u is AntiAir) 
            { 
                if (team == "Player") pn1AntiAir++; else if (team == "Player0") p0AntiAir++;
                else if (team == "Player1") p1AntiAir++; else if (team == "Player2") p2AntiAir++;
            }
            else if (u is AntiTank) 
            { 
                if (team == "Player") pn1AntiTank++; else if (team == "Player0") p0AntiTank++;
                else if (team == "Player1") p1AntiTank++; else if (team == "Player2") p2AntiTank++;
            }
            else if (u is Infantry) 
            { 
                if (team == "Player") pn1Infantry++; else if (team == "Player0") p0Infantry++;
                else if (team == "Player1") p1Infantry++; else if (team == "Player2") p2Infantry++;
            }
        }

        // 兵器统计 - 四队伍
        int pn1Weapons = 0, p0Weapons = 0, p1Weapons = 0, p2Weapons = 0;
        int pn1Cannons = 0, p0Cannons = 0, p1Cannons = 0, p2Cannons = 0;
        int pn1Lasers = 0, p0Lasers = 0, p1Lasers = 0, p2Lasers = 0;
        if (weaponManager != null)
        {
            foreach (var w in weaponManager.AllWeapons.ToList())
            {
                if (w == null || !IsInstanceValid(w)) continue;
                string team = w.team;
                if (w is BlackCannon) 
                { 
                    if (team == "Player") pn1Cannons++; else if (team == "Player0") p0Cannons++;
                    else if (team == "Player1") p1Cannons++; else if (team == "Player2") p2Cannons++;
                }
                else if (w is Laser) 
                { 
                    if (team == "Player") pn1Lasers++; else if (team == "Player0") p0Lasers++;
                    else if (team == "Player1") p1Lasers++; else if (team == "Player2") p2Lasers++;
                }
                else 
                { 
                    if (team == "Player") pn1Weapons++; else if (team == "Player0") p0Weapons++;
                    else if (team == "Player1") p1Weapons++; else if (team == "Player2") p2Weapons++;
                }
            }
        }

        int totalUnits = all.Count(u => u != null && IsInstanceValid(u));
        int totalWeapons = weaponManager?.AllWeapons.Count(w => w != null && IsInstanceValid(w)) ?? 0;

        // 颜色定义
        string cP1 = "#ff4d4d";   // 红
        string cP2 = "#4d80ff";   // 蓝
        string cP0 = "#b3b3b3";   // 灰
        string cPN1 = "#b366ff";  // 紫
        string cTitle = "#ffaa33"; // 橙黄

        string text = $" [color={cTitle}]⚔ 单位统计[/color] \n";
        text += $"  [color={cPN1}]🟣P-1[/color]  [color={cP0}]⚪P0[/color]  [color={cP1}]🔴P1[/color]  [color={cP2}]🔵P2[/color]\n";
        text += $"步 [color={cPN1}]{pn1Infantry,2}|[color={cP0}]{p0Infantry,2}|[color={cP1}]{p1Infantry,2}|[color={cP2}]{p2Infantry,2}[/color]\n";
        text += $"机 [color={cPN1}]{pn1Mech,2}|[color={cP0}]{p0Mech,2}|[color={cP1}]{p1Mech,2}|[color={cP2}]{p2Mech,2}[/color]\n";
        text += $"O [color={cPN1}]{pn1Oozium,2}|[color={cP0}]{p0Oozium,2}[color={cP1}]|{p1Oozium,2}|[color={cP2}]{p2Oozium,2}[/color]\n";

        if (pn1Tank > 0 || p0Tank > 0 || p1Tank > 0 || p2Tank > 0)
            text += $"轻 [color={cPN1}]{pn1Tank,2}|[color={cP0}]{p0Tank,2}|[color={cP1}]{p1Tank,2}|[color={cP2}]{p2Tank,2}[/color]\n";

        if (pn1MdTank > 0 || p0MdTank > 0 || p1MdTank > 0 || p2MdTank > 0)
            text += $"重 [color={cPN1}]{pn1MdTank,2}|[color={cP0}]{p0MdTank,2}|[color={cP1}]{p1MdTank,2}|[color={cP2}]{p2MdTank,2}[/color]\n";

        if (pn1APC > 0 || p0APC > 0 || p1APC > 0 || p2APC > 0)
            text += $"运 [color={cPN1}]{pn1APC,2}|[color={cP0}]{p0APC,2}|[color={cP1}]{p1APC,2}|[color={cP2}]{p2APC,2}[/color]\n";

        if (pn1Recon > 0 || p0Recon > 0 || p1Recon > 0 || p2Recon > 0)
            text += $"侦 [color={cPN1}]{pn1Recon,2}|[color={cP0}]{p0Recon,2}|[color={cP1}]{p1Recon,2}|[color={cP2}]{p2Recon,2}[/color]\n";

        if (pn1Artillery > 0 || p0Artillery > 0 || p1Artillery > 0 || p2Artillery > 0)
            text += $"炮[color={cPN1}] {pn1Artillery,2}|[color={cP0}]{p0Artillery,2}|[color={cP1}]{p1Artillery,2}|[color={cP2}]{p2Artillery,2}[/color]\n";

        if (pn1Rocket > 0 || p0Rocket > 0 || p1Rocket > 0 || p2Rocket > 0)
            text += $"箭[color={cPN1}] {pn1Rocket,2}|[color={cP0}]{p0Rocket,2}|[color={cP1}]{p1Rocket,2}|[color={cP2}]{p2Rocket,2}[/color]\n";

        if (pn1AntiAir > 0 || p0AntiAir > 0 || p1AntiAir > 0 || p2AntiAir > 0)
            text += $"防[color={cPN1}] {pn1AntiAir,2}|[color={cP0}]{p0AntiAir,2}|[color={cP1}]{p1AntiAir,2}|[color={cP2}]{p2AntiAir,2}[/color]\n";

        if (pn1AntiTank > 0 || p0AntiTank > 0 || p1AntiTank > 0 || p2AntiTank > 0)
            text += $"反[color={cPN1}] {pn1AntiTank,2}|[color={cP0}]{p0AntiTank,2}|[color={cP1}]{p1AntiTank,2}|[color={cP2}]{p2AntiTank,2}[/color]\n";

        if (pn1FlyBomb > 0 || p0FlyBomb > 0 || p1FlyBomb > 0 || p2FlyBomb > 0)
            text += $"弹 [color={cPN1}]{pn1FlyBomb,2}|[color={cP0}]{p0FlyBomb,2}|[color={cP1}]{p1FlyBomb,2}|[color={cP2}]{p2FlyBomb,2}[/color]\n";

        if (pn1Flare > 0 || p0Flare > 0 || p1Flare > 0 || p2Flare > 0)
            text += $"照 [color={cPN1}]{pn1Flare,2}|[color={cP0}]{p0Flare,2}|[color={cP1}]{p1Flare,2}|[color={cP2}]{p2Flare,2}[/color]\n";

        if (pn1Bike > 0 || p0Bike > 0 || p1Bike > 0 || p2Bike > 0)
            text += $"摩[color={cPN1}] {pn1Bike,2}|[color={cP0}]{p0Bike,2}|[color={cP1}]{p1Bike,2}|[color={cP2}]{p2Bike,2}[/color]\n";

        // 兵器统计
        bool hasAnyWeapon = pn1Cannons > 0 || p0Cannons > 0 || p1Cannons > 0 || p2Cannons > 0
                         || pn1Lasers > 0 || p0Lasers > 0 || p1Lasers > 0 || p2Lasers > 0
                         || pn1Weapons > 0 || p0Weapons > 0 || p1Weapons > 0 || p2Weapons > 0;
        
        if (hasAnyWeapon)
        {
            text += $"\n[color={cTitle}]===兵器===[/color]\n";
            if (pn1Cannons > 0 || p0Cannons > 0 || p1Cannons > 0 || p2Cannons > 0)
                text += $"炮 [color={cPN1}]{pn1Cannons,2}|[color={cP0}]{p0Cannons,2}|[color={cP1}]{p1Cannons,2}|[color={cP2}]{p2Cannons,2}[/color]\n";
            if (pn1Lasers > 0 || p0Lasers > 0 || p1Lasers > 0 || p2Lasers > 0)
                text += $"激[color={cPN1}] {pn1Lasers,2}|[color={cP0}]{p0Lasers,2}|[color={cP1}]{p1Lasers,2}|[color={cP2}]{p2Lasers,2}[/color]\n";
            if (pn1Weapons > 0 || p0Weapons > 0 || p1Weapons > 0 || p2Weapons > 0)
                text += $"其[color={cPN1}] {pn1Weapons,2}|[color={cP0}]{p0Weapons,2}|[color={cP1}]{p1Weapons,2}|[color={cP2}]{p2Weapons,2}[/color]\n";
        }


        int totalAll = totalUnits + totalWeapons;

        int pn1TotalUnits = pn1Infantry + pn1Mech + pn1Oozium + pn1Tank + pn1MdTank + pn1Artillery + pn1Rocket + pn1APC + pn1Recon + pn1AntiAir + pn1AntiTank + pn1FlyBomb + pn1Flare + pn1Bike;
        int p0TotalUnits = p0Infantry + p0Mech + p0Oozium + p0Tank + p0MdTank + p0Artillery + p0Rocket + p0APC + p0Recon + p0AntiAir + p0AntiTank + p0FlyBomb + p0Flare + p0Bike;
        int p1TotalUnits = p1Infantry + p1Mech + p1Oozium + p1Tank + p1MdTank + p1Artillery + p1Rocket + p1APC + p1Recon + p1AntiAir + p1AntiTank + p1FlyBomb + p1Flare + p1Bike;
        int p2TotalUnits = p2Infantry + p2Mech + p2Oozium + p2Tank + p2MdTank + p2Artillery + p2Rocket + p2APC + p2Recon + p2AntiAir + p2AntiTank + p2FlyBomb + p2Flare + p2Bike;

        int pn1TotalWeapons = pn1Cannons + pn1Lasers + pn1Weapons;
        int p0TotalWeapons = p0Cannons + p0Lasers + p0Weapons;
        int p1TotalWeapons = p1Cannons + p1Lasers + p1Weapons;
        int p2TotalWeapons = p2Cannons + p2Lasers + p2Weapons;

        int pn1TotalAll = pn1TotalUnits + pn1TotalWeapons;
        int p0TotalAll = p0TotalUnits + p0TotalWeapons;
        int p1TotalAll = p1TotalUnits + p1TotalWeapons;
        int p2TotalAll = p2TotalUnits + p2TotalWeapons;

        text += $"\n[color={cTitle}]===总计===[/color]\n";
        text += $"单 {pn1TotalUnits,2}|{p0TotalUnits,2}|{p1TotalUnits,2}|{p2TotalUnits,2}\n";
        if (totalWeapons > 0)
            text += $"兵 {pn1TotalWeapons,2}|{p0TotalWeapons,2}|{p1TotalWeapons,2}|{p2TotalWeapons,2}\n";
        text += $"共 {pn1TotalAll,2}|{p0TotalAll,2}|{p1TotalAll,2}|{p2TotalAll,2}\n";

        UnitLists.Text = text;
    }
    public void UpdateFacilityLists()
    {
        if (UnitLists == null) return;
        if (gridManager == null) return;
        
        var cities = gridManager.GetAllCities();
        int p0Cities = 0, p1Cities = 0, p2Cities = 0, pN1Cities = 0;
        
        foreach (var city in cities)
        {
            if (city == null) continue;
            switch (city.facilityTeam)
            {
                case "Player0": p0Cities++; break;
                case "Player1": p1Cities++; break;
                case "Player2": p2Cities++; break;
                case "Player": pN1Cities++; break;
            }
        }
        
        string text = " [color=yellow]设施统计[/color] \n";
        text += $"[color=gray]中立(P0): {p0Cities}[/color]\n";
        text += $"[color=red]🔴P1: {p1Cities}[/color]\n";
        text += $"[color=blue]🔵P2: {p2Cities}[/color]\n";
        if (pN1Cities > 0)
            text += $"[color=orange]P-1: {pN1Cities}[/color]\n";
        text += $"总计: {cities.Count} 设施";
        
        UnitLists.Text = text;
    }
    public void UpdateFundsLists()
    {
        if (UnitLists == null) return;
        
        // 计算P1/P2每回合收入
        int p1Income = 0, p2Income = 0;
        if (gridManager?.grids != null)
        {
            foreach (var grid in gridManager.grids)
            {
                if (grid?.city == null) continue;
                if (grid.city is City c)
                {
                    if (grid.city.facilityTeam == "Player1")
                        p1Income += c.fundsPerTurn;
                    else if (grid.city.facilityTeam == "Player2")
                        p2Income += c.fundsPerTurn;
                }
            }
        }
        
        string cP1 = "#ff4d4d";
        string cP2 = "#4d80ff";
        string cTitle = "#ffaa33";
        
        string text = $" [color={cTitle}]💰 资金统计[/color] \n\n";
        text += $"[color={cP1}]🔴 P1 资金[/color]\n";
        text += $"  现有: {p1Funds} G\n";
        text += $"  收入: +{p1Income} G/回合\n";
        text += $"  上限: {p1FundsMax} G\n\n";
        text += $"[color={cP2}]🔵 P2 资金[/color]\n";
        text += $"  现有: {p2Funds} G\n";
        text += $"  收入: +{p2Income} G/回合\n";
        text += $"  上限: {p2FundsMax} G\n";
        
        UnitLists.Text = text;
    }
    
    public void UpdateValueLists()
    {
        if (UnitLists == null) return;
        
        long pn1Funds = 0, p0Funds = 0, p1Funds = 0, p2Funds = 0;
        
        // 统计单位造价
        var all = unitManager?.AllUnits ?? new List<Infantry>();
        foreach (var u in all)
        {
            if (u == null || !IsInstanceValid(u)) continue;
            switch (u.team)
            {
                case "Player": pn1Funds += u.cost; break;
                case "Player0": p0Funds += u.cost; break;
                case "Player1": p1Funds += u.cost; break;
                case "Player2": p2Funds += u.cost; break;
            }
        }
        
        // 统计兵器造价
        if (weaponManager != null)
        {
            foreach (var w in weaponManager.AllWeapons.ToList())
            {
                if (w == null || !IsInstanceValid(w)) continue;
                switch (w.team)
                {
                    case "Player": pn1Funds += w.cost; break;
                    case "Player0": p0Funds += w.cost; break;
                    case "Player1": p1Funds += w.cost; break;
                    case "Player2": p2Funds += w.cost; break;
                }
            }
        }
        
        long totalFunds = pn1Funds + p0Funds + p1Funds + p2Funds;
        
        string cP1 = "#ff4d4d";
        string cP2 = "#4d80ff";
        string cP0 = "#b3b3b3";
        string cPN1 = "#b366ff";
        string cTitle = "#ffaa33";
        
        string text = $" [color={cTitle}]💎 单位价值统计[/color] \n";
        text += $"  [color={cPN1}]🟣P-1[/color]  [color={cP0}]⚪P0[/color]  [color={cP1}]🔴P1[/color]  [color={cP2}]🔵P2[/color]\n";
        text += $"💵 [color={cPN1}]{pn1Funds,7}|[color={cP0}]{p0Funds,7}|[color={cP1}]{p1Funds,7}|[color={cP2}]{p2Funds,7}[/color]\n";
        text += $"\n[color={cTitle}]===总计===[/color]\n";
        text += $"💰 {totalFunds} G\n";
        
        UnitLists.Text = text;
    }


    // ========== ✅ 新增：缩放滑块 ==========
    private void SetupZoomSlider()
    {
        var mainScene = GetTree().CurrentScene;
        var canvas = mainScene.GetNodeOrNull<CanvasLayer>("CanvasLayer");
        if (canvas == null) return;

        // 获取或查找Camera
        cameraController = mainScene.GetNodeOrNull<CameraTouchController>("CameraController");
        if (cameraController == null) return;

        // 创建滑块容器
        var sliderPanel = new Control();
        sliderPanel.Name = "ZoomSliderPanel";
        sliderPanel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        sliderPanel.OffsetLeft = -80;
        sliderPanel.OffsetTop = 200;
        sliderPanel.OffsetRight = 0;
        sliderPanel.OffsetBottom = 500;
        sliderPanel.CustomMinimumSize = new Godot.Vector2(60, 300);
        canvas.AddChild(sliderPanel);

        // 背景
        var bg = new ColorRect();
        bg.Name = "ZoomBg";
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0, 0, 0, 0.6f);
        sliderPanel.AddChild(bg);

        // 标题
        var title = new Label();
        title.Name = "ZoomTitle";
        title.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        title.Text = "缩放";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeColorOverride("font_color", Colors.White);
        title.AddThemeFontSizeOverride("font_size", 14);
        title.CustomMinimumSize = new Godot.Vector2(0, 20);
        sliderPanel.AddChild(title);

        // 大圆（可拖动区域）
        var knob = new Button();
        knob.Name = "ZoomKnob";
        knob.SetAnchorsPreset(Control.LayoutPreset.Center);
        knob.CustomMinimumSize = new Godot.Vector2(48, 48);
        knob.Size = new Godot.Vector2(48, 48);
        knob.Text = "●";
        knob.AddThemeFontSizeOverride("font_size", 28);
        knob.AddThemeColorOverride("font_color", new Color(1, 0.8f, 0.2f));
        var knobStyle = new StyleBoxFlat();
        knobStyle.BgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        knobStyle.SetCornerRadiusAll(24);
        knob.AddThemeStyleboxOverride("normal", knobStyle);
        sliderPanel.AddChild(knob);

        // 百分比标签
        var percentLabel = new Label();
        percentLabel.Name = "ZoomPercent";
        percentLabel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        percentLabel.Text = "100%";
        percentLabel.HorizontalAlignment = HorizontalAlignment.Center;
        percentLabel.AddThemeColorOverride("font_color", Colors.White);
        percentLabel.AddThemeFontSizeOverride("font_size", 12);
        percentLabel.CustomMinimumSize = new Godot.Vector2(0, 20);
        sliderPanel.AddChild(percentLabel);

        // 滑块逻辑：处理拖动
        bool isDraggingKnob = false;
        float knobStartY = 0;
        float zoomStartPercent = 0;

        knob.ButtonDown += () =>
        {
            isDraggingKnob = true;
            knobStartY = sliderPanel.GetGlobalMousePosition().Y;
            zoomStartPercent = cameraController.ZoomPercent;
        };

        knob.ButtonUp += () =>
        {
            isDraggingKnob = false;
        };

        // 使用 Godot 的 _Process 来更新
        var updateTimer = new Timer();
        updateTimer.WaitTime = 0.016f;
        updateTimer.OneShot = false;
        updateTimer.Timeout += () =>
        {
            if (!isDraggingKnob) return;

            float currentY = sliderPanel.GetGlobalMousePosition().Y;
            float deltaY = knobStartY - currentY; // 向上拖动 = 放大
            float deltaPercent = deltaY / 200f * 100f; // 200px 范围对应 100%
            float newPercent = Mathf.Clamp(zoomStartPercent + deltaPercent, 1f, 100f);

            cameraController.SetZoomPercent(newPercent);
            percentLabel.Text = $"{Mathf.RoundToInt(newPercent)}%";
        };
        sliderPanel.AddChild(updateTimer);
        updateTimer.Start();

        // 点击面板直接跳转
        sliderPanel.GuiInput += (@event) =>
        {
            if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
            {
                var localPos = sliderPanel.GetLocalMousePosition();
                float t = 1f - (localPos.Y / sliderPanel.Size.Y);
                t = Mathf.Clamp(t, 0f, 1f);
                float newPercent = t * 100f;
                cameraController.SetZoomPercent(newPercent);
                percentLabel.Text = $"{Mathf.RoundToInt(newPercent)}%";
            }
        };
    }

    // ========== ✅ 新增：统计面板收缩/展开 ==========
    private void SetupUnitListsToggle()
    {
        var mainScene = GetTree().CurrentScene;
        if (UnitLists == null) return;

        // 创建收缩按钮
        unitListsToggleBtn = new Button();
        unitListsToggleBtn.Name = "UnitListsToggleBtn";
        unitListsToggleBtn.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        unitListsToggleBtn.OffsetLeft = -80;
        unitListsToggleBtn.OffsetTop = 0;
        unitListsToggleBtn.OffsetRight = -20;
        unitListsToggleBtn.OffsetBottom = 32;
        unitListsToggleBtn.Text = "📊 统计";
        unitListsToggleBtn.AddThemeFontSizeOverride("font_size", 14);
        unitListsToggleBtn.AddThemeColorOverride("font_color", Colors.White);
        var btnStyle = new StyleBoxFlat();
        btnStyle.BgColor = new Color(0.2f, 0.4f, 0.6f, 0.9f);
        btnStyle.SetCornerRadiusAll(8);
        unitListsToggleBtn.AddThemeStyleboxOverride("normal", btnStyle);
        var btnStyleHover = new StyleBoxFlat();
        btnStyleHover.BgColor = new Color(0.3f, 0.5f, 0.7f, 0.9f);
        btnStyleHover.SetCornerRadiusAll(8);
        unitListsToggleBtn.AddThemeStyleboxOverride("hover", btnStyleHover);
        // ✅ 把按钮放进 CanvasLayer 中（作为 UI 层，不受相机缩放影响）
        var canvasLayer = mainScene.GetNodeOrNull<CanvasLayer>("CanvasLayer");
        if (canvasLayer != null)
        {
            canvasLayer.AddChild(unitListsToggleBtn);
        }
        else
        {
            mainScene.AddChild(unitListsToggleBtn);
        }

        // 点击切换展开/收缩
        unitListsToggleBtn.Pressed += ToggleUnitLists;

        // 初始状态：展开
        unitListsExpanded = true;
    }

    private void ToggleUnitLists()
    {
        unitListsExpanded = !unitListsExpanded;

        if (unitListsExpanded)
        {
            // 展开：显示面板
            UnitLists.Visible = true;
            unitListsToggleBtn.Text = "📊 统计";
        }
        else
        {
            // 收缩：隐藏面板
            UnitLists.Visible = false;
            unitListsToggleBtn.Text = "📊 统计";
        }
    }


    // ✅ 动态添加单位 - 传入分类参数
    public void AddNewUnit(PackedScene unitScene, Vector2I gridPos, string team, UnitCategory? category = null)
    {
        var newUnit = unitManager?.SpawnUnit(unitScene, gridPos, team);
        if (newUnit != null)
        {
            newUnit.OnClickPiece = OnSelectPiece;
            newUnit.isMoved = false;
            newUnit.isAttacked = false;

            // 自动推断分类（如果未指定）
            var actualCategory = category ?? InferCategory(newUnit);
            RegisterUnit(newUnit, actualCategory);

            // ✅ 更新统计
            UpdateUnitLists();
        }
    }

    private UnitCategory InferCategory(Infantry unit)
    {
        return unit switch
        {
            APC _ => UnitCategory.APC,
            Recon _ => UnitCategory.Recon,
            Mech _ => UnitCategory.Mech,
            Oozium _ => UnitCategory.Oozium,
            MdTank _ => UnitCategory.MdTank,
            LightTank _ => UnitCategory.Tank,
            Rocket _ => UnitCategory.Rocket,
            FlyBomb _ => UnitCategory.FlyBomb,
            Flare _ => UnitCategory.Vehicle,
            Bike _ => UnitCategory.Infantry,
            Artillery _ => UnitCategory.Artillery,
            AntiAir _ => UnitCategory.AntiAir,
            AntiTank _ => UnitCategory.AntiTank,
            Infantry _ => UnitCategory.Infantry,
            _ => UnitCategory.Other
        };
    }

    // 动态移除单位
       public void CheckVictoryCondition()
{
    if (gameEnded || blockVictoryCheck) return;

    // 获取所有存活单位
    var allUnits = unitManager?.AllUnits.Where(u => IsInstanceValid(u) && u.health > 0).ToList();
    if (allUnits == null) return;

    var p1Units = allUnits.Where(u => u.team == TeamHelper.Player1).ToList();
    var p2Units = allUnits.Where(u => u.team == TeamHelper.Player2).ToList();
    var p0Units = allUnits.Where(u => u.team == TeamHelper.Player0).ToList();
    var pN1Units = allUnits.Where(u => u.team == TeamHelper.Player).ToList();

    GD.Print($"[VictoryCheck] P1单位:{p1Units.Count} P2单位:{p2Units.Count}");

    // 判定胜利条件：某一方单位全灭
    if (p1Units.Count == 0 && p2Units.Count > 0)
    {
        // P2 获胜
        ShowVictory("Player2", "Player1");
    }
    else if (p2Units.Count == 0 && p1Units.Count > 0)
    {
        // P1 获胜
        ShowVictory("Player1", "Player2");
    }
    // 如果双方都全灭，可以视为平局或根据需求处理
    else if (p1Units.Count == 0 && p2Units.Count == 0)
    {
        ShowVictory("Draw", "Draw");  // 平局
    }
}
private void ShowVictory(string winner, string loser)
{
    gameEnded = true;

    if (victoryPanel == null) return;

    victoryPanel.Show();
    victoryPanel.ZIndex = 100;
    victoryPanel.SetAnchorsPreset(Control.LayoutPreset.FullRect); // 全屏

    // 背景
    var bg = victoryPanel.GetNodeOrNull<ColorRect>("Background");
    if (bg != null)
    {
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0, 0, 0, 0.9f);
    }

    // 获取 VBoxContainer
    var vbox = victoryPanel.GetNodeOrNull<VBoxContainer>("VBoxContainer");
    if (vbox != null)
    {
        // ✅ 关键修复：让容器在屏幕正中间
        vbox.SetAnchorsPreset(Control.LayoutPreset.Center); // 锚点设为屏幕中心

        // 设置容器大小（固定大小以便居中计算）
        vbox.CustomMinimumSize = new Godot.Vector2(600, 300);
        vbox.Size = new Godot.Vector2(600, 300);

        // 设置容器内的对齐方式（对于VBox，Alignment控制水平轴）
        vbox.Alignment = BoxContainer.AlignmentMode.Center; // 水平居中

        // 子元素间距
        vbox.AddThemeConstantOverride("separation", 30);

        // ✅ 关键：设置 grow 模式确保居中生效
        vbox.GrowHorizontal = Control.GrowDirection.Both;
        vbox.GrowVertical = Control.GrowDirection.Both;
    }

    // 获取节点
    var titleLabel = victoryTitleLabel ?? vbox?.GetNodeOrNull<RichTextLabel>("VictoryTitleLabel");
    var subLabel = victorySubtitleLabel ?? vbox?.GetNodeOrNull<RichTextLabel>("VictorySubtitleLabel");
    var btn = restartButton ?? vbox?.GetNodeOrNull<Button>("RestartButton");

    // ========== 标题 ==========
    if (titleLabel != null)
    {
        titleLabel.BbcodeEnabled = false;
        titleLabel.Text = winner == "Player1" ? "🔴 P1 获得胜利！" : "🔵 P2 获得胜利！";
        titleLabel.AddThemeColorOverride("default_color", 
            winner == "Player1" ? new Color(1, 0.2f, 0.2f) : new Color(0.2f, 0.5f, 1));
        titleLabel.AddThemeFontSizeOverride("normal_font_size", 56);

        // ✅ 关键修复：在容器中水平填充并居中
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; // 填充容器宽度
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.VerticalAlignment = VerticalAlignment.Center;
        titleLabel.CustomMinimumSize = new Godot.Vector2(0, 80); // 只设高度，宽度由容器决定

        titleLabel.Show();
    }

    // ========== 副标题 ==========
    if (subLabel != null)
    {
        subLabel.BbcodeEnabled = false;
        subLabel.Text = winner == "Player1" ? "🔵 P2 战败" : "🔴 P1 战败";
        subLabel.AddThemeColorOverride("default_color", new Color(0.8f, 0.8f, 0.8f));
        subLabel.AddThemeFontSizeOverride("normal_font_size", 32);

        // ✅ 同样设置填充
        subLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        subLabel.HorizontalAlignment = HorizontalAlignment.Center;
        subLabel.CustomMinimumSize = new Godot.Vector2(0, 50);

        subLabel.Show();
    }

    // ========== 按钮 ==========
    if (btn != null)
    {
        btn.Text = "🔄 再来一局";
        btn.AddThemeFontSizeOverride("font_size", 24);
        btn.CustomMinimumSize = new Godot.Vector2(200, 60);

        // 按钮不需要 ExpandFill，因为它有固定宽度，VBox 会自动居中它

        var style = new StyleBoxFlat();
        style.BgColor = winner == "Player1" ? new Color(1, 0.3f, 0.3f) : new Color(0.3f, 0.5f, 1);
        style.SetCornerRadiusAll(12);
        btn.AddThemeStyleboxOverride("normal", style);

        btn.Show();
    }

}


private void CreateFallbackLabel(Control parent, string name, string text, int fontSize)
{
    var label = new Label(); // 使用普通Label而不是RichTextLabel，更简单可靠
    label.Name = name + "Label";
    label.Text = text;
    label.AddThemeFontSizeOverride("font_size", fontSize);
    label.AddThemeColorOverride("font_color", Colors.White);
    label.HorizontalAlignment = HorizontalAlignment.Center;
    label.CustomMinimumSize = new Godot.Vector2(400, fontSize + 20);

    parent.AddChild(label);
    label.Owner = parent;

    GD.Print($"[Victory] 动态创建了标签: {name}");
}
private void SetupVictoryLayout()
{
    if (victoryPanel == null) return;

    var viewportSize = GetViewport().GetVisibleRect().Size;
    var centerX = viewportSize.X / 2;
    var centerY = viewportSize.Y / 2;

    // 标题位置（中上）
    if (victoryTitleLabel != null)
    {
        victoryTitleLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
        victoryTitleLabel.Position = new Godot.Vector2(centerX - 200, centerY - 120);
        victoryTitleLabel.Size = new Godot.Vector2(400, 80);
    }

    // 副标题位置（中）
    if (victorySubtitleLabel != null)
    {
        victorySubtitleLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
        victorySubtitleLabel.Position = new Godot.Vector2(centerX - 150, centerY - 20);
        victorySubtitleLabel.Size = new Godot.Vector2(300, 50);
    }

    // 按钮位置（中下）
    if (restartButton != null)
    {
        restartButton.SetAnchorsPreset(Control.LayoutPreset.Center);
        restartButton.Position = new Godot.Vector2(centerX - 100, centerY + 60);
    }
}

// 入场动画（淡入+缩放）
private void AnimateVictoryEntry()
{
    if (victoryPanel == null) return;

    victoryPanel.Modulate = new Color(1, 1, 1, 0); // 完全透明开始

    var tween = CreateTween();
    tween.SetTrans(Tween.TransitionType.Back);
    tween.SetEase(Tween.EaseType.Out);

    // 淡入
    tween.TweenProperty(victoryPanel, "modulate:a", 1.0f, 0.5f);

    // 标题弹跳效果
    if (victoryTitleLabel != null)
    {
        victoryTitleLabel.Scale = Godot.Vector2.Zero;
        var tween2 = CreateTween();
        tween2.SetTrans(Tween.TransitionType.Back);
        tween2.SetEase(Tween.EaseType.Out);
        tween2.TweenProperty(victoryTitleLabel, "scale", Godot.Vector2.One, 0.6f)
               .SetDelay(0.1f);
    }
}

// 如果Inspector没绑定节点，自动创建UI（备用方案）
private void CreateVictoryUI()
{
    // 创建主面板
    victoryPanel = new Control();
    victoryPanel.Name = "VictoryPanel";
    AddChild(victoryPanel);

    // 创建背景
    var bg = new ColorRect();
    bg.Name = "Background";
    bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    bg.Color = new Color(0, 0, 0, 0.9f);
    victoryPanel.AddChild(bg);

    // 创建标题标签
    victoryTitleLabel = new RichTextLabel();
    victoryTitleLabel.Name = "VictoryTitle";
    victoryTitleLabel.BbcodeEnabled = false; // 不用BBCode，用Modulate控制颜色
    victoryPanel.AddChild(victoryTitleLabel);

    // 创建副标题
    victorySubtitleLabel = new RichTextLabel();
    victorySubtitleLabel.Name = "VictorySubtitle";
    victorySubtitleLabel.BbcodeEnabled = false;
    victoryPanel.AddChild(victorySubtitleLabel);

    // 创建按钮
    restartButton = new Button();
    restartButton.Name = "RestartButton";
    restartButton.Pressed += OnRestartPressed;
    victoryPanel.AddChild(restartButton);

    victoryPanel.Hide(); // 初始隐藏
    GD.Print("[CreateVictoryUI] 自动创建胜利UI完成");
}


public void RestartGame()
{
    // 重新加载当前场景
    GetTree().ReloadCurrentScene();
}

    public void RemoveUnit(Infantry unit)
    {
        // ✅ 从所有专用列表中移除
        infantryUnits.Remove(unit);
        mechUnits.Remove(unit);
        ooziumUnits.Remove(unit);
        tankUnits.Remove(unit);
        mdTankUnits.Remove(unit);
        vehicleUnits.Remove(unit);
        artilleryUnits.Remove(unit);
        antiairUnits.Remove(unit);
        apcUnits.Remove(unit);
        reconUnits.Remove(unit);

        unitCategories.Remove(unit);
        unitManager?.RemoveUnit(unit);

        if (selectedInfantry == unit)
        {
            selectedInfantry = null;
        }
        CheckVictoryCondition();
        // ✅ 更新统计
        UpdateUnitLists();
    }

    // 从列表移除（供 Infantry 调用）
    public void RemovePiece(Infantry piece)
    {
        RemoveUnit(piece);
    }

    // 切换回合
    public void NextPhase()
    {
        HideProductionMenu();
        turnEndLabel?.Show();
        GetTree().CreateTimer(1.2f).Timeout += () => turnEndLabel?.Hide();
        int previousTurn = turns; 
        turnPhase++;
        if (turnPhase > totalPhases)
        {
            turns++;
            turnPhase = 1;
            GD.Print($"=== 第 {turns} 回合开始 ===");
            gridManager?.PerformTurnSupply();

        weaponManager?.OnTurnStartForNewTurn(); // 使用新方法

        // ✅ 照明弹效果大回合过期处理
        fogOfWarManager?.OnBigTurnEnd();

        }

        string currentTeam = TeamHelper.GetPhaseTeamName(turnPhase);
        GD.Print($"阶段 {turnPhase}/{totalPhases} - {currentTeam} 的回合");

        // ✅ 资金系统：每回合开始时给对应势力增加资金
        if (turnPhase == 1)
        {
            AddFundsForTeam("Player1");
        }
        else if (turnPhase == 2)
        {
            AddFundsForTeam("Player2");
        }

        // ✅ P0 阶段：P0 和 P-1 兵器自动执行 AI
        if (turnPhase == 3)
        {
            foreach (var wp in weaponManager?.AllWeapons ?? new List<Weapon>())
            {
                if (IsInstanceValid(wp) && (TeamHelper.ShouldWeaponUseAI(wp.team) || TeamHelper.ShouldPlayerWeaponUseAI(turnPhase, wp.team)))
                {
                    wp.ExecuteAI();
                }
            }
        }

        UpdateTurnLabel();
        
        // ========== ✅ 刷新战争迷雾 ==========
        fogOfWarManager?.OnTurnChanged();
        bool isNewTurn = (turns > previousTurn);
        selectedInfantry = null;
                        if (turnPhase == 1)

        {
        }
        Infantry.UpdateUnitsCannotMove();
        // 重置所有单位状态
        
                foreach (var wp in weaponManager?.AllWeapons ?? new List<Weapon>())
        {
            if (IsInstanceValid(wp))
            {
                wp.OnTurnStart();
            }
        }
        foreach (var unit in unitManager?.AllUnits ?? new List<Infantry>())
        {
            if (!IsInstanceValid(unit)) 
            {
                continue;
            }
        if (isNewTurn && unit.canTransportUnits && unit.maxTransportCapacity > 0 )
        {
            unit.OnTurnStartAutoSupply();
        }
            if (unit.currentCaptureProgress > 0)
            {
                if (unit.grid != unit.capturingGrid)
                {
                    // 不在占领的格子上了，重置进度
                    unit.currentCaptureProgress = 0;
                    unit.capturingGrid = null;
                }
                else
                {
                }
            }
            bool canMoveThisTurn = APC.CanUnitMove(unit);
            unit.isMoved = false;
            unit.isAttacked = false;




                if (unit is Infantry inf)
    {
        inf.UpdateTeamVisual();
    }




            unit.originalGrid = null;
            unit.state = UnitState.Idle;
            unit.isMoved = false;
            unit.isAttacked = false;
            unit.movePoints = unit.defaultMovePoints;
            unit.SetWaitVisual(false);
            unit.StartBreath();


            var turnEndMethod = unit.GetType().GetMethod("OnTurnEnd");
            if (turnEndMethod != null && turnEndMethod.DeclaringType != typeof(Infantry))
            {
                turnEndMethod.Invoke(unit, null);
            }

            if (unit.hpLabel != null)
            {
                unit.hpLabel.Modulate = Colors.White;
            }

            // 重新绑定格子
            if (unit.grid == null)
            {
                unitManager.BindUnitToGrid(unit, true);
            }
            else
            {
                var expectedGridPos = unitManager.WorldToGrid(unit.Position);
                if (unit.grid.GridIndex != expectedGridPos)
                {
                    unitManager.BindUnitToGrid(unit, true);
                }
            }
        }
        // ✅ 每完整 Turn 燃料消耗（仅在完整回合切换时，即两 Phase 都结束后）
        if (isNewTurn)
        {
            foreach (var u in unitManager?.AllUnits ?? new List<Infantry>())
            {
                if (IsInstanceValid(u))
                    u.ConsumeDailyFuel();
            }
        }

        weaponManager?.OnPhaseStart();
        // 关闭所有UI
        gridManager.CloseRange();
        gridManager.HideAttackRange();
        (GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu)?.Hide();

        // ✅ 回合切换时更新统计（可能有单位死亡）
        UpdateUnitLists();

        // ========== ✅ 新增：回合切换时触发地形伤害（完整回合切换时）==========
        if (isNewTurn)
        {
            ApplyTerrainDamageToAllUnits();
        }

        // ✅ AI 回合触发
        var aiManager = GetTree()?.GetFirstNodeInGroup("ai_manager") as AI_Manager;
        if (aiManager != null && aiManager.IsCurrentPhaseAI())
        {
            aiManager.OnPhaseStart();
        }
    }

    // ✅ 资金系统：给指定势力增加设施资金收入
    private void AddFundsForTeam(string team)
    {
        if (gridManager?.grids == null) return;
        int income = 0;
        foreach (var grid in gridManager.grids)
        {
            if (grid?.city == null) continue;
            if (grid.city.facilityTeam != team) continue;
            if (grid.city is City c)
            {
                income += c.fundsPerTurn;
            }
        }

        if (team == "Player1")
        {
            p1Funds = Mathf.Min(p1Funds + income, p1FundsMax);
            GD.Print($"[Funds] P1 获得资金 {income}，当前资金: {p1Funds}/{p1FundsMax}");
        }
        else if (team == "Player2")
        {
            p2Funds = Mathf.Min(p2Funds + income, p2FundsMax);
            GD.Print($"[Funds] P2 获得资金 {income}，当前资金: {p2Funds}/{p2FundsMax}");
        }

        UpdateUnitLists();
    }

    // ========== ✅ 新增：对所有单位应用地形伤害 ==========
    private void ApplyTerrainDamageToAllUnits()
    {
        if (unitManager?.AllUnits == null) return;

        GD.Print($"[ApplyTerrainDamage] ===== 第 {turns} 回合地形伤害结算 =====");

        foreach (var unit in unitManager.AllUnits.ToList())
        {
            if (unit == null || !IsInstanceValid(unit)) continue;
            if (unit.grid == null) continue;

            var grid = unit.grid;

            // 1. 地形固有伤害
            grid.ApplyTerrainDamage(unit);

            // 检查单位是否已被消灭
            if (!IsInstanceValid(unit) || unit.health <= 0) continue;

            // 2. 格子自定义攻击（经防御判定）
            grid.ApplyGridCustomAttack(unit);

            if (!IsInstanceValid(unit) || unit.health <= 0) continue;

            // 3. 格子自定义固定伤害（不走防御，可为负回血）
            grid.ApplyGridCustomDamage(unit);

            if (!IsInstanceValid(unit) || unit.health <= 0) continue;

            // 4. 格子弹药变化（固定值加减，无判定）
            grid.ApplyGridAmmoChange(unit);

            // 5. 格子燃料变化（固定值加减，无判定）
            grid.ApplyGridFuelChange(unit);

            // 6. 锁血判定
            grid.LockHealthIfNeeded(unit);
        }

        // 更新统计（可能有单位死亡）
        UpdateUnitLists();
        CheckVictoryCondition();

        GD.Print($"[ApplyTerrainDamage] ===== 地形伤害结算完成 =====");
    }


    // 更新回合标签
    public void UpdateTurnLabel()
    {
        if (TurnLabel != null)
        {
            TurnLabel.Text = $"当前回合：{turns} - 阶段：{turnPhase}";
        }
    }

    // 清空选中
    public void ClearSelectedInfantry()
    {
        selectedInfantry = null;
    }

    // 判断当前阶段是否允许该阵营移动
    public bool IsTurnPhaseValid(string team)
    {
        return TeamHelper.CanOperateTeam(turnPhase, team);
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
            GD.Print("[GameManager] 编辑模式下禁止操作单位");
            return;
        }

        if (infantry == null)
        {
            GD.Print("取消选择单位");
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

        if (infantry is Oozium oozium)
        {
            GD.Print($"[OnSelectPiece] 选中 Oozium: {oozium.Name}, 吞噬模式已激活");
        }


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
            GD.Print("该单位已行动或当前阶段不允许移动");
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



    int moveCost = 0;
    if (unit is Oozium)
    {
        moveCost = 1; // Oozium 固定消耗1
    }
    else
    {
        // 计算实际消耗
        var path = BuildPath(fromGrid, toGrid);
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
    }

    // 恢复移动力
    unit.movePoints += moveCost;

    // 恢复燃料
    if (unit.consumeFuel)
    {
        int fuelCost = unit is Oozium ? 0 : moveCost;
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

    // ✅ 针对不同类型的单位恢复颜色
    // Mech 和 LightTank 使用 animSprite
    if (unit is Mech mechUnit && mechUnit.animSprite != null)
    {
        mechUnit.animSprite.Modulate = mechUnit.normal;
    }
    else if (unit is LightTank tankUnit && tankUnit.animSprite != null)
    {
        tankUnit.animSprite.Modulate = tankUnit.normal;
    }
    else if (unit.sprite != null)
    {
        // 普通 Infantry 使用 sprite
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
private List<Grids> BuildPath(Grids from, Grids to)
{
    if (gridManager == null) return new List<Grids>();
    return gridManager.BuildPath(from, to);
}

public void RegisterUnit(Infantry unit, UnitCategory category)
{
    // 如果已存在，先从旧列表移除
    if (unitCategories.ContainsKey(unit))
    {
        var oldCategory = unitCategories[unit];
        RemoveFromCategoryList(unit, oldCategory);
    }

    unitCategories[unit] = category;
    AddToCategoryList(unit, category);

    GD.Print($"[GameManager] 注册单位: {unit.Name} -> {category}");

    // 立即更新统计面板
    UpdateUnitLists();
}

    private void AddToCategoryList(Infantry unit, UnitCategory category)
    {
        switch (category)
        {
            case UnitCategory.Infantry:
                if (!infantryUnits.Contains(unit)) infantryUnits.Add(unit);
                break;
            case UnitCategory.Mech:
                if (!mechUnits.Contains(unit)) mechUnits.Add(unit);
                break;
            case UnitCategory.Oozium:
                if (!ooziumUnits.Contains(unit)) ooziumUnits.Add(unit);
                break;
            case UnitCategory.Tank:
                if (!tankUnits.Contains(unit)) tankUnits.Add(unit);
                break;
            case UnitCategory.MdTank:
                if (!mdTankUnits.Contains(unit)) mdTankUnits.Add(unit);
                break;
            case UnitCategory.Vehicle:
                if (!vehicleUnits.Contains(unit)) vehicleUnits.Add(unit);
                break;
            case UnitCategory.APC:  // ✅ 确保有这行！
                if (!apcUnits.Contains(unit)) apcUnits.Add(unit);
                break;
            case UnitCategory.AntiAir:
                if (!antiairUnits.Contains(unit)) antiairUnits.Add(unit);
                break;
            case UnitCategory.Artillery:
                if (!artilleryUnits.Contains(unit)) artilleryUnits.Add(unit);
                break;
            case UnitCategory.Rocket:
                if (!artilleryUnits.Contains(unit)) artilleryUnits.Add(unit);
                break;
            case UnitCategory.FlyBomb:
                if (!flyBombUnits.Contains(unit)) flyBombUnits.Add(unit);
                break;
            case UnitCategory.Recon:
                if (!reconUnits.Contains(unit)) reconUnits.Add(unit);
                break;
        }
    }

    private void RemoveFromCategoryList(Infantry unit, UnitCategory category)
    {
        switch (category)
        {
            case UnitCategory.Infantry:
                infantryUnits.Remove(unit);
                break;
            case UnitCategory.Mech:
                mechUnits.Remove(unit);
                break;
            case UnitCategory.Oozium:
                ooziumUnits.Remove(unit);
                break;
            case UnitCategory.Tank:
                tankUnits.Remove(unit);
                break;
            case UnitCategory.MdTank:
                mdTankUnits.Remove(unit);
                break;
            case UnitCategory.Vehicle:
                vehicleUnits.Remove(unit);
                break;
            case UnitCategory.APC:  
                apcUnits.Remove(unit);
                break;
            case UnitCategory.AntiAir:  
                antiairUnits.Remove(unit);
                break;
            case UnitCategory.Artillery:
                artilleryUnits.Remove(unit);
                break;
            case UnitCategory.Rocket:
                artilleryUnits.Remove(unit);
                break;
            case UnitCategory.FlyBomb:
                flyBombUnits.Remove(unit);
                break;
            case UnitCategory.Recon:
                reconUnits.Remove(unit);
                break;
        }
    }

    public List<Infantry> GetAllArmoredUnits()
    {
        var armored = new List<Infantry>();
        foreach (var node in tankUnits)
        {
            if (IsInstanceValid(node)) armored.Add(node);
        }
        foreach (var node in vehicleUnits)
        {
            if (IsInstanceValid(node)) armored.Add(node);
        }
        return armored;
    }

    // ========== ✅ 战争迷雾公共API ==========
    public void SetFogOfWarEnabled(bool enabled)
    {
        fogOfWarManager?.SetFogOfWarEnabled(enabled);
    }

    public bool IsFogOfWarEnabled()
    {
        return fogOfWarManager?.isFogOfWarEnabled ?? false;
    }

    public bool IsProductionMenuOpen()
    {
        return productionMenuPanel != null && productionMenuPanel.Visible;
    }

    // ========== ✅ 爆炸执行 ==========
    public void ExecuteExplosion(Infantry unit)
    {
        if (unit == null || !unit.canExplode) return;

        // ✅ 无限自爆模式消耗自爆弹
        if (!unit.explosionDestroysSelf)
        {
            if (unit.currentExplodeAmmo <= 0)
            {
                GD.Print($"[Explode] {unit.Name} 自爆弹不足，无法自爆");
                return;
            }
            unit.currentExplodeAmmo--;
            GD.Print($"[Explode] {unit.Name} 消耗1发自爆弹，剩余 {unit.currentExplodeAmmo}/{unit.maxExplodeAmmo}");
        }

        var gridPos = unit.grid?.GridIndex ?? unitManager.WorldToGrid(unit.Position);
        int minR = unit.explosionMinRange;
        int maxR = unit.explosionMaxRange;

        // 获取受影响单位
        var affected = new List<Infantry>();
        foreach (var u in unitManager.AllUnits)
        {
            if (u == null || !IsInstanceValid(u)) continue;
            var uGridPos = u.grid?.GridIndex ?? unitManager.WorldToGrid(u.Position);
            int dist = Mathf.Abs(uGridPos.X - gridPos.X) + Mathf.Abs(uGridPos.Y - gridPos.Y);
            if (dist >= minR && dist <= maxR)
            {
                // 目标筛选
                bool isEnemy = u.team != unit.team;
                bool include = unit.explosionTargetMode switch
                {
                    1 => isEnemy,   // 仅敌
                    2 => !isEnemy,  // 仅友
                    _ => true        // 所有
                };
                if (include) affected.Add(u);
            }
        }

        // 计算并应用伤害（对单位）
        foreach (var target in affected)
        {
            int damage = CalculateExplosionDamage(unit, target);
            ApplyExplosionDamage(unit, target, damage, false);
        }

        // ✅ 影响兵器（如果开启）
        if (unit.explosionAffectsWeapons && weaponManager != null)
        {
            foreach (var w in weaponManager.AllWeapons.ToList())
            {
                if (w == null || !IsInstanceValid(w)) continue;
                if (w.grid == null) continue;
                int dist = Mathf.Abs(w.grid.GridIndex.X - gridPos.X) + Mathf.Abs(w.grid.GridIndex.Y - gridPos.Y);
                if (dist < minR || dist > maxR) continue;

                // 目标筛选（兵器也有 team）
                bool isEnemy = w.team != unit.team;
                bool include = unit.explosionTargetMode switch
                {
                    1 => isEnemy,   // 仅敌
                    2 => !isEnemy,  // 仅友
                    _ => true        // 所有
                };
                if (!include) continue;

                int damage = CalculateExplosionDamage(unit, w);
                ApplyExplosionDamage(unit, w, damage, true);
            }
        }

        // ✅ 自爆内伤（开启、不摧毁自己且最小范围为0时对自身造成伤害）
        if (unit.explosionSelfDamageEnabled && !unit.explosionDestroysSelf && unit.explosionMinRange == 0)
        {
            int selfDamage = CalculateExplosionDamage(unit, unit);
            ApplyExplosionDamage(unit, unit, selfDamage, false);
        }

        // 触发单位自身的 OnExplode
        unit.OnExplode();

        // 播放爆炸特效
        PlayExplosionEffect(unit.GlobalPosition);

        // 摧毁自身
        if (unit.explosionDestroysSelf)
        {
            unitManager.RemoveUnit(unit);
        }
        else
        {
            // 无限爆炸模式：重置为已行动状态
            unit.state = UnitState.Acted;
            unit.isMoved = true;
            unit.SetWaitVisual(true);
        }
    }

    // ========== ✅ 爆炸特效 ==========
    private void PlayExplosionEffect(Vector2 position)
    {
        var particles = new GpuParticles2D
        {
            Position = position,
            ZIndex = 300,
            Emitting = true,
            OneShot = true,
            Explosiveness = 1.0f,
            Amount = 80,
            Lifetime = 1.2f,
        };

        var material = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 20.0f,
            InitialVelocityMin = 60.0f,
            InitialVelocityMax = 200.0f,
            ScaleMin = 4.0f,
            ScaleMax = 10.0f,
            Color = new Color(1.0f, 0.35f, 0.0f, 1.0f),
            Gravity = new Vector3(0, 0, 0),
            DampingMin = 50.0f,
            DampingMax = 100.0f,
        };

        particles.ProcessMaterial = material;
        GetTree().CurrentScene.AddChild(particles);

        // 自动清理
        var timer = new Timer
        {
            WaitTime = 2.0f,
            OneShot = true,
            Autostart = true
        };
        timer.Timeout += () =>
        {
            particles.QueueFree();
            timer.QueueFree();
        };
        particles.AddChild(timer);

        // 屏幕震动（可选）
        var camera = GetTree().CurrentScene.GetNodeOrNull<Camera2D>("Camera2D");
        if (camera != null)
        {
            camera.Offset = new Vector2(GD.Randf() * 6 - 3, GD.Randf() * 6 - 3);
            var shakeTimer = new Timer { WaitTime = 0.15f, OneShot = true, Autostart = true };
            shakeTimer.Timeout += () => { camera.Offset = Vector2.Zero; shakeTimer.QueueFree(); };
            camera.AddChild(shakeTimer);
        }
    }

    // ========== ✅ 爆炸伤害计算 ==========
    private int CalculateExplosionDamage(Infantry source, object target)
    {
        int maxHp = 100;
        if (target is Infantry inf) maxHp = inf.maxHealth;
        else if (target is Weapon w) maxHp = w.maxHealth;

        switch (source.explosionDamageMode)
        {
            case 0: // 固定值（可为负=加血）
                return source.explosionFixedValue;
            case 1: // 百分比（最大HP，可为负=加血）
                return Mathf.RoundToInt(maxHp * source.explosionPercentValue);
            case 2: // 攻击公式（查攻防表）
                if (target is Infantry tInf)
                {
                    int dmg = source.GetPrimaryDamageFromMatrix(tInf);
                    if (dmg == 0) dmg = Mathf.RoundToInt(maxHp * source.explosionPercentValue);
                    return dmg;
                }
                // 兵器没有攻防表，回退百分比
                return Mathf.RoundToInt(maxHp * source.explosionPercentValue);
            default:
                return 0;
        }
    }

    // ========== ✅ 爆炸伤害应用 ==========
    private void ApplyExplosionDamage(Infantry source, object target, int damage, bool isWeapon)
    {
        if (damage == 0) return; // 0=无变化

        if (isWeapon && target is Weapon w)
        {
            // 兵器
            if (damage > 0)
            {
                // 扣血
                w.TakeDamage(damage);
                if (!source.explosionCanKill && w.health <= 0)
                    w.health = 1;
                if (w.health <= 0)
                    weaponManager.RemoveWeapon(w);
            }
            else
            {
                // 回血（负值）
                int heal = -damage;
                int newHealth = w.health + heal;
                if (!source.explosionCanExceedMaxHealth && newHealth > w.maxHealth)
                    newHealth = w.maxHealth;
                w.health = newHealth;
                w.UpdateHpLabel();
            }
        }
        else if (target is Infantry inf)
        {
            // 单位
            if (damage > 0)
            {
                // 扣血
                int newHealth = inf.health - damage;
                if (!source.explosionCanKill && newHealth <= 0)
                    newHealth = 1;
                inf.health = Mathf.Max(0, newHealth);
                inf.UpdateHpLabel();
                if (inf.health <= 0)
                    unitManager.RemoveUnit(inf);
            }
            else
            {
                // 回血（负值）
                int heal = -damage;
                int newHealth = inf.health + heal;
                if (!source.explosionCanExceedMaxHealth && newHealth > inf.maxHealth)
                    newHealth = inf.maxHealth;
                inf.health = newHealth;
                inf.UpdateHpLabel();
            }
        }
    }

    // ========== ✅ 生产系统 ==========
    private Panel productionMenuPanel;
    private Label productionMenuTitle;
    private VBoxContainer productionMenuContainer;
    private Grids productionTargetGrid;
    private Facility productionTargetFacility;

    public void ShowProductionMenu(Facility facility, Grids grid)
    {
        if (facility == null || !facility.canProduce || facility.producibleUnitNames.Count == 0) return;

        // 关闭行动菜单，避免重叠
        (GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu)?.Hide();

        productionTargetGrid = grid;
        productionTargetFacility = facility;

        if (productionMenuPanel == null)
        {
            CreateProductionMenuUI();
        }

        // 刷新列表
        foreach (var child in productionMenuContainer.GetChildren())
            child.QueueFree();

        string currentTeam = turnPhase == 1 ? "Player1" : "Player2";
        int currentFunds = GetTeamFunds(currentTeam);

        productionMenuTitle.Text = $"🏭 生产菜单 ({facility.GetType().Name})\n💰 资金: {currentFunds} G";

        foreach (string unitName in facility.producibleUnitNames)
        {
            if (!UnitProductionDatabase.HasUnit(unitName)) continue;

            var info = UnitProductionDatabase.GetInfo(unitName);
            bool canAfford = currentFunds >= info.Cost;
            bool gridEmpty = grid.infantries.Count == 0 && grid.weapons.Count == 0;

            var btn = new Button();
            btn.CustomMinimumSize = new Godot.Vector2(300, 50);
            btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            btn.Text = $"{info.DisplayName}\n💰 {info.Cost} G";
            btn.AddThemeFontSizeOverride("font_size", 13);

            if (!gridEmpty)
            {
                btn.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                var disabledStyle = new StyleBoxFlat();
                disabledStyle.BgColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
                disabledStyle.SetCornerRadiusAll(6);
                btn.AddThemeStyleboxOverride("normal", disabledStyle);
                btn.TooltipText = "格子已被占据";
                btn.Disabled = true;
            }
            else if (!canAfford)
            {
                btn.AddThemeColorOverride("font_color", new Color(0.8f, 0.3f, 0.3f));
                var poorStyle = new StyleBoxFlat();
                poorStyle.BgColor = new Color(0.3f, 0.1f, 0.1f, 0.6f);
                poorStyle.SetCornerRadiusAll(6);
                btn.AddThemeStyleboxOverride("normal", poorStyle);
                btn.TooltipText = "资金不足";
                btn.Disabled = true;
            }
            else
            {
                btn.AddThemeColorOverride("font_color", Colors.White);
                var normalStyle = new StyleBoxFlat();
                normalStyle.BgColor = new Color(0.2f, 0.4f, 0.3f, 0.8f);
                normalStyle.SetCornerRadiusAll(6);
                btn.AddThemeStyleboxOverride("normal", normalStyle);
                var hoverStyle = new StyleBoxFlat();
                hoverStyle.BgColor = new Color(0.3f, 0.6f, 0.4f, 0.9f);
                hoverStyle.SetCornerRadiusAll(6);
                btn.AddThemeStyleboxOverride("hover", hoverStyle);

                string capturedUnitName = unitName;
                btn.Pressed += () => OnProduceUnitClicked(capturedUnitName);
            }

            productionMenuContainer.AddChild(btn);
        }

        var closeBtn = new Button();
        closeBtn.Text = "❌ 关闭";
        closeBtn.CustomMinimumSize = new Godot.Vector2(300, 40);
        closeBtn.AddThemeColorOverride("font_color", Colors.White);
        var closeStyle = new StyleBoxFlat();
        closeStyle.BgColor = new Color(0.4f, 0.2f, 0.2f, 0.8f);
        closeStyle.SetCornerRadiusAll(6);
        closeBtn.AddThemeStyleboxOverride("normal", closeStyle);
        closeBtn.Pressed += HideProductionMenu;
        productionMenuContainer.AddChild(closeBtn);

        productionMenuPanel.Visible = true;
    }

    private void CreateProductionMenuUI()
    {
        var canvas = GetTree().CurrentScene.GetNodeOrNull<CanvasLayer>("CanvasLayer");
        if (canvas == null) return;

        productionMenuPanel = new Panel();
        productionMenuPanel.Name = "ProductionMenuPanel";
        productionMenuPanel.CustomMinimumSize = new Godot.Vector2(360, 500);
        productionMenuPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
        productionMenuPanel.Visible = false;
        productionMenuPanel.ZIndex = 1200;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.1f, 0.12f, 0.18f, 0.97f);
        panelStyle.SetCornerRadiusAll(16);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.BorderColor = new Color(0.4f, 0.7f, 0.9f);
        productionMenuPanel.AddThemeStyleboxOverride("panel", panelStyle);

        productionMenuTitle = new Label();
        productionMenuTitle.Name = "ProductionMenuTitle";
        productionMenuTitle.Text = "🏭 生产菜单";
        productionMenuTitle.HorizontalAlignment = HorizontalAlignment.Center;
        productionMenuTitle.AddThemeFontSizeOverride("font_size", 18);
        productionMenuTitle.AddThemeColorOverride("font_color", new Color(0.4f, 0.9f, 1f));
        productionMenuTitle.CustomMinimumSize = new Godot.Vector2(0, 60);
        productionMenuTitle.Position = new Godot.Vector2(0, 12);
        productionMenuTitle.Size = new Godot.Vector2(360, 60);
        productionMenuPanel.AddChild(productionMenuTitle);

        // ✅ 拖拽手柄
        var dragHandle = new Panel();
        dragHandle.Name = "ProductionDragHandle";
        dragHandle.CustomMinimumSize = new Godot.Vector2(360, 28);
        dragHandle.Position = new Godot.Vector2(0, 0);
        var handleStyle = new StyleBoxFlat();
        handleStyle.BgColor = new Color(0.3f, 0.3f, 0.4f, 0.5f);
        handleStyle.SetCornerRadiusAll(8);
        dragHandle.AddThemeStyleboxOverride("panel", handleStyle);
        var dragHint = new Label();
        dragHint.Text = "⋮⋮ 拖拽移动";
        dragHint.AddThemeFontSizeOverride("font_size", 10);
        dragHint.AddThemeColorOverride("font_color", new Color(0.6f, 0.7f, 0.8f));
        dragHint.Position = new Godot.Vector2(8, 2);
        dragHandle.AddChild(dragHint);
        productionMenuPanel.AddChild(dragHandle);

        // ✅ 绑定拖拽
        dragHandle.GuiInput += (InputEvent @event) => {
            if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
            {
                if (mouseEvent.Pressed)
                {
                    isDraggingProductionMenu = true;
                    currentDraggedProductionMenu = productionMenuPanel;
                    dragOffsetProduction = productionMenuPanel.GlobalPosition - dragHandle.GetGlobalMousePosition();
                }
                else
                {
                    isDraggingProductionMenu = false;
                    currentDraggedProductionMenu = null;
                }
            }
        };

        var closeBtn = new Button();
        closeBtn.Name = "ProductionCloseBtn";
        closeBtn.Text = "✕";
        closeBtn.CustomMinimumSize = new Godot.Vector2(32, 32);
        closeBtn.Position = new Godot.Vector2(320, 10);
        closeBtn.ZIndex = 10;
        closeBtn.Pressed += HideProductionMenu;
        productionMenuPanel.AddChild(closeBtn);

        var scroll = new ScrollContainer();
        scroll.Position = new Godot.Vector2(15, 80);
        scroll.Size = new Godot.Vector2(330, 400);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

        productionMenuContainer = new VBoxContainer();
        productionMenuContainer.AddThemeConstantOverride("separation", 6);
        productionMenuContainer.Size = new Godot.Vector2(330, 0);

        scroll.AddChild(productionMenuContainer);
        productionMenuPanel.AddChild(scroll);

        canvas.AddChild(productionMenuPanel);
    }

    private void HideProductionMenu()
    {
        if (productionMenuPanel != null)
            productionMenuPanel.Visible = false;
        productionTargetGrid = null;
        productionTargetFacility = null;
    }

    private void OnProduceUnitClicked(string unitName)
    {
        if (productionTargetGrid == null || productionTargetFacility == null) return;
        if (!UnitProductionDatabase.HasUnit(unitName)) return;

        var info = UnitProductionDatabase.GetInfo(unitName);
        string currentTeam = turnPhase == 1 ? "Player1" : "Player2";

        // 编辑模式不判定资金
        bool isEditMode = terrainEditor != null && terrainEditor.IsEditMode;
        if (!isEditMode)
        {
            int currentFunds = GetTeamFunds(currentTeam);
            if (currentFunds < info.Cost)
            {
                GD.Print($"[Production] 资金不足: {currentFunds} < {info.Cost}");
                return;
            }
            SetTeamFunds(currentTeam, currentFunds - info.Cost);
        }

        // 生成单位
        var scene = GD.Load<PackedScene>(info.ScenePath);
        if (scene == null)
        {
            GD.PrintErr($"[Production] 无法加载场景: {info.ScenePath}");
            return;
        }

        var unit = scene.Instantiate<Infantry>();
        if (unit == null) return;

        unit.Position = productionTargetGrid.Position;
        unit.team = currentTeam;
        int unitNumber = unitManager.AllUnits.Count + 1;
        unit.Name = $"{currentTeam}_{unitName}_{unitNumber}";

        unitManager.units.AddChild(unit);
        bool bound = unitManager.BindUnitToGrid(unit, true);
        if (!bound)
        {
            unit.QueueFree();
            GD.PrintErr("[Production] 绑定格子失败");
            return;
        }

        unitManager.AllUnits.Add(unit);

        RegisterUnit(unit, InferCategory(unit));
        unit.OnClickPiece = OnSelectPiece;
        unit.UpdateTeamVisual();
        unit.UpdateHpLabel();
        if (unit.sprite != null)
            unit.StartBreath();

        UpdateUnitLists();
        RefreshSpecializedUnitLists();

        GD.Print($"[Production] 生产成功: {unit.Name} at {productionTargetGrid.GridIndex}, cost={info.Cost}");

        // 刷新菜单（资金已变化，可能有些按钮要变灰）
        ShowProductionMenu(productionTargetFacility, productionTargetGrid);
    }

    public int GetTeamFunds(string team)
    {
        return team switch
        {
            "Player1" => p1Funds,
            "Player2" => p2Funds,
            _ => 0
        };
    }

    public void SetTeamFunds(string team, int value)
    {
        if (team == "Player1") p1Funds = Mathf.Clamp(value, 0, p1FundsMax);
        else if (team == "Player2") p2Funds = Mathf.Clamp(value, 0, p2FundsMax);
        UpdateUnitLists();
    }

    // ========== ✅ 重置游戏状态（导入地图后调用）==========
    public void ResetGameState()
    {
        gameEnded = false;
        if (victoryPanel != null)
        {
            victoryPanel.Hide();
            victoryPanel.Modulate = new Color(1, 1, 1, 1);
        }
        GD.Print("[GameManager] 游戏状态已重置");
    }

    // ========== ✅ 生产菜单拖拽处理 ==========
    public override void _Process(double delta)
    {
        if (isDraggingProductionMenu && currentDraggedProductionMenu != null)
        {
            Vector2 newPosition = currentDraggedProductionMenu.GetGlobalMousePosition() + dragOffsetProduction;
            currentDraggedProductionMenu.GlobalPosition = newPosition;
        }
    }

}

