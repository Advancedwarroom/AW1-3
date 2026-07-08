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

    // 回合管理
    public int turns = 1;
    public int turnPhase = 1;
    public const int totalPhases = 2;

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
    
    gameEnded = false;
}

// 新增方法
private void DeferredFogRefresh()
{
    fogOfWarManager?.RefreshFog();
    GD.Print($"[GameManager] 延迟刷新战争迷雾完成");
}
    // GameManager.cs - 在类顶部添加
private double lastLeftClickTime = 0;
private int leftClickCount = 0;
private const double TRIPLE_CLICK_WINDOW = 0.5; // 500ms内算一次连击

public override void _Input(InputEvent @event)
{
    var terrainEditor = GetTree().GetFirstNodeInGroup("terrain_editor") as TerrainEditor;
    if (terrainEditor != null && terrainEditor.ShouldBlockUnitOperations())
        return;

    if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
    {
        // ✅ 右键单击：直接切换回合（保留原版功能）
        if (mouseEvent.ButtonIndex == MouseButton.Right)
        {
            GD.Print("😊👌右键单击切换回合");
            NextPhase();
        }
        
        // ✅ 左键三连击：切换回合（防止双击误触）
        if (mouseEvent.ButtonIndex == MouseButton.Left)
        {
            double now = Time.GetTicksMsec() / 1000.0;
            
            if (now - lastLeftClickTime < TRIPLE_CLICK_WINDOW)
            {
                leftClickCount++;
                GD.Print($"[左键连击] 第 {leftClickCount} 次点击");
                
                // 只有三连击才能切换回合
                if (leftClickCount >= 3)
                {
                    GD.Print("🔥🔥🔥 左键三连击成功！切换回合！");
                    NextPhase();
                    leftClickCount = 0;
                }
            }
            else
            {
                leftClickCount = 1;
            }
            lastLeftClickTime = now;
        }
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


    public void UpdateUnitLists()
    {
        if (UnitLists == null) return;

        // ✅ 直接从源头实时计算，不依赖维护的列表
        var all = unitManager?.AllUnits ?? new List<Infantry>();
        int p1Infantry = 0, p2Infantry = 0;
        int p1Mech = 0, p2Mech = 0;
        int p1Oozium = 0, p2Oozium = 0;
        int p1Tank = 0, p2Tank = 0;
        int p1MdTank = 0, p2MdTank = 0;
        int p1Artillery = 0, p2Artillery = 0;
        int p1Rocket = 0, p2Rocket = 0;
        int p1APC = 0, p2APC = 0;
        int p1Recon = 0, p2Recon = 0;
        int p1AntiAir = 0, p2AntiAir = 0;
        int p1AntiTank = 0, p2AntiTank = 0;
        int p1FlyBomb = 0, p2FlyBomb = 0;

        foreach (var u in all)
        {
            if (u == null || !IsInstanceValid(u)) continue;
            string team = u.team;

            if (u is Mech) { if (team == "Player1") p1Mech++; else p2Mech++; }
            else if (u is Oozium) { if (team == "Player1") p1Oozium++; else p2Oozium++; }
            else if (u is FlyBomb) { if (team == "Player1") p1FlyBomb++; else p2FlyBomb++; }
            else if (u is LightTank) { if (team == "Player1") p1Tank++; else p2Tank++; }
            else if (u is MdTank) { if (team == "Player1") p1MdTank++; else p2MdTank++; }
            else if (u is APC) { if (team == "Player1") p1APC++; else p2APC++; }
            else if (u is Recon) { if (team == "Player1") p1Recon++; else p2Recon++; }
            else if (u is Artillery) { if (team == "Player1") p1Artillery++; else p2Artillery++; }
            else if (u is Rocket) { if (team == "Player1") p1Rocket++; else p2Rocket++; }
            else if (u is AntiAir) { if (team == "Player1") p1AntiAir++; else p2AntiAir++; }
            else if (u is AntiTank) { if (team == "Player1") p1AntiTank++; else p2AntiTank++; }
            else if (u is Infantry) { if (team == "Player1") p1Infantry++; else p2Infantry++; }
        }

        // 兵器统计
        int p1Weapons = 0, p2Weapons = 0;
        int p1Cannons = 0, p2Cannons = 0;
        int p1Lasers = 0, p2Lasers = 0;
        if (weaponManager != null)
        {
            foreach (var w in weaponManager.AllWeapons.ToList())
            {
                if (w == null || !IsInstanceValid(w)) continue;
                string team = w.team;
                if (w is BlackCannon) { if (team == "Player1") p1Cannons++; else p2Cannons++; p1Weapons++; }
                else if (w is Laser) { if (team == "Player1") p1Lasers++; else p2Lasers++; p1Weapons++; }
                else { if (team == "Player1") p1Weapons++; else p2Weapons++; }
            }
        }

        int totalUnits = all.Count(u => u != null && IsInstanceValid(u));
        int totalWeapons = weaponManager?.AllWeapons.Count(w => w != null && IsInstanceValid(w)) ?? 0;

        // 构建文本
        string text = " [color=red]单位[/color][color=blue]统计[/color] \n";
        text += $"[color=red]🔴P1[/color]\t\t\t\t|[color=blue]🔵P2[/color]\n";
        text += $"[color=red]步兵:\t\t{p1Infantry}\t\t[/color]|\t\t[color=blue]{p2Infantry}[/color]\n";
        text += $"[color=red]机甲:\t\t{p1Mech}\t\t[/color]|\t   [color=blue] {p2Mech}[/color]\n";
        text += $"[color=red]Oozium:\t{p1Oozium}[/color]\t\t|\t\t[color=blue]{p2Oozium}[/color]\n";

        if (p1Tank > 0 || p2Tank > 0)
            text += $"[color=red]轻坦: \t\t{p1Tank}\t\t[/color]|[color=blue]\t\t{p2Tank}\n[/color]";

        if (p1MdTank > 0 || p2MdTank > 0)
            text += $"[color=red]重坦: \t\t{p1MdTank}\t\t[/color]|[color=blue]\t\t{p2MdTank}\n[/color]";

        if (p1APC > 0 || p2APC > 0)
            text += $"[color=red]运输车:\t{p1APC}\t\t[/color]|\t\t[color=blue]{p2APC}[/color]\n";

        if (p1Recon > 0 || p2Recon > 0)
            text += $"[color=red]侦察车:\t{p1Recon}\t\t[/color]|\t\t[color=blue]{p2Recon}[/color]\n";

        if (p1Artillery > 0 || p2Artillery > 0)
            text += $"[color=red]火炮:\t\t{p1Artillery}\t\t[/color]|\t\t[color=blue]{p2Artillery}[/color]\n";

        if (p1Rocket > 0 || p2Rocket > 0)
            text += $"[color=red]火箭炮:\t{p1Rocket}\t\t[/color]|\t\t[color=blue]{p2Rocket}[/color]\n";

        if (p1AntiAir > 0 || p2AntiAir > 0)
            text += $"[color=red]防空高炮:\t{p1AntiAir}\t\t[/color]|\t\t[color=blue]{p2AntiAir}[/color]\n";

        if (p1AntiTank > 0 || p2AntiTank > 0)
            text += $"[color=red]反坦克炮:\t{p1AntiTank}\t\t[/color]|\t\t[color=blue]{p2AntiTank}[/color]\n";

        if (p1FlyBomb > 0 || p2FlyBomb > 0)
            text += $"[color=red]飞弹:\t{p1FlyBomb}\t\t[/color]|\t\t[color=blue]{p2FlyBomb}[/color]\n";

        if (p1Weapons > 0 || p2Weapons > 0 || p1Cannons > 0 || p2Cannons > 0 || p1Lasers > 0 || p2Lasers > 0)
        {
            text += $"\n[color=yellow]===兵器统计===[/color]\n";
            if (p1Cannons > 0 || p2Cannons > 0)
                text += $"[color=red]迷你加农炮:\t{p1Cannons}[/color]\t\t|\t\t[color=blue]{p2Cannons}[/color]\n";
            if (p1Lasers > 0 || p2Lasers > 0)
                text += $"[color=red]激光炮:\t{p1Lasers}[/color]\t\t|\t\t[color=blue]{p2Lasers}[/color]\n";
        }

        text += $"总计: {totalUnits}单位";
        if (totalWeapons > 0)
            text += $" + {totalWeapons}兵器";

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
        unitListsToggleBtn.OffsetLeft = -120;
        unitListsToggleBtn.OffsetTop = 0;
        unitListsToggleBtn.OffsetRight = -60;
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
    if (gameEnded) return;

    // 获取所有存活单位
    var allUnits = unitManager?.AllUnits.Where(u => IsInstanceValid(u) && u.health > 0).ToList();
    if (allUnits == null) return;

    var p1Units = allUnits.Where(u => u.team == "Player1").ToList();
    var p2Units = allUnits.Where(u => u.team == "Player2").ToList();

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

        }

        string currentTeam = turnPhase == 1 ? "Player1" : "Player2";
        GD.Print($"阶段 {turnPhase}/{totalPhases} - {currentTeam} 的回合");

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
        
                foreach (var wp in weapons)
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
            if (team == "Player0")
    {
        return false;
    }
        return team switch
        {
            "Player1" => turnPhase == 1,
            "Player2" => turnPhase == 2,
            _ => false
        };
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
            selectedInfantry = null;
            selectedWeapon = null; 
            return;
        }
            if (infantry.isAttacked || infantry.state == UnitState.Acted)
        {
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
        mechUnit.animSprite.Modulate = Colors.White;
    }
    else if (unit is LightTank tankUnit && tankUnit.animSprite != null)
    {
        tankUnit.animSprite.Modulate = Colors.White;
    }
    else if (unit.sprite != null)
    {
        // 普通 Infantry 使用 sprite
        unit.sprite.Modulate = Colors.White;
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

    // ========== ✅ 爆炸执行 ==========
    public void ExecuteExplosion(Infantry unit)
    {
        if (unit == null || !unit.canExplode) return;

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

}

