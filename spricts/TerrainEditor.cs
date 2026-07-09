using Godot;
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

public class UnitTemplate
{
    public string DisplayName;
    public string ScenePath;
    public string Category;
    public string Team;
    public Color DisplayColor;
    public string Description;
}

public enum SpawnMode { None, Unit, Weapon, Facility }

public partial class TerrainEditor : Control
{
    [Export] public GameManager gameManager;
    [Export] public GridManager gridManager;

    // ========== UI 组件 ==========
    private Button modeToggleButton;

    // 主选择菜单（单位/兵器/格子）
    private Panel selectionMenuPanel;
    private VBoxContainer selectionButtonContainer;
    private Label selectionTitleLabel;

    // 地形编辑菜单
    private Panel terrainMenuPanel;
    private ScrollContainer terrainScrollContainer;
    private VBoxContainer terrainButtonContainer;
    private Label terrainTitleLabel;

    // 属性编辑器面板（通用：单位+兵器）
    private Panel propertyEditorPanel;
    private ScrollContainer propertyScrollContainer;
    private VBoxContainer propertyContainer;
    private Label propertyEditorTitle;
    private Button propertyEditorCloseBtn;
    private Button propertyEditorSaveBtn;

    private SpawnMode currentSpawnMode = SpawnMode.None;
    private string selectedTemplatePath = "";
    private string selectedTemplateCategory = "";
    // 自定义伤害字段UI（格子专用）
    private VBoxContainer customDamageContainer;
    private CheckBox canDestroyCheckBox;
    private SpinBox fixedDamageSpinBox;
    private SpinBox fixedAttackSpinBox;
    private CheckBox canOverMaxCheckBox;
    private SpinBox fixedAmmoSpinBox;
    private CheckBox ammoCanOverMaxCheckBox;
    private CheckBox ammoCanReachZeroCheckBox;
    private SpinBox fixedFuelSpinBox;
    private CheckBox fuelCanOverMaxCheckBox;
    private CheckBox fuelCanReachZeroCheckBox;
    private VBoxContainer ammoFuelContainer;

    // 状态
    public bool IsEditMode { get; private set; } = false;
    private Grids selectedGrid = null;
    private object currentEditingTarget = null;
    private bool isMenuOpen = false;

    // ========== ✅ 全局配置面板 ==========
    private Panel globalConfigPanel;
    private VBoxContainer globalConfigContainer;
    private CheckBox fogOfWarCheckBox;
    private Button openGlobalConfigBtn;

    // 缓存反射信息（同时缓存字段和属性）
    private Dictionary<Type, List<FieldInfo>> exportFieldsCache = new();
    private Dictionary<Type, List<PropertyInfo>> exportPropertiesCache = new();

public bool IsAnyMenuOpen()
{
    return selectionMenuPanel?.Visible == true 
        || terrainMenuPanel?.Visible == true 
        || propertyEditorPanel?.Visible == true 
        || spawnMenuPanel?.Visible == true
        || globalConfigPanel?.Visible == true;
}
private bool fogStateBeforeEdit = false;
private readonly List<UnitTemplate> unitTemplates = new()
{
    // 步兵类
    new UnitTemplate { DisplayName = "🚶 步兵", ScenePath = "res://Prefabs/infantry(1).tscn", 
        Category = "步兵", Team = "Player1", DisplayColor = new Color(0.4f, 0.7f, 0.4f), 
        Description = "基础步兵单位，可占领城市" },
    new UnitTemplate { DisplayName = "🦾 机甲", ScenePath = "res://Prefabs/mech.tscn", 
        Category = "步兵", Team = "Player1", DisplayColor = new Color(0.5f, 0.6f, 0.4f), 
        Description = "反装甲步兵，移动力较低" },

    // 装甲类
    new UnitTemplate { DisplayName = "🚗 轻型坦克", ScenePath = "res://Prefabs/light_tank.tscn", 
        Category = "装甲", Team = "Player1", DisplayColor = new Color(0.6f, 0.5f, 0.3f), 
        Description = "快速装甲单位，攻防均衡" },
    new UnitTemplate { DisplayName = "🚙 重型坦克", ScenePath = "res://Prefabs/Md_Tank.tscn", 
        Category = "装甲", Team = "Player1", DisplayColor = new Color(0.55f, 0.45f, 0.25f), 
        Description = "重火力履带单位，对轻甲毁灭性，移动力5" },
    new UnitTemplate { DisplayName = "🚀 火箭炮", ScenePath = "res://Prefabs/Rocket.tscn", 
        Category = "装甲", Team = "Player1", DisplayColor = new Color(0.7f, 0.4f, 0.3f), 
        Description = "远程间接火力，射程3-5格" },
    new UnitTemplate { DisplayName = "🔫 自行火炮", ScenePath = "res://Prefabs/Artillery.tscn", 
        Category = "装甲", Team = "Player1", DisplayColor = new Color(0.6f, 0.45f, 0.35f), 
        Description = "中程火力支援，射程2-3格" },
    new UnitTemplate { DisplayName = "🚛 运输车", ScenePath = "res://Prefabs/apc.tscn", 
        Category = "装甲", Team = "Player1", DisplayColor = new Color(0.5f, 0.55f, 0.4f), 
        Description = "运输单位，可搭载步兵" },
        new UnitTemplate { 
    DisplayName = "🛡️ 防空高炮", 
    ScenePath = "res://Prefabs/AntiAir.tscn", 
    Category = "装甲", 
    Team = "Player1", 
    DisplayColor = new Color(0.5f, 0.7f, 0.3f),  
    Description = "对步兵/机甲毁灭性，移动力6，弹药9发，无副武器" 
},
    new UnitTemplate { 
    DisplayName = "🚙 侦察车", 
    ScenePath = "res://Prefabs/Recon.tscn", 
    Category = "装甲", 
    Team = "Player1", 
    DisplayColor = new Color(0.5f, 0.6f, 0.3f),  
    Description = "高视野侦察单位，移动力8，仅副武器，视野5" 
},
    new UnitTemplate { 
    DisplayName = "🎯 反坦克炮", 
    ScenePath = "res://Prefabs/Anti_Tank.tscn", 
    Category = "装甲", 
    Team = "Player1", 
    DisplayColor = new Color(0.55f, 0.5f, 0.3f),  
    Description = "射程1-3反装甲单位，可近战反击，对地高伤害，弹药6发" 
},


    new UnitTemplate { DisplayName = "🔦 照明车", ScenePath = "res://Prefabs/Flare.tscn", 
        Category = "装甲", Team = "Player1", DisplayColor = new Color(0.6f, 0.55f, 0.3f), 
        Description = "可发射照明弹驱散战争迷雾，移动力5，履带移动，仅副武器" },
    new UnitTemplate { DisplayName = "🏍️ 摩托兵", ScenePath = "res://Prefabs/Bike.tscn", 
        Category = "步兵", Team = "Player1", DisplayColor = new Color(0.5f, 0.5f, 0.35f), 
        Description = "快速步兵单位，可占领城市，移动力5，轮胎移动" },

    // 特殊单位
    new UnitTemplate { DisplayName = "🦠 史莱姆", ScenePath = "res://Prefabs/oozium.tscn", 
        Category = "特殊", Team = "Player1", DisplayColor = new Color(0.4f, 0.3f, 0.6f), 
        Description = "吞噬型单位，可消灭任何敌人" },
    new UnitTemplate { DisplayName = "💣 飞弹", ScenePath = "res://Prefabs/FlyBomb.tscn", 
        Category = "特殊", Team = "Player1", DisplayColor = new Color(0.9f, 0.3f, 0.1f), 
        Description = "自爆单位，3格范围50%HP伤害，空军移动力9" },
};

private readonly List<UnitTemplate> weaponTemplates = new()
{
    new UnitTemplate { DisplayName = "⚫ 黑炮", ScenePath = "res://Prefabs/black_cannon.tscn", 
        Category = "兵器", Team = "Player1", DisplayColor = new Color(0.3f, 0.3f, 0.3f), 
        Description = "三角形范围兵器，固定百分比伤害" },
    new UnitTemplate { DisplayName = "🔴 激光炮", ScenePath = "res://Prefabs/Laser.tscn", 
        Category = "兵器", Team = "Player1", DisplayColor = new Color(0.9f, 0.2f, 0.2f), 
        Description = "直线激光兵器，穿透攻击，可配置角度/射程/冷却" },
};

private readonly List<UnitTemplate> facilityTemplates = new()
{
    new UnitTemplate { DisplayName = "🏙️ City", ScenePath = "res://Prefabs/City.tscn", 
        Category = "设施", Team = "Player0", DisplayColor = new Color(0.8f, 0.6f, 0.2f), 
        Description = "城市设施，可占领并提供补给" },
};

// UI 引用
private Panel spawnMenuPanel;
private VBoxContainer spawnButtonContainer;
private Label spawnTitleLabel;
private Button spawnModeUnitBtn;
private Button spawnModeWeaponBtn;
private Button spawnModeFacilityBtn;
private Button destroyModeBtn;
private Label spawnHintLabel;
private HBoxContainer spawnModeSelector;
public override void _Ready()
{
    AddToGroup("terrain_editor");

    if (gameManager == null)
        gameManager = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
    if (gridManager == null)
        gridManager = GetTree().GetFirstNodeInGroup("grid_manager") as GridManager;

    CreateModeToggleButton();
    CreateSelectionMenu();
    CreateTerrainMenu();
    CreatePropertyEditor();
    CreateSpawnMenu();
    CreateGlobalConfigPanel();

    var viewportSize = GetViewport().GetVisibleRect().Size;
    modeToggleButton.SetAnchorsPreset(LayoutPreset.TopLeft);
    modeToggleButton.Position = new Vector2(viewportSize.X - 160, viewportSize.Y - 70);

    
}

// ========== 修改 _Input 添加 ESC 关闭生成菜单 ==========
public override void _Input(InputEvent @event)
{
    if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
    {
        if (globalConfigPanel?.Visible == true)
        {
            CloseGlobalConfigPanel();
            return;
        }

        if (spawnMenuPanel?.Visible == true)
        {
            CloseSpawnMenu();
            return;
        }
        if (propertyEditorPanel?.Visible == true)
        {
            ClosePropertyEditor();
            return;
        }
        if (terrainMenuPanel?.Visible == true)
        {
            CloseTerrainMenu();
            return;
        }
        if (selectionMenuPanel?.Visible == true)
        {
            CloseSelectionMenu();
            isMenuOpen = false;
            return;
        }

        // ESC 打开全局配置面板
        OpenGlobalConfigPanel();
    }
}
// ========== 创建生成菜单 ==========
private void CreateSpawnMenu()
{
    spawnMenuPanel = new Panel();
    spawnMenuPanel.Name = "SpawnMenuPanel";
    spawnMenuPanel.CustomMinimumSize = new Vector2(400, 600);
    spawnMenuPanel.SetAnchorsPreset(LayoutPreset.Center);
    spawnMenuPanel.Visible = false;
    spawnMenuPanel.ZIndex = 1200;

    var panelStyle = new StyleBoxFlat();
    panelStyle.BgColor = new Color(0.1f, 0.12f, 0.18f, 0.97f);
    panelStyle.SetCornerRadiusAll(16);
    panelStyle.SetBorderWidthAll(3);
    panelStyle.BorderColor = new Color(0.4f, 0.7f, 0.9f);
    spawnMenuPanel.AddThemeStyleboxOverride("panel", panelStyle);

    // 标题
    spawnTitleLabel = new Label();
    spawnTitleLabel.Text = "🎮 单位/兵器/设施生成器";
    spawnTitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
    spawnTitleLabel.AddThemeFontSizeOverride("font_size", 20);
    spawnTitleLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.9f, 1f));
    spawnTitleLabel.CustomMinimumSize = new Vector2(0, 35);
    spawnTitleLabel.Position = new Vector2(0, 12);
    spawnTitleLabel.Size = new Vector2(320, 35);
    spawnMenuPanel.AddChild(spawnTitleLabel);


    // 模式选择器
    spawnModeSelector = new HBoxContainer();
    spawnModeSelector.Position = new Vector2(15, 50);
    spawnModeSelector.Size = new Vector2(370, 40);
    spawnModeSelector.AddThemeConstantOverride("separation", 8);

    spawnModeUnitBtn = new Button();
    spawnModeUnitBtn.Text = "🚶 生成单位";
    spawnModeUnitBtn.CustomMinimumSize = new Vector2(90, 36);
    spawnModeUnitBtn.AddThemeFontSizeOverride("font_size", 12);
    spawnModeUnitBtn.Pressed += () => SwitchSpawnMode(SpawnMode.Unit);
    spawnModeSelector.AddChild(spawnModeUnitBtn);

    spawnModeWeaponBtn = new Button();
    spawnModeWeaponBtn.Text = "⚔️ 生成兵器";
    spawnModeWeaponBtn.CustomMinimumSize = new Vector2(90, 36);
    spawnModeWeaponBtn.AddThemeFontSizeOverride("font_size", 12);
    spawnModeWeaponBtn.Pressed += () => SwitchSpawnMode(SpawnMode.Weapon);
    spawnModeSelector.AddChild(spawnModeWeaponBtn);

    spawnModeFacilityBtn = new Button();
    spawnModeFacilityBtn.Text = "🏙️ 生成设施";
    spawnModeFacilityBtn.CustomMinimumSize = new Vector2(90, 36);
    spawnModeFacilityBtn.AddThemeFontSizeOverride("font_size", 12);
    spawnModeFacilityBtn.Pressed += () => SwitchSpawnMode(SpawnMode.Facility);
    spawnModeSelector.AddChild(spawnModeFacilityBtn);

    destroyModeBtn = new Button();
    destroyModeBtn.Text = "💀 销毁模式";
    destroyModeBtn.CustomMinimumSize = new Vector2(90, 36);
    destroyModeBtn.AddThemeFontSizeOverride("font_size", 12);
    destroyModeBtn.Pressed += ToggleDestroyMode;
    spawnModeSelector.AddChild(destroyModeBtn);

    spawnMenuPanel.AddChild(spawnModeSelector);

    // 提示标签
    spawnHintLabel = new Label();
    spawnHintLabel.Text = "选择模式开始...";
    spawnHintLabel.HorizontalAlignment = HorizontalAlignment.Center;
    spawnHintLabel.AddThemeFontSizeOverride("font_size", 12);
    spawnHintLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.6f));
    spawnHintLabel.Position = new Vector2(10, 95);
    spawnHintLabel.Size = new Vector2(380, 25);
    spawnMenuPanel.AddChild(spawnHintLabel);

    // 滚动容器
    var scroll = new ScrollContainer();
    scroll.Position = new Vector2(15, 125);
    scroll.Size = new Vector2(370, 460);
    scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

    spawnButtonContainer = new VBoxContainer();
    spawnButtonContainer.AddThemeConstantOverride("separation", 6);
    spawnButtonContainer.Size = new Vector2(370, 0);

    scroll.AddChild(spawnButtonContainer);
    spawnMenuPanel.AddChild(scroll);

    var dragHandle = new Panel();
    dragHandle.Name = "DragHandle";
    dragHandle.CustomMinimumSize = new Vector2(400, 35);
    dragHandle.Position = new Vector2(0, 0);
    dragHandle.MouseFilter = MouseFilterEnum.Pass;
    spawnMenuPanel.AddChild(dragHandle);

    spawnTitleLabel.Position = new Vector2(0, 12 + 35);
    spawnTitleLabel.Size = new Vector2(400, 35);

    var closeBtn = new Button();
    closeBtn.Name = "CloseButton";
    closeBtn.Text = "✕";
    closeBtn.CustomMinimumSize = new Vector2(32, 32);
    closeBtn.Position = new Vector2(360, 10);
    closeBtn.ZIndex = 10;
    closeBtn.MouseFilter = MouseFilterEnum.Stop;
    closeBtn.Pressed += CloseSpawnMenu;
    spawnMenuPanel.AddChild(closeBtn);

    AddChild(spawnMenuPanel);
    MakeMenuDraggable(spawnMenuPanel, dragHandle);

}

private bool isDestroyMode = false;

private void ToggleDestroyMode()
{
    isDestroyMode = !isDestroyMode;
    destroyModeBtn.Text = isDestroyMode ? "❌ 退出销毁" : "💀 销毁模式";

    if (isDestroyMode)
    {
        spawnHintLabel.Text = "💀 销毁模式：点击格子上的单位/兵器即可销毁";
        spawnHintLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
        currentSpawnMode = SpawnMode.None;
        selectedTemplatePath = "";
        UpdateModeButtonStyles();
    }
    else
    {
        spawnHintLabel.Text = "选择模式开始...";
        spawnHintLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.6f));
    }
}

private string GetFriendlyTypeName(Infantry unit)
{
    return unit switch
    {
        Mech => "机甲",
        MdTank => "重坦",
        LightTank => "轻坦",
        Recon => "侦察车",
        Artillery => "火炮",
        Rocket => "火箭",
        AntiAir => "防空高炮",
        APC => "运输",
        Flare => "照明车",
        Bike => "摩托兵",
        Oozium => "史莱姆",
        Infantry => "步兵",
        _ => unit.GetType().Name
    };
}

private string GetFriendlyWeaponName(Weapon weapon)
{
    return weapon switch
    {
        BlackCannon => "黑炮",
        Laser => "激光炮",
        _ => weapon.GetType().Name
    };
}


private void SwitchSpawnMode(SpawnMode mode)
{
    currentSpawnMode = mode;
    isDestroyMode = false;
    destroyModeBtn.Text = "💀 销毁模式";

    UpdateModeButtonStyles();
    RefreshSpawnButtons();
}

private void UpdateModeButtonStyles()
{
    ResetButtonStyle(spawnModeUnitBtn);
    ResetButtonStyle(spawnModeWeaponBtn);
    ResetButtonStyle(spawnModeFacilityBtn);
    ResetButtonStyle(destroyModeBtn);

    if (currentSpawnMode == SpawnMode.Unit)
        HighlightButton(spawnModeUnitBtn, new Color(0.3f, 0.6f, 0.3f));
    else if (currentSpawnMode == SpawnMode.Weapon)
        HighlightButton(spawnModeWeaponBtn, new Color(0.3f, 0.4f, 0.6f));
    else if (currentSpawnMode == SpawnMode.Facility)
        HighlightButton(spawnModeFacilityBtn, new Color(0.8f, 0.6f, 0.2f));
    else if (isDestroyMode)
        HighlightButton(destroyModeBtn, new Color(0.6f, 0.2f, 0.2f));
}

private void ResetButtonStyle(Button btn)
{
    var style = new StyleBoxFlat();
    style.BgColor = new Color(0.25f, 0.25f, 0.3f, 0.8f);
    style.SetCornerRadiusAll(6);
    btn.AddThemeStyleboxOverride("normal", style);
}

private void HighlightButton(Button btn, Color color)
{
    var style = new StyleBoxFlat();
    style.BgColor = new Color(color.R, color.G, color.B, 0.9f);
    style.SetCornerRadiusAll(6);
    btn.AddThemeStyleboxOverride("normal", style);
}

private void RefreshSpawnButtons()
{
    foreach (var child in spawnButtonContainer.GetChildren())
        child.QueueFree();

    selectedTemplatePath = "";
    selectedTemplateCategory = "";

    if (currentSpawnMode == SpawnMode.None) return;

    List<UnitTemplate> templates;
    if (currentSpawnMode == SpawnMode.Unit)
        templates = unitTemplates;
    else if (currentSpawnMode == SpawnMode.Weapon)
        templates = weaponTemplates;
    else
        templates = facilityTemplates;

    var grouped = templates.GroupBy(t => t.Category).OrderBy(g => g.Key);

    foreach (var group in grouped)
    {
        var categoryLabel = new Label();
        categoryLabel.Text = $"=== {group.Key} ===";
        categoryLabel.AddThemeFontSizeOverride("font_size", 14);
        categoryLabel.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.3f));
        categoryLabel.CustomMinimumSize = new Vector2(0, 25);
        spawnButtonContainer.AddChild(categoryLabel);

        foreach (var template in group)
        {
            var btn = CreateTemplateButton(template);
            spawnButtonContainer.AddChild(btn);
        }
    }

    spawnHintLabel.Text = currentSpawnMode switch
    {
        SpawnMode.Unit => "🚶 选择单位模板，然后点击地图格子生成",
        SpawnMode.Weapon => "⚔️ 选择兵器模板，然后点击地图格子生成",
        SpawnMode.Facility => "🏙️ 选择设施模板，然后点击地图格子生成",
        _ => "选择模式开始..."
    };
    spawnHintLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.9f, 0.6f));
}

private Button CreateTemplateButton(UnitTemplate template)
{
    var btn = new Button();
    btn.CustomMinimumSize = new Vector2(270, 50);
    btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;

    btn.Text = $"{template.DisplayName}\n{template.Description}";
    btn.AddThemeFontSizeOverride("font_size", 12);
    btn.AddThemeColorOverride("font_color", Colors.White);
    btn.Alignment = HorizontalAlignment.Left;

    var normalStyle = new StyleBoxFlat();
    normalStyle.BgColor = new Color(template.DisplayColor.R * 0.3f, template.DisplayColor.G * 0.3f, 
        template.DisplayColor.B * 0.3f, 0.8f);
    normalStyle.SetCornerRadiusAll(8);
    btn.AddThemeStyleboxOverride("normal", normalStyle);

    var hoverStyle = new StyleBoxFlat();
    hoverStyle.BgColor = new Color(template.DisplayColor.R * 0.5f, template.DisplayColor.G * 0.5f, 
        template.DisplayColor.B * 0.5f, 0.9f);
    hoverStyle.SetCornerRadiusAll(8);
    btn.AddThemeStyleboxOverride("hover", hoverStyle);

    var pressedStyle = new StyleBoxFlat();
    pressedStyle.BgColor = new Color(template.DisplayColor.R * 0.7f, template.DisplayColor.G * 0.7f, 
        template.DisplayColor.B * 0.7f, 1.0f);
    pressedStyle.SetCornerRadiusAll(8);
    btn.AddThemeStyleboxOverride("pressed", pressedStyle);

    var teamHBox = new HBoxContainer();
    teamHBox.Position = new Vector2(180, 12);
    teamHBox.Size = new Vector2(100, 26);
    teamHBox.AddThemeConstantOverride("separation", 4);

    var p1Btn = new Button();
    p1Btn.Text = "P1";
    p1Btn.CustomMinimumSize = new Vector2(36, 24);
    p1Btn.AddThemeFontSizeOverride("font_size", 10);
    p1Btn.Pressed += () => {
        template.Team = "Player1";
        UpdateTemplateTeamIndicator(btn, "Player1");
    };
    teamHBox.AddChild(p1Btn);

    var p2Btn = new Button();
    p2Btn.Text = "P2";
    p2Btn.CustomMinimumSize = new Vector2(36, 24);
    p2Btn.AddThemeFontSizeOverride("font_size", 10);
    p2Btn.Pressed += () => {
        template.Team = "Player2";
        UpdateTemplateTeamIndicator(btn, "Player2");
    };
    teamHBox.AddChild(p2Btn);

    var p0Btn = new Button();
    p0Btn.Text = "P0";
    p0Btn.CustomMinimumSize = new Vector2(30, 24);
    p0Btn.AddThemeFontSizeOverride("font_size", 10);
    p0Btn.Pressed += () => {
        template.Team = "Player0";
        UpdateTemplateTeamIndicator(btn, "Player0");
    };
    teamHBox.AddChild(p0Btn);

    var pN1Btn = new Button();
    pN1Btn.Text = "P-1";
    pN1Btn.CustomMinimumSize = new Vector2(30, 24);
    pN1Btn.AddThemeFontSizeOverride("font_size", 10);
    pN1Btn.Pressed += () => {
        template.Team = "Player";
        UpdateTemplateTeamIndicator(btn, "Player");
    };
    teamHBox.AddChild(pN1Btn);

    btn.AddChild(teamHBox);

    btn.Pressed += () => {
        selectedTemplatePath = template.ScenePath;
        selectedTemplateCategory = template.Category;
        spawnHintLabel.Text = $"✅ 已选择: {template.DisplayName} ({template.Team})\n点击地图格子生成...";
        spawnHintLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));

        foreach (var child in spawnButtonContainer.GetChildren())
        {
            if (child is Button b && b != btn)
                ResetTemplateButtonStyle(b);
        }
        HighlightTemplateButton(btn, template.DisplayColor);
    };

    return btn;
}

private void UpdateTemplateTeamIndicator(Button btn, string team)
{
    var teamLabel = btn.GetNodeOrNull<Label>("TeamLabel");
    if (teamLabel == null)
    {
        teamLabel = new Label();
        teamLabel.Name = "TeamLabel";
        teamLabel.Position = new Vector2(220, 4);
        teamLabel.Size = new Vector2(60, 20);
        teamLabel.AddThemeFontSizeOverride("font_size", 10);
        btn.AddChild(teamLabel);
    }
    teamLabel.Text = team switch {
        "Player1" => "🔴P1",
        "Player2" => "🔵P2",
        "Player0" => "⚪P0",
        "Player" => "🟣P-1",
        _ => team
    };
    teamLabel.AddThemeColorOverride("font_color", team switch {
        "Player1" => new Color(1, 0.3f, 0.3f),
        "Player2" => new Color(0.3f, 0.5f, 1f),
        "Player0" => new Color(0.7f, 0.7f, 0.7f),
        "Player" => new Color(0.7f, 0.4f, 0.9f),
        _ => Colors.White
    });
}

private void ResetTemplateButtonStyle(Button btn)
{
    var style = new StyleBoxFlat();
    style.BgColor = new Color(0.25f, 0.25f, 0.3f, 0.6f);
    style.SetCornerRadiusAll(8);
    btn.AddThemeStyleboxOverride("normal", style);
}

private void HighlightTemplateButton(Button btn, Color color)
{
    var style = new StyleBoxFlat();
    style.BgColor = new Color(color.R * 0.6f, color.G * 0.6f, color.B * 0.6f, 0.95f);
    style.SetCornerRadiusAll(8);
    style.SetBorderWidthAll(2);
    style.BorderColor = Colors.Yellow;
    btn.AddThemeStyleboxOverride("normal", style);
}

private void SpawnUnitAtGrid(Grids grid, string scenePath, string team)
{
    if (string.IsNullOrEmpty(scenePath) || grid == null) return;

    var scene = GD.Load<PackedScene>(scenePath);
    if (scene == null)
    {
        GD.PrintErr($"[Spawn] 无法加载场景: {scenePath}");
        spawnHintLabel.Text = $"❌ 错误：无法加载场景\n{scenePath}";
        spawnHintLabel.AddThemeColorOverride("font_color", Colors.Red);
        return;
    }

    try
    {
        if (grid.weapon != null)
        {
            spawnHintLabel.Text = "❌ 该格子已有兵器，不能放置单位";
            spawnHintLabel.AddThemeColorOverride("font_color", Colors.Red);
            return;
        }

        var unit = scene.Instantiate<Infantry>();
        if (unit == null)
        {
            spawnHintLabel.Text = "❌ 实例化失败";
            return;
        }

        unit.Position = grid.Position;
        unit.team = team;
        int unitNumber = gameManager.unitManager.AllUnits.Count + 1;
        string typeName = GetFriendlyTypeName(unit);
        unit.Name = $"{team}_{typeName}_{unitNumber}";

        gameManager.unitManager.units.AddChild(unit);

        bool bound = gameManager.unitManager.BindUnitToGrid(unit, true);
        if (!bound)
        {
            unit.QueueFree();
            spawnHintLabel.Text = "❌ 绑定格子失败";
            return;
        }

        gameManager.RegisterUnit(unit, GetUnitCategory(unit));
        unit.OnClickPiece = gameManager.OnSelectPiece;
        unit.UpdateTeamVisual();
        unit.UpdateHpLabel();
        if (unit.sprite != null)
            unit.StartBreath();

        gameManager.UpdateUnitLists();
        gameManager.RefreshSpecializedUnitLists();

        spawnHintLabel.Text = $"✅ 生成成功: {unit.Name}";
        spawnHintLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));

        GD.Print($"[Spawn] 生成单位: {unit.Name} at {grid.GridIndex}, team={team}");
    }
    catch (Exception e)
    {
        GD.PrintErr($"[Spawn] 生成失败: {e.Message}");
        spawnHintLabel.Text = $"❌ 生成失败: {e.Message}";
        spawnHintLabel.AddThemeColorOverride("font_color", Colors.Red);
    }
    gameManager.unitManager.RefreshUnitList();
    gameManager.RefreshSpecializedUnitLists();
    gameManager.UpdateUnitLists();
}

private void SpawnWeaponAtGrid(Grids grid, string scenePath, string team)
{
    if (string.IsNullOrEmpty(scenePath) || grid == null) return;

    var scene = GD.Load<PackedScene>(scenePath);
    if (scene == null)
    {
        GD.PrintErr($"[Spawn] 无法加载场景: {scenePath}");
        return;
    }

    try
    {
        if (grid.weapon != null)
        {
            spawnHintLabel.Text = "❌ 该格子已有兵器";
            spawnHintLabel.AddThemeColorOverride("font_color", Colors.Red);
            return;
        }

        var blockingUnit = grid.infantries.FirstOrDefault(u => 
            u != null && IsInstanceValid(u) && u.overlapType == UnitOverlapType.NonOverlapping);
        if (blockingUnit != null)
        {
            spawnHintLabel.Text = $"❌ 该格子被 {blockingUnit.Name} 占据";
            spawnHintLabel.AddThemeColorOverride("font_color", Colors.Red);
            return;
        }

        var weapon = scene.Instantiate<Weapon>();
        if (weapon == null)
        {
            spawnHintLabel.Text = "❌ 兵器实例化失败";
            return;
        }

        weapon.Position = grid.Position;
        weapon.team = team;
        weapon.Name = $"{team}_{weapon.GetType().Name}_{gameManager.weaponManager.AllWeapons.Count}";

        gameManager.weaponManager.weaponsNode.AddChild(weapon);

        bool bound = gameManager.weaponManager.BindWeaponToGrid(weapon, true);
        if (!bound)
        {
            weapon.QueueFree();
            spawnHintLabel.Text = "❌ 兵器绑定格子失败";
            return;
        }
        gameManager.weaponManager.AllWeapons.Add(weapon);
        weapon.OnClickWeapon = gameManager.OnSelectWeapon;
        weapon.UpdateHpLabel();
        if (weapon is BlackCannon cannon)
        {
            cannon.UpdateDirectionVisual();
            cannon.UpdateAmmoVisual();
        }

        gameManager.UpdateUnitLists();

        spawnHintLabel.Text = $"✅ 生成兵器: {weapon.Name}";
        spawnHintLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));

        GD.Print($"[Spawn] 生成兵器: {weapon.Name} at {grid.GridIndex}, team={team}");
    }
    catch (Exception e)
    {
        GD.PrintErr($"[Spawn] 兵器生成失败: {e.Message}");
        spawnHintLabel.Text = $"❌ 生成失败: {e.Message}";
        spawnHintLabel.AddThemeColorOverride("font_color", Colors.Red);
    }
}

private void DestroyTargetAtGrid(Grids grid)
{
    if (grid == null) return;

    var allTargets = new List<object>();

    foreach (var unit in grid.infantries.ToList())
    {
        if (unit != null && IsInstanceValid(unit))
            allTargets.Add(unit);
    }

    foreach (var weapon in grid.weapons.ToList())
    {
        if (weapon != null && IsInstanceValid(weapon))
            allTargets.Add(weapon);
    }

    if (grid.city != null && IsInstanceValid(grid.city))
    {
        allTargets.Add(grid.city);
    }

    if (allTargets.Count == 0)
    {
        spawnHintLabel.Text = "⚠️ 该格子没有可销毁的目标";
        return;
    }

    if (allTargets.Count == 1)
    {
        ExecuteDestroy(allTargets[0]);
    }
    else
    {
        ShowDestroySelectionMenu(grid, allTargets);
    }
}

private void ExecuteDestroy(object target)
{
    try
    {
        if (target is Infantry unit)
        {
            string name = unit.Name;
            gameManager.RemoveUnit(unit);
            spawnHintLabel.Text = $"💀 已销毁单位: {name}";
        }
        else if (target is Weapon weapon)
        {
            string name = weapon.Name;
            gameManager.RemoveWeapon(weapon);
            spawnHintLabel.Text = $"💀 已销毁兵器: {name}";
        }
        else if (target is Facility facility)
        {
            string name = facility.Name;
            if (facility.ParentGrid != null)
            {
                facility.ParentGrid.city = null;
            }
            facility.QueueFree();
            spawnHintLabel.Text = $"💀 已销毁设施: {name}";
            gameManager.UpdateUnitLists();
        }

        spawnHintLabel.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
        gameManager.UpdateUnitLists();
    }
    catch (Exception e)
    {
        GD.PrintErr($"[Destroy] 销毁失败: {e.Message}");
        spawnHintLabel.Text = $"❌ 销毁失败: {e.Message}";
    }
}

private void ShowDestroySelectionMenu(Grids grid, List<object> targets)
{
    foreach (var child in selectionButtonContainer.GetChildren())
        child.QueueFree();

    selectionTitleLabel.Text = $"💀 选择要销毁的目标 ({targets.Count}个)";

    foreach (var target in targets)
    {
        string displayName = "";
        Color color = Colors.White;
        Action onClick = null;

        if (target is Infantry unit)
        {
            string teamEmoji = unit.team switch { "Player1" => "🔴", "Player2" => "🔵", "Player0" => "⚪", "Player" => "🟣", _ => "⚪" };
            displayName = $"{teamEmoji} [单位:{unit.GetType().Name}] {unit.Name} HP:{Mathf.CeilToInt(unit.health/10f)}";
            color = new Color(0.8f, 0.3f, 0.3f);
            onClick = () => {
                ExecuteDestroy(unit);
                CloseSelectionMenu();
            };
        }
        else if (target is Weapon weapon)
        {
            string teamEmoji = weapon.team switch { "Player1" => "🔴", "Player2" => "🔵", "Player0" => "⚪", "Player" => "🟣", _ => "⚪" };
            displayName = $"{teamEmoji} [兵器:{weapon.GetType().Name}] {weapon.Name} HP:{Mathf.CeilToInt(weapon.health/10f)}";
            color = new Color(0.6f, 0.2f, 0.2f);
            onClick = () => {
                ExecuteDestroy(weapon);
                CloseSelectionMenu();
            };
        }
        else if (target is Facility facility)
        {
            string teamEmoji = facility.facilityTeam switch { "Player1" => "🔴", "Player2" => "🔵", "Player0" => "⚪", "Player" => "🟣", _ => "⚪" };
            displayName = $"{teamEmoji} [设施:{facility.GetType().Name}] {facility.Name} (Team:{facility.facilityTeam})";
            color = new Color(0.8f, 0.5f, 0.2f);
            onClick = () => {
                ExecuteDestroy(facility);
                CloseSelectionMenu();
            };
        }

        var btn = CreateSelectionButton(displayName, color, onClick);
        selectionButtonContainer.AddChild(btn);
    }

    var cancelBtn = CreateSelectionButton("❌ 取消", new Color(0.5f, 0.5f, 0.5f), CloseSelectionMenu);
    selectionButtonContainer.AddChild(cancelBtn);

    var viewportSize = GetViewport().GetVisibleRect().Size;
    selectionMenuPanel.Position = new Vector2(
        (viewportSize.X - selectionMenuPanel.Size.X) / 2,
        (viewportSize.Y - selectionMenuPanel.Size.Y) / 2
    );
    selectionMenuPanel.Visible = true;
    isMenuOpen = true;
}

private UnitCategory GetUnitCategory(Infantry unit)
{
    return unit switch
    {
        Mech _ => UnitCategory.Mech,
        Oozium _ => UnitCategory.Oozium,
        LightTank _ => UnitCategory.Tank,
        Recon _ => UnitCategory.Recon,
        APC _ => UnitCategory.APC,
        Flare _ => UnitCategory.Vehicle,
        Bike _ => UnitCategory.Infantry,
        Artillery _ => UnitCategory.Artillery,
        Rocket _ => UnitCategory.Rocket,
        FlyBomb _ => UnitCategory.FlyBomb,
        Infantry _ => UnitCategory.Infantry,
        _ => UnitCategory.Other
    };
}
// ========== 菜单控制 ==========
private void OpenSpawnMenu()
{
    CloseAllMenus();
    spawnMenuPanel.Visible = true;
    isMenuOpen = true;
    currentSpawnMode = SpawnMode.None;
    isDestroyMode = false;
    selectedTemplatePath = "";
    UpdateModeButtonStyles();

    foreach (var child in spawnButtonContainer.GetChildren())
        child.QueueFree();

    spawnHintLabel.Text = "选择生成模式...";
}

private void CloseSpawnMenu()
{
    spawnMenuPanel.Visible = false;
    currentSpawnMode = SpawnMode.None;
    isDestroyMode = false;
    selectedTemplatePath = "";
    isMenuOpen = false;
}

// ========== 修改后的格子点击处理（整合生成系统）=========
public void OnGridClickedForEdit(Grids grid)
{
    if (!IsEditMode || grid == null) return;

    selectedGrid = grid;

    // 优先处理生成模式
    if (currentSpawnMode != SpawnMode.None && !string.IsNullOrEmpty(selectedTemplatePath))
    {
        if (currentSpawnMode == SpawnMode.Unit)
            SpawnUnitAtGrid(grid, selectedTemplatePath, unitTemplates.First(t => t.ScenePath == selectedTemplatePath).Team);
        else if (currentSpawnMode == SpawnMode.Weapon)
            SpawnWeaponAtGrid(grid, selectedTemplatePath, weaponTemplates.First(t => t.ScenePath == selectedTemplatePath).Team);
        else if (currentSpawnMode == SpawnMode.Facility)
            SpawnFacilityAtGrid(grid, selectedTemplatePath, facilityTemplates.First(t => t.ScenePath == selectedTemplatePath).Team);
        return;
    }

    // 处理销毁模式
    if (isDestroyMode)
    {
        DestroyTargetAtGrid(grid);
        return;
    }

    // 原有逻辑：收集所有可编辑对象
    var allTargets = new List<EditTarget>();

    var units = grid.infantries?
        .Where(u => u != null && IsInstanceValid(u))
        .OrderByDescending(u => u.health)
        .ToList() ?? new List<Infantry>();

    foreach (var unit in units)
    {
        string teamEmoji = unit.team switch { "Player1" => "🔴", "Player2" => "🔵", "Player0" => "⚪", "Player" => "🟣", _ => "⚪" };
        string hpText = unit.maxHealth <= 999 ? $"HP:{Mathf.CeilToInt(unit.health / 10f)}" : $"HP:{unit.health}";
        allTargets.Add(new EditTarget
        {
            TargetType = "unit",
            DisplayName = $"{teamEmoji} [单位:{unit.GetType().Name}] {unit.Name} ({hpText})",
            TargetObject = unit,
            Color = new Color(0.3f, 0.5f, 0.8f)
        });
    }

    var weapons = grid.weapons?
        .Where(w => w != null && IsInstanceValid(w))
        .ToList() ?? new List<Weapon>();

    foreach (var weapon in weapons)
    {
        string teamEmoji = weapon.team switch { "Player1" => "🔴", "Player2" => "🔵", "Player0" => "⚪", "Player" => "🟣", _ => "⚪" };
        string hpText = weapon.maxHealth <= 999 ? $"HP:{Mathf.CeilToInt(weapon.health / 10f)}" : $"HP:{weapon.health}";
        allTargets.Add(new EditTarget
        {
            TargetType = "weapon",
            DisplayName = $"{teamEmoji} [兵器:{weapon.GetType().Name}] {weapon.Name} ({hpText})",
            TargetObject = weapon,
            Color = new Color(0.7f, 0.4f, 0.3f)
        });
    }

    // 添加功能选项
    allTargets.Add(new EditTarget
    {
        TargetType = "grid",
        DisplayName = "🟫 修改格子地形/属性",
        TargetObject = grid,
        Color = new Color(0.4f, 0.7f, 0.4f)
    });

    allTargets.Add(new EditTarget
    {
        TargetType = "spawn",
        DisplayName = "🎮 生成新单位/兵器...",
        TargetObject = null,
        Color = new Color(0.4f, 0.6f, 0.9f)
    });

    if (allTargets.Count == 1)
    {
        OpenTerrainMenu(grid);
    }
    else
    {
        ShowMultiSelectMenu(allTargets);
    }
}

// 修改多选菜单，添加生成分支
private void ShowMultiSelectMenu(List<EditTarget> targets)
{
    foreach (var child in selectionButtonContainer.GetChildren())
        child.QueueFree();

    int objectCount = targets.Count(t => t.TargetType != "grid" && t.TargetType != "spawn");
    selectionTitleLabel.Text = $"格子 {selectedGrid.GridIndex} - 选择操作 ({objectCount}个对象)";

    foreach (var target in targets)
    {
        Button btn;
        switch (target.TargetType)
        {
            case "unit":
                btn = CreateSelectionButton(target.DisplayName, target.Color, () => {
                    CloseSelectionMenu();
                    OpenUnitEditor((Infantry)target.TargetObject);
                });
                break;
            case "weapon":
                btn = CreateSelectionButton(target.DisplayName, target.Color, () => {
                    CloseSelectionMenu();
                    OpenWeaponEditor((Weapon)target.TargetObject);
                });
                break;
            case "grid":
                btn = CreateSelectionButton(target.DisplayName, target.Color, () => {
                    CloseSelectionMenu();
                    OpenTerrainMenu(selectedGrid);
                });
                break;
            case "spawn":
                btn = CreateSelectionButton(target.DisplayName, target.Color, () => {
                    CloseSelectionMenu();
                    OpenSpawnMenu();
                });
                break;
            default:
                continue;
        }
        selectionButtonContainer.AddChild(btn);
    }

    var viewportSize = GetViewport().GetVisibleRect().Size;
    selectionMenuPanel.Position = new Vector2(
        (viewportSize.X - selectionMenuPanel.Size.X) / 2,
        (viewportSize.Y - selectionMenuPanel.Size.Y) / 2
    );
    selectionMenuPanel.Visible = true;
    isMenuOpen = true;
}

// 新增：打开兵器编辑器
private void OpenWeaponEditor(Weapon weapon)
{
    currentEditingTarget = weapon;
    propertyEditorTitle.Text = $"编辑兵器: {weapon.Name} ({weapon.GetType().Name})";

    BuildPropertyEditor(weapon);

    var viewportSize = GetViewport().GetVisibleRect().Size;
    propertyEditorPanel.Position = new Vector2(
        (viewportSize.X - propertyEditorPanel.Size.X) / 2,
        (viewportSize.Y - propertyEditorPanel.Size.Y) / 2
    );
    propertyEditorPanel.Visible = true;
    isMenuOpen = true;
}

// TerrainEditor.cs - 修改 CreateMemberControl，确保修改 team 时调用 RefreshTeamVisual
private void CreateMemberControl(object target, string name, Type memberType, Func<object> getter, Action<object> setter)
{
    var hbox = new HBoxContainer();
    hbox.CustomMinimumSize = new Vector2(0, 32);
    hbox.AddThemeConstantOverride("separation", 8);

    var nameLabel = new Label();
    nameLabel.Text = name;
    nameLabel.CustomMinimumSize = new Vector2(150, 28);
    nameLabel.AddThemeFontSizeOverride("font_size", 12);
    nameLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.9f));
    hbox.AddChild(nameLabel);

    var value = getter();

    Action<object> wrappedSetter = (newValue) => {
        setter(newValue);

        if (name == "team" && target is Node2D node)
        {
            RefreshTeamVisual(target);
        }
        else
        {
            OnPropertyChanged(target, name);
        }
    };

    Control editor = CreateEditorForType(memberType, value, getter, wrappedSetter);

    if (editor != null)
    {
        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.AddChild(editor);
    }

    propertyContainer.AddChild(hbox);
}

private Control CreateEditorForType(Type propType, object value, Func<object> getter, Action<object> onChanged)
{
    if (propType == typeof(int))
    {
        var spinBox = new SpinBox();
        spinBox.MinValue = -999999;
        spinBox.MaxValue = 999999;
        spinBox.Value = (int)(value ?? 0);
        spinBox.CustomMinimumSize = new Vector2(100, 28);
        spinBox.AddThemeFontSizeOverride("font_size", 12);
        spinBox.ValueChanged += (newVal) => onChanged((int)newVal);
        return spinBox;
    }

    if (propType == typeof(float))
    {
        var spinBox = new SpinBox();
        spinBox.MinValue = -999999;
        spinBox.MaxValue = 999999;
        spinBox.Step = 0.01;
        spinBox.Value = (float)(value ?? 0f);
        spinBox.CustomMinimumSize = new Vector2(100, 28);
        spinBox.AddThemeFontSizeOverride("font_size", 12);
        spinBox.ValueChanged += (newVal) => onChanged((float)newVal);
        return spinBox;
    }

    if (propType == typeof(bool))
    {
        var checkBox = new CheckBox();
        checkBox.ButtonPressed = (bool)(value ?? false);
        checkBox.CustomMinimumSize = new Vector2(60, 28);
        checkBox.Toggled += (pressed) => onChanged(pressed);
        return checkBox;
    }

    if (propType == typeof(string))
    {
        var lineEdit = new LineEdit();
        lineEdit.Text = (string)(value ?? "");
        lineEdit.CustomMinimumSize = new Vector2(120, 28);
        lineEdit.AddThemeFontSizeOverride("font_size", 12);
        lineEdit.TextChanged += (newText) => onChanged(newText);
        return lineEdit;
    }

    if (propType.IsEnum)
    {
        var optionBtn = new OptionButton();
        var enumNames = Enum.GetNames(propType);

        for (int i = 0; i < enumNames.Length; i++)
        {
            optionBtn.AddItem(enumNames[i], i);
        }

        optionBtn.Selected = (int)(value ?? 0);
        optionBtn.CustomMinimumSize = new Vector2(120, 28);
        optionBtn.AddThemeFontSizeOverride("font_size", 12);
        optionBtn.ItemSelected += (index) => {
            var newValue = Enum.ToObject(propType, (int)index);
            onChanged(newValue);
        };
        return optionBtn;
    }

    if (propType == typeof(Color))
    {
        var colorPicker = new ColorPickerButton();
        colorPicker.Color = (Color)(value ?? Colors.White);
        colorPicker.CustomMinimumSize = new Vector2(80, 28);
        colorPicker.ColorChanged += (newColor) => onChanged(newColor);
        return colorPicker;
    }

    if (propType == typeof(Vector2))
    {
        var vec = (Vector2)(value ?? Vector2.Zero);
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 4);

        var xSpin = new SpinBox { MinValue = -99999, MaxValue = 99999, Step = 0.1, Value = vec.X, CustomMinimumSize = new Vector2(70, 28) };
        var ySpin = new SpinBox { MinValue = -99999, MaxValue = 99999, Step = 0.1, Value = vec.Y, CustomMinimumSize = new Vector2(70, 28) };

        var capturedGetter = getter;
        xSpin.ValueChanged += (v) => {
            var current = (Vector2)(capturedGetter?.Invoke() ?? Vector2.Zero);
            var newVec = new Vector2((float)v, current.Y);
            onChanged(newVec);
        };
        ySpin.ValueChanged += (v) => {
            var current = (Vector2)(capturedGetter?.Invoke() ?? Vector2.Zero);
            var newVec = new Vector2(current.X, (float)v);
            onChanged(newVec);
        };

        hbox.AddChild(new Label { Text = "X:", CustomMinimumSize = new Vector2(15, 20) });
        hbox.AddChild(xSpin);
        hbox.AddChild(new Label { Text = "Y:", CustomMinimumSize = new Vector2(15, 20) });
        hbox.AddChild(ySpin);
        return hbox;
    }

    if (propType == typeof(Vector2I))
    {
        var vec = (Vector2I)(value ?? Vector2I.Zero);
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 4);

        var xSpin = new SpinBox { MinValue = -99999, MaxValue = 99999, Value = vec.X, CustomMinimumSize = new Vector2(70, 28) };
        var ySpin = new SpinBox { MinValue = -99999, MaxValue = 99999, Value = vec.Y, CustomMinimumSize = new Vector2(70, 28) };

        var capturedGetter = getter;
        xSpin.ValueChanged += (v) => {
            var current = (Vector2I)(capturedGetter?.Invoke() ?? Vector2I.Zero);
            onChanged(new Vector2I((int)v, current.Y));
        };
        ySpin.ValueChanged += (v) => {
            var current = (Vector2I)(capturedGetter?.Invoke() ?? Vector2I.Zero);
            onChanged(new Vector2I(current.X, (int)v));
        };

        hbox.AddChild(new Label { Text = "X:", CustomMinimumSize = new Vector2(15, 20) });
        hbox.AddChild(xSpin);
        hbox.AddChild(new Label { Text = "Y:", CustomMinimumSize = new Vector2(15, 20) });
        hbox.AddChild(ySpin);
        return hbox;
    }

    if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Godot.Collections.Dictionary<,>))
    {
        var keyType = propType.GetGenericArguments()[0];
        var valueType = propType.GetGenericArguments()[1];
        var dict = value as System.Collections.IDictionary;

        if (keyType == typeof(float) && valueType == typeof(bool))
        {
            return CreateAngleConfigEditor(
                value as Godot.Collections.Dictionary<float, bool>,
                (newDict) => onChanged(newDict)
            );
        }
        if (keyType == typeof(string) && valueType == typeof(int))
        {
            var dictin = value as Godot.Collections.Dictionary<string, int>;
            bool isPrimary = (currentEditingTarget as Infantry)?.attackMatrix == dict;
            string title = isPrimary ? "主武器" : "副武器";
            Color color = isPrimary ? Colors.Red : Colors.Blue;
            return CreateAttackMatrixEditor(dictin, title, color, (newDict) => onChanged(newDict));
        }

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);

        var label = new Label();
        label.Text = $"Dictionary ({(dict?.Count ?? 0)} items) - 只读";
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 0.5f));
        vbox.AddChild(label);

        if (dict != null)
        {
            foreach (var key in dict.Keys)
            {
                var itemLabel = new Label();
                itemLabel.Text = $"  {key}: {dict[key]}";
                itemLabel.AddThemeFontSizeOverride("font_size", 10);
                itemLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
                vbox.AddChild(itemLabel);
            }
        }
        return vbox;
    }

    if (propType == typeof(Godot.Collections.Array<string>))
    {
        var array = (Godot.Collections.Array<string>)(value ?? new Godot.Collections.Array<string>());
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);

        var textEdit = new TextEdit();
        textEdit.Text = string.Join(",", array);
        textEdit.CustomMinimumSize = new Vector2(120, 60);
        textEdit.AddThemeFontSizeOverride("font_size", 11);

        textEdit.TextChanged += () => {
            var newArray = new Godot.Collections.Array<string>(
                textEdit.Text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToArray()
            );
            onChanged(newArray);
        };

        vbox.AddChild(textEdit);
        return vbox;
    }

    if (typeof(Node).IsAssignableFrom(propType))
    {
        var label = new Label();
        var node = value as Node;
        label.Text = node != null ? $"[{propType.Name}] {node.Name}" : "[null]";
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 0.5f));
        return label;
    }

    var readOnly = new Label();
    readOnly.Text = $"[{propType.Name}] {value ?? "null"}";
    readOnly.AddThemeFontSizeOverride("font_size", 11);
    readOnly.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
    return readOnly;
}
// ========== ✅ 新增：Laser角度配置专用编辑器 ==========
    private Control CreateAngleConfigEditor(Godot.Collections.Dictionary<float, bool> dict, Action<object> onChanged)
    {
        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", 6);

        var titleLabel = new Label();
        titleLabel.Text = "角度配置 (°)";
        titleLabel.AddThemeFontSizeOverride("font_size", 13);
        titleLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        mainVbox.AddChild(titleLabel);

        var hintLabel = new Label();
        hintLabel.Text = "勾选=启用该角度发射";
        hintLabel.AddThemeFontSizeOverride("font_size", 10);
        hintLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.6f));
        mainVbox.AddChild(hintLabel);

        var anglesContainer = new VBoxContainer();
        anglesContainer.Name = "AnglesContainer";
        anglesContainer.AddThemeConstantOverride("separation", 4);
        mainVbox.AddChild(anglesContainer);

        Action refreshAngles = () => { };
        refreshAngles = () => {
            foreach (var child in anglesContainer.GetChildren())
                child.QueueFree();

            if (dict == null || dict.Count == 0)
            {
                var emptyLabel = new Label();
                emptyLabel.Text = "(无配置)";
                emptyLabel.AddThemeFontSizeOverride("font_size", 11);
                emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                anglesContainer.AddChild(emptyLabel);
                return;
            }

            var sortedPairs = dict.OrderBy(kvp => kvp.Key).ToList();
            foreach (var kvp in sortedPairs)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);

                var angleLabel = new Label();
                angleLabel.Text = $"{kvp.Key:F1}°";
                angleLabel.CustomMinimumSize = new Vector2(60, 24);
                angleLabel.AddThemeFontSizeOverride("font_size", 12);
                angleLabel.AddThemeColorOverride("font_color", kvp.Value ? Colors.Green : Colors.Red);
                row.AddChild(angleLabel);

                var checkBox = new CheckBox();
                checkBox.ButtonPressed = kvp.Value;
                checkBox.CustomMinimumSize = new Vector2(24, 24);
                float capturedAngle = kvp.Key;
                bool currentValue = kvp.Value;
                checkBox.Toggled += (pressed) => {
                    dict[capturedAngle] = pressed;
                    onChanged(dict);
                    refreshAngles?.Invoke();
                    if (currentEditingTarget is Laser laser)
                    {
                        if (pressed) laser.EnableAngle(capturedAngle);
                        else laser.DisableAngle(capturedAngle);
                    }
                };
                row.AddChild(checkBox);

                var statusLabel = new Label();
                statusLabel.Text = currentValue ? "✓ 启用" : "✗ 禁用";
                statusLabel.AddThemeFontSizeOverride("font_size", 10);
                statusLabel.AddThemeColorOverride("font_color", currentValue ? new Color(0.3f, 1, 0.3f) : new Color(1, 0.3f, 0.3f));
                row.AddChild(statusLabel);

                var deleteBtn = new Button();
                deleteBtn.Text = "删除";
                deleteBtn.CustomMinimumSize = new Vector2(50, 22);
                deleteBtn.AddThemeFontSizeOverride("font_size", 10);
                float deleteAngle = kvp.Key;
                deleteBtn.Pressed += () => {
                    dict.Remove(deleteAngle);
                    onChanged(dict);
                    refreshAngles?.Invoke();
                    if (currentEditingTarget is Laser laser)
                        laser.DisableAngle(deleteAngle);
                };
                row.AddChild(deleteBtn);

                anglesContainer.AddChild(row);
            }
        };

        refreshAngles();

        var addSeparator = new HSeparator();
        addSeparator.CustomMinimumSize = new Vector2(0, 4);
        mainVbox.AddChild(addSeparator);

        var addTitle = new Label();
        addTitle.Text = "添加新角度:";
        addTitle.AddThemeFontSizeOverride("font_size", 12);
        addTitle.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.3f));
        mainVbox.AddChild(addTitle);

        var addRow = new HBoxContainer();
        addRow.AddThemeConstantOverride("separation", 6);

        var angleSpin = new SpinBox();
        angleSpin.MinValue = 0;
        angleSpin.MaxValue = 359;
        angleSpin.Step = 1;
        angleSpin.Value = 0;
        angleSpin.CustomMinimumSize = new Vector2(80, 28);
        angleSpin.AddThemeFontSizeOverride("font_size", 11);
        addRow.AddChild(angleSpin);

        var addBtn = new Button();
        addBtn.Text = "添加";
        addBtn.CustomMinimumSize = new Vector2(60, 28);
        addBtn.AddThemeFontSizeOverride("font_size", 11);
        addBtn.Pressed += () => {
            float newAngle = (float)angleSpin.Value;
            while (newAngle < 0) newAngle += 360;
            while (newAngle >= 360) newAngle -= 360;

            if (!dict.ContainsKey(newAngle))
            {
                dict[newAngle] = true;
                onChanged(dict);
                refreshAngles?.Invoke();
                if (currentEditingTarget is Laser laser)
                    laser.EnableAngle(newAngle);
            }
        };
        addRow.AddChild(addBtn);

        mainVbox.AddChild(addRow);

        var presetRow = new HBoxContainer();
        presetRow.AddThemeConstantOverride("separation", 4);
        var presetLabel = new Label();
        presetLabel.Text = "快速添加:";
        presetLabel.AddThemeFontSizeOverride("font_size", 10);
        presetRow.AddChild(presetLabel);

        float[] presets = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };
        foreach (var preset in presets)
        {
            var presetBtn = new Button();
            presetBtn.Text = $"{preset:F0}°";
            presetBtn.CustomMinimumSize = new Vector2(40, 24);
            presetBtn.AddThemeFontSizeOverride("font_size", 9);
            float capturedPreset = preset;
            presetBtn.Pressed += () => {
                if (!dict.ContainsKey(capturedPreset))
                {
                    dict[capturedPreset] = true;
                    onChanged(dict);
                    refreshAngles?.Invoke();
                    if (currentEditingTarget is Laser laser)
                        laser.EnableAngle(capturedPreset);
                }
            };
            presetRow.AddChild(presetBtn);
        }
        mainVbox.AddChild(presetRow);

        if (currentEditingTarget is Laser)
        {
            var applyBtn = new Button();
            applyBtn.Text = "🔄 应用角度配置到激光炮";
            applyBtn.CustomMinimumSize = new Vector2(200, 32);
            applyBtn.AddThemeFontSizeOverride("font_size", 12);
            applyBtn.AddThemeColorOverride("font_color", Colors.Yellow);
            var applyStyle = new StyleBoxFlat();
            applyStyle.BgColor = new Color(0.2f, 0.4f, 0.2f, 0.8f);
            applyStyle.SetCornerRadiusAll(6);
            applyBtn.AddThemeStyleboxOverride("normal", applyStyle);
            applyBtn.Pressed += () => {
                if (currentEditingTarget is Laser laser)
                {
                    laser.ApplyAngleConfigChanges();
                    GD.Print($"[TerrainEditor] Laser {laser.Name} 角度配置已应用");
                }
            };
            mainVbox.AddChild(applyBtn);
        }

        return mainVbox;
    }

    private Control CreateAttackMatrixEditor(
    Godot.Collections.Dictionary<string, int> dict, 
    string title,
    Color titleColor,
    Action<object> onChanged)
{
    var mainVbox = new VBoxContainer();
    mainVbox.AddThemeConstantOverride("separation", 4);

    var titleLabel = new Label();
    titleLabel.Text = $"⚔️ {title}";
    titleLabel.AddThemeFontSizeOverride("font_size", 13);
    titleLabel.AddThemeColorOverride("font_color", titleColor);
    mainVbox.AddChild(titleLabel);

    if (title == "主武器")
    {
        var mulLabel = new Label();
        mulLabel.Text = $"反击系数: {((Infantry)currentEditingTarget).counterMul:F1}";
        mulLabel.AddThemeFontSizeOverride("font_size", 10);
        mulLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        mainVbox.AddChild(mulLabel);
    }

    string attackerType = currentEditingTarget?.GetType().Name ?? "Unknown";
    string[] defenderTypes = { "Infantry", "Mech", "LightTank", "Artillery", "Rocket", "APC", "Oozium" };

    foreach (var defenderType in defenderTypes)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);

        var nameLabel = new Label();
        nameLabel.Text = $"→ {defenderType}:";
        nameLabel.CustomMinimumSize = new Vector2(100, 24);
        nameLabel.AddThemeFontSizeOverride("font_size", 11);
        hbox.AddChild(nameLabel);

        string key = $"{attackerType}_{defenderType}";
        int currentValue = dict.GetValueOrDefault(key, 0);

        var spinBox = new SpinBox();
        spinBox.MinValue = 0;
        spinBox.MaxValue = 999;
        spinBox.Value = currentValue;
        spinBox.CustomMinimumSize = new Vector2(80, 24);
        spinBox.AddThemeFontSizeOverride("font_size", 11);

        string capturedKey = key;
        spinBox.ValueChanged += (newVal) => {
            dict[capturedKey] = (int)newVal;
            onChanged(dict);
        };

        hbox.AddChild(spinBox);

        if (currentValue == 0)
        {
            var hintLabel = new Label();
            hintLabel.Text = "-";
            hintLabel.AddThemeColorOverride("font_color", Colors.Red);
            hbox.AddChild(hintLabel);
        }
        else if (title == "主武器")
        {
            float mul = ((Infantry)currentEditingTarget).counterMul;
            int counterVal = Mathf.RoundToInt(currentValue * mul);
            var counterLabel = new Label();
            counterLabel.Text = $"反:{counterVal}";
            counterLabel.AddThemeFontSizeOverride("font_size", 10);
            counterLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 0.5f));
            hbox.AddChild(counterLabel);
        }

        mainVbox.AddChild(hbox);
    }

    return mainVbox;
}

private void ApplyPreset(Godot.Collections.Dictionary<string, int> dict, string attackerType, string preset)
{
    string[] defenders = { "Infantry", "Mech", "LightTank", "Artillery", "Rocket", "APC", "Oozium" };

    foreach (var d in defenders)
    {
        string key = $"{attackerType}_{d}";
        int value = preset switch
        {
            "infantry" => d switch {
                "Infantry" => 55, "Mech" => 45, "LightTank" => 15,
                "Artillery" => 25, "Rocket" => 25, "APC" => 15, "Oozium" => 0, _ => 0
            },
            "armor" => d switch {
                "Infantry" => 75, "Mech" => 70, "LightTank" => 55,
                "Artillery" => 45, "Rocket" => 55, "APC" => 45, "Oozium" => 0, _ => 0
            },
            _ => 50
        };
        dict[key] = value;
    }
}

    // ========== ✅ 创建选择菜单（单位/兵器/格子 多选一） ==========
    private void CreateSelectionMenu()
    {
        selectionMenuPanel = new Panel();
        selectionMenuPanel.Name = "SelectionMenuPanel";
        selectionMenuPanel.CustomMinimumSize = new Vector2(320, 400);
        selectionMenuPanel.SetAnchorsPreset(LayoutPreset.Center);
        selectionMenuPanel.Visible = false;
        selectionMenuPanel.ZIndex = 1000;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.12f, 0.12f, 0.18f, 0.96f);
        panelStyle.SetCornerRadiusAll(16);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.BorderColor = new Color(0.6f, 0.6f, 0.8f);
        selectionMenuPanel.AddThemeStyleboxOverride("panel", panelStyle);

        selectionTitleLabel = new Label();
        selectionTitleLabel.Name = "SelectionTitle";
        selectionTitleLabel.Text = "选择编辑目标";
        selectionTitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        selectionTitleLabel.AddThemeFontSizeOverride("font_size", 20);
        selectionTitleLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        selectionTitleLabel.CustomMinimumSize = new Vector2(0, 35);
        selectionTitleLabel.Position = new Vector2(0, 12);
        selectionTitleLabel.Size = new Vector2(320, 35);
        selectionMenuPanel.AddChild(selectionTitleLabel);


        var closeBtn = new Button();
        closeBtn.Text = "✕";
        closeBtn.CustomMinimumSize = new Vector2(32, 32);
        closeBtn.Position = new Vector2(280, 10);
        closeBtn.Pressed += CloseSelectionMenu;
        selectionMenuPanel.AddChild(closeBtn);

        var scroll = new ScrollContainer();
        scroll.Position = new Vector2(15, 55);
        scroll.Size = new Vector2(290, 330);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

        selectionButtonContainer = new VBoxContainer();
        selectionButtonContainer.Name = "SelectionButtonContainer";
        selectionButtonContainer.AddThemeConstantOverride("separation", 8);
        selectionButtonContainer.Size = new Vector2(290, 0);

        scroll.AddChild(selectionButtonContainer);
        selectionMenuPanel.AddChild(scroll);

        AddChild(selectionMenuPanel);
            var dragHandle = new Panel();
    dragHandle.CustomMinimumSize = new Vector2(320, 30);
    dragHandle.Position = new Vector2(0, 0);
    var handleStyle = new StyleBoxFlat();
    handleStyle.BgColor = new Color(0.3f, 0.3f, 0.4f, 0.5f);
    handleStyle.SetCornerRadiusAll(8);
    dragHandle.AddThemeStyleboxOverride("panel", handleStyle);

    var dragHint = new Label();
    dragHint.Text = "⋮⋮ 拖拽移动";
    dragHint.AddThemeFontSizeOverride("font_size", 10);
    dragHint.AddThemeColorOverride("font_color", new Color(0.6f, 0.7f, 0.8f));
    dragHint.Position = new Vector2(8, 2);
    dragHandle.AddChild(dragHint);
    }

    // ========== ✅ 创建通用属性编辑器（支持字段+属性） ==========
    private void CreatePropertyEditor()
    {
        propertyEditorPanel = new Panel();
        propertyEditorPanel.Name = "PropertyEditorPanel";
        propertyEditorPanel.CustomMinimumSize = new Vector2(380, 600);
        propertyEditorPanel.SetAnchorsPreset(LayoutPreset.Center);
        propertyEditorPanel.Visible = false;
        propertyEditorPanel.ZIndex = 1100;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.1f, 0.12f, 0.16f, 0.97f);
        panelStyle.SetCornerRadiusAll(12);
        panelStyle.SetBorderWidthAll(2);
        panelStyle.BorderColor = new Color(0.4f, 0.7f, 0.4f);
        propertyEditorPanel.AddThemeStyleboxOverride("panel", panelStyle);

        // ✅ 新增：拖拽手柄
        var dragHandle = new Panel();
        dragHandle.Name = "DragHandle";
        dragHandle.CustomMinimumSize = new Vector2(380, 30);
        dragHandle.Position = new Vector2(0, 0);
        var handleStyle = new StyleBoxFlat();
        handleStyle.BgColor = new Color(0.3f, 0.3f, 0.4f, 0.5f);
        handleStyle.SetCornerRadiusAll(8);
        dragHandle.AddThemeStyleboxOverride("panel", handleStyle);
        var dragHint = new Label();
        dragHint.Text = "⋮⋮ 拖拽移动";
        dragHint.AddThemeFontSizeOverride("font_size", 10);
        dragHint.AddThemeColorOverride("font_color", new Color(0.6f, 0.7f, 0.8f));
        dragHint.Position = new Vector2(8, 2);
        dragHandle.AddChild(dragHint);
        propertyEditorPanel.AddChild(dragHandle);
        MakeMenuDraggable(propertyEditorPanel, dragHandle);

        var titleBar = new HBoxContainer();
        titleBar.Position = new Vector2(10, 35);
        titleBar.Size = new Vector2(360, 35);

        propertyEditorTitle = new Label();
        propertyEditorTitle.Name = "PropertyEditorTitle";
        propertyEditorTitle.Text = "属性编辑器";
        propertyEditorTitle.AddThemeFontSizeOverride("font_size", 18);
        propertyEditorTitle.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        propertyEditorTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titleBar.AddChild(propertyEditorTitle);

        propertyEditorCloseBtn = new Button();
        propertyEditorCloseBtn.Text = "✕";
        propertyEditorCloseBtn.CustomMinimumSize = new Vector2(35, 35);
        propertyEditorCloseBtn.Pressed += ClosePropertyEditor;
        titleBar.AddChild(propertyEditorCloseBtn);

        propertyEditorPanel.AddChild(titleBar);

        propertyScrollContainer = new ScrollContainer();
        propertyScrollContainer.Position = new Vector2(10, 75);
        propertyScrollContainer.Size = new Vector2(360, 475);
        propertyScrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

        propertyContainer = new VBoxContainer();
        propertyContainer.Name = "PropertyContainer";
        propertyContainer.AddThemeConstantOverride("separation", 6);
        propertyContainer.Size = new Vector2(360, 0);

        propertyScrollContainer.AddChild(propertyContainer);
        propertyEditorPanel.AddChild(propertyScrollContainer);

        propertyEditorSaveBtn = new Button();
        propertyEditorSaveBtn.Text = "💾 保存修改";
        propertyEditorSaveBtn.CustomMinimumSize = new Vector2(200, 40);
        propertyEditorSaveBtn.Position = new Vector2(90, 580);
        propertyEditorSaveBtn.AddThemeFontSizeOverride("font_size", 16);
        propertyEditorSaveBtn.AddThemeColorOverride("font_color", Colors.White);

        var saveStyle = new StyleBoxFlat();
        saveStyle.BgColor = new Color(0.2f, 0.6f, 0.3f, 0.9f);
        saveStyle.SetCornerRadiusAll(8);
        propertyEditorSaveBtn.AddThemeStyleboxOverride("normal", saveStyle);

        propertyEditorSaveBtn.Pressed += OnSaveProperties;
        propertyEditorPanel.AddChild(propertyEditorSaveBtn);

        AddChild(propertyEditorPanel);
    }

    // ========== 模式切换按钮 ==========
    private void CreateModeToggleButton()
    {
        modeToggleButton = new Button();
        modeToggleButton.Name = "ModeToggleButton";
        modeToggleButton.Text = "🛠️ 编辑模式";
        modeToggleButton.CustomMinimumSize = new Vector2(140, 50);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.3f, 0.5f, 0.3f, 0.9f);
        style.SetCornerRadiusAll(8);
        modeToggleButton.AddThemeStyleboxOverride("normal", style);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(0.4f, 0.6f, 0.4f, 0.95f);
        hoverStyle.SetCornerRadiusAll(8);
        modeToggleButton.AddThemeStyleboxOverride("hover", hoverStyle);

        modeToggleButton.AddThemeFontSizeOverride("font_size", 16);
        modeToggleButton.AddThemeColorOverride("font_color", Colors.White);
        modeToggleButton.Pressed += OnModeTogglePressed;

        AddChild(modeToggleButton);
    }

    // ========== 原有地形菜单 ==========
    private void CreateTerrainMenu()
    {
        terrainMenuPanel = new Panel();
        terrainMenuPanel.Name = "TerrainMenuPanel";
        terrainMenuPanel.CustomMinimumSize = new Vector2(280, 680);
        terrainMenuPanel.SetAnchorsPreset(LayoutPreset.Center);
        terrainMenuPanel.Visible = false;
        terrainMenuPanel.ZIndex = 1000;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.15f, 0.15f, 0.2f, 0.95f);
        panelStyle.SetCornerRadiusAll(12);
        panelStyle.SetBorderWidthAll(2);
        panelStyle.BorderColor = new Color(0.4f, 0.4f, 0.5f);
        terrainMenuPanel.AddThemeStyleboxOverride("panel", panelStyle);

        terrainTitleLabel = new Label();
        terrainTitleLabel.Text = "选择地形类型";
        terrainTitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        terrainTitleLabel.AddThemeFontSizeOverride("font_size", 18);
        terrainTitleLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        terrainTitleLabel.CustomMinimumSize = new Vector2(0, 30);
        terrainTitleLabel.Position = new Vector2(0, 10);
        terrainTitleLabel.Size = new Vector2(280, 30);
        terrainMenuPanel.AddChild(terrainTitleLabel);

        var closeButton = new Button();
        closeButton.Text = "✕";
        closeButton.CustomMinimumSize = new Vector2(30, 30);
        closeButton.Position = new Vector2(245, 8);
        closeButton.Pressed += CloseTerrainMenu;
        terrainMenuPanel.AddChild(closeButton);

        terrainScrollContainer = new ScrollContainer();
        terrainScrollContainer.Position = new Vector2(10, 45);
        terrainScrollContainer.Size = new Vector2(260, 240);
        terrainScrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

        terrainButtonContainer = new VBoxContainer();
        terrainButtonContainer.AddThemeConstantOverride("separation", 4);
        terrainButtonContainer.Size = new Vector2(260, 0);

        foreach (var terrainType in availableTerrainTypes)
        {
            var btn = CreateTerrainButton(terrainType);
            terrainButtonContainer.AddChild(btn);
        }

        terrainScrollContainer.AddChild(terrainButtonContainer);
        terrainMenuPanel.AddChild(terrainScrollContainer);

        CreateCustomDamageUI();
        CreateAmmoFuelUI();

        AddChild(terrainMenuPanel);
    var dragHandle = new Panel();
    dragHandle.Name = "DragHandle";
    dragHandle.CustomMinimumSize = new Vector2(280, 30);
    dragHandle.Position = new Vector2(0, 0);
    var handleStyle = new StyleBoxFlat();
    handleStyle.BgColor = new Color(0.3f, 0.3f, 0.4f, 0.5f);
    handleStyle.SetCornerRadiusAll(8);
    dragHandle.AddThemeStyleboxOverride("panel", handleStyle);
    
    var dragHint = new Label();
    dragHint.Text = "⋮⋮ 拖拽移动";
    dragHint.AddThemeFontSizeOverride("font_size", 10);
    dragHint.AddThemeColorOverride("font_color", new Color(0.6f, 0.7f, 0.8f));
    dragHint.Position = new Vector2(8, 2);
    dragHandle.AddChild(dragHint);
    
    terrainMenuPanel.AddChild(dragHandle);
    MakeMenuDraggable(terrainMenuPanel, dragHandle);
    }

    private void ShowSelectionMenu(Grids grid, List<Infantry> units, List<Weapon> weapons)
    {
        foreach (var child in selectionButtonContainer.GetChildren())
            child.QueueFree();

        selectionTitleLabel.Text = $"格子 {grid.GridIndex} - 选择编辑目标";

        int optionCount = 0;

        foreach (var unit in units)
        {
            string typeName = unit.GetType().Name;
            string teamColor = unit.team switch { "Player1" => "🔴", "Player2" => "🔵", "Player0" => "⚪", "Player" => "🟣", _ => "⚪" };
            string hpText = $"HP:{Mathf.CeilToInt(unit.health / 10f)}";

            var btn = CreateSelectionButton(
                $"{teamColor} [单位:{typeName}] {unit.Name} ({hpText})",
                new Color(0.3f, 0.5f, 0.8f),
                () => OpenUnitEditor(unit)
            );
            selectionButtonContainer.AddChild(btn);
            optionCount++;
        }

        foreach (var weapon in weapons)
        {
            string typeName = weapon.GetType().Name;
            string teamColor = weapon.team switch { "Player1" => "🔴", "Player2" => "🔵", "Player0" => "⚪", "Player" => "🟣", _ => "⚪" };
            string hpText = $"HP:{Mathf.CeilToInt(weapon.health / 10f)}";

            var btn = CreateSelectionButton(
                $"{teamColor} [兵器:{typeName}] {weapon.Name} ({hpText})",
                new Color(0.7f, 0.4f, 0.3f),
                () => OpenWeaponEditor(weapon)
            );
            selectionButtonContainer.AddChild(btn);
            optionCount++;
        }

        var gridBtn = CreateSelectionButton(
            "🟫 修改格子地形/属性",
            new Color(0.4f, 0.7f, 0.4f),
            () => OpenTerrainMenu(grid)
        );
        selectionButtonContainer.AddChild(gridBtn);
        optionCount++;

        // ✅ 设施更改按钮
        var facilityBtn = CreateSelectionButton(
            "🏙️ 设施更改",
            new Color(0.8f, 0.6f, 0.2f),
            () => OpenFacilityMenu(grid)
        );
        selectionButtonContainer.AddChild(facilityBtn);
        optionCount++;

        GD.Print($"[TerrainEditor] 显示选择菜单，共 {optionCount} 个选项");

        var viewportSize = GetViewport().GetVisibleRect().Size;
        selectionMenuPanel.Position = new Vector2(
            (viewportSize.X - selectionMenuPanel.Size.X) / 2,
            (viewportSize.Y - selectionMenuPanel.Size.Y) / 2
        );
        selectionMenuPanel.Visible = true;
        isMenuOpen = true;
    }

    private Button CreateSelectionButton(string text, Color color, Action onPressed)
    {
        var btn = new Button();
        btn.CustomMinimumSize = new Vector2(280, 45);
        btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        btn.Text = text;
        btn.AddThemeFontSizeOverride("font_size", 14);
        btn.AddThemeColorOverride("font_color", Colors.White);

        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = new Color(color.R * 0.3f, color.G * 0.3f, color.B * 0.3f, 0.8f);
        normalStyle.SetCornerRadiusAll(8);
        btn.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(color.R * 0.5f, color.G * 0.5f, color.B * 0.5f, 0.9f);
        hoverStyle.SetCornerRadiusAll(8);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        btn.Pressed += onPressed;
        return btn;
    }

    // ========== ✅ 打开单位编辑器 ==========
    private void OpenUnitEditor(Infantry unit)
    {
        CloseSelectionMenu();
        currentEditingTarget = unit;

        propertyEditorTitle.Text = $"编辑单位: {unit.Name} ({unit.GetType().Name})";

        BuildPropertyEditor(unit);

        var viewportSize = GetViewport().GetVisibleRect().Size;
        propertyEditorPanel.Position = new Vector2(
            (viewportSize.X - propertyEditorPanel.Size.X) / 2,
            (viewportSize.Y - propertyEditorPanel.Size.Y) / 2
        );
        propertyEditorPanel.Visible = true;
        isMenuOpen = true;
    }

    private void BuildPropertyEditor(object target)
    {
        foreach (var child in propertyContainer.GetChildren())
            child.QueueFree();


        if (target is Grids grid)
        {
            BuildGridPropertyEditor(grid);
            return;
        }

        Type targetType = target.GetType();

        var fields = GetExportFields(targetType);
        var properties = GetExportProperties(targetType);

        var allMembers = new List<(string group, string name, Type type, Func<object> getter, Action<object> setter)>();

        foreach (var field in fields)
        {
            string group = GetExportGroupName(field);
            var f = field;
            allMembers.Add((group, f.Name, f.FieldType, 
                () => f.GetValue(target),
                (v) => f.SetValue(target, v)));
        }

        foreach (var prop in properties)
        {
            string group = GetExportGroupName(prop);
            var p = prop;
            allMembers.Add((group, p.Name, p.PropertyType,
                () => p.GetValue(target),
                (v) => p.SetValue(target, v)));
        }

        var grouped = allMembers
            .GroupBy(m => m.group)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            if (!string.IsNullOrEmpty(group.Key))
            {
                var groupLabel = new Label();
                groupLabel.Text = $"=== {group.Key} ===";
                groupLabel.AddThemeFontSizeOverride("font_size", 14);
                groupLabel.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.2f));
                groupLabel.CustomMinimumSize = new Vector2(0, 25);
                propertyContainer.AddChild(groupLabel);
            }

            foreach (var member in group)
            {
                CreateMemberControl(target, member.name, member.type, member.getter, member.setter);
            }
        }


        if (target is Infantry infantry)
        {
            AddUnitVisionFields(infantry);
        }
        else if (target is Weapon weapon)
        {
            AddWeaponVisionFields(weapon);
        }
    }

    private List<FieldInfo> GetExportFields(Type type)
    {
        if (exportFieldsCache.ContainsKey(type))
            return exportFieldsCache[type];

        var result = new List<FieldInfo>();

        var currentType = type;
        while (currentType != null && currentType != typeof(object))
        {
            var fields = currentType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(f => f.GetCustomAttribute<ExportAttribute>() != null)
                .ToList();

            result.AddRange(fields);
            currentType = currentType.BaseType;
        }

        exportFieldsCache[type] = result;
        return result;
    }

    private List<PropertyInfo> GetExportProperties(Type type)
    {
        if (exportPropertiesCache.ContainsKey(type))
            return exportPropertiesCache[type];

        var result = new List<PropertyInfo>();

        var currentType = type;
        while (currentType != null && currentType != typeof(object))
        {
            var props = currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.GetCustomAttribute<ExportAttribute>() != null)
                .Where(p => p.CanRead && p.CanWrite)
                .ToList();

            result.AddRange(props);
            currentType = currentType.BaseType;
        }

        exportPropertiesCache[type] = result;
        return result;
    }


// 替换原有的 BuildGridPropertyEditor 方法
private void BuildGridPropertyEditor(Grids grid)
{
    // 基础属性
    var basicLabel = new Label();
    basicLabel.Text = "=== 基础属性 ===";
    basicLabel.AddThemeFontSizeOverride("font_size", 14);
    basicLabel.AddThemeColorOverride("font_color", Colors.Yellow);
    propertyContainer.AddChild(basicLabel);

    CreateMemberControl(grid, "gridType", typeof(GridType), 
        () => grid.gridType, (v) => { grid.gridType = (GridType)v; });

    if (grid.city != null)
    {
        var cityLabel = new Label();
        cityLabel.Text = "=== City 属性 ===";
        cityLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        propertyContainer.AddChild(cityLabel);

        CreateMemberControl(grid.city, "facilityTeam", typeof(string),
            () => grid.city.facilityTeam, (v) => { grid.city.facilityTeam = (string)v; });
        CreateMemberControl(grid.city, "isSupplyPoint", typeof(bool),
            () => grid.city.isSupplyPoint, (v) => { grid.city.isSupplyPoint = (bool)v; });
        CreateMemberControl(grid.city, "healAmount", typeof(int),
            () => grid.city.healAmount, (v) => { grid.city.healAmount = (int)v; });
        CreateMemberControl(grid.city, "capturePointsRequired", typeof(int),
            () => grid.city.capturePointsRequired, (v) => { grid.city.capturePointsRequired = (int)v; });

        // ✅ 占领进度（只读显示）
        var captureLabel = new Label();
        captureLabel.Text = $"当前占领中单位数: {grid.city.capturingUnits.Count}";
        captureLabel.AddThemeFontSizeOverride("font_size", 12);
        captureLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.6f));
        propertyContainer.AddChild(captureLabel);
    }

    // ✅ 格子视野系统
    var visionLabel = new Label();
    visionLabel.Text = "=== 格子视野系统 ===";
    visionLabel.AddThemeFontSizeOverride("font_size", 14);
    visionLabel.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.2f));
    propertyContainer.AddChild(visionLabel);

    CreateMemberControl(grid, "gridVisionMode", typeof(GridVisionMode),
        () => grid.gridVisionMode, (v) => { grid.gridVisionMode = (GridVisionMode)v; });
    CreateMemberControl(grid, "gridVisionRange", typeof(int),
        () => grid.gridVisionRange, (v) => { grid.gridVisionRange = (int)v; });
    CreateMemberControl(grid, "towerDirection", typeof(TowerDirection),
        () => grid.towerDirection, (v) => { grid.towerDirection = (TowerDirection)v; });
    CreateMemberControl(grid, "towerCanRotate", typeof(bool),
        () => grid.towerCanRotate, (v) => { grid.towerCanRotate = (bool)v; });
    CreateMemberControl(grid, "towerRayAngle", typeof(float),
        () => grid.towerRayAngle, (v) => { grid.towerRayAngle = (float)v; });
    CreateMemberControl(grid, "gridVisionIgnoreTerrain", typeof(bool),
        () => grid.gridVisionIgnoreTerrain, (v) => { grid.gridVisionIgnoreTerrain = (bool)v; });
    CreateMemberControl(grid, "visionBonus", typeof(int),
        () => grid.visionBonus, (v) => { grid.visionBonus = (int)v; });
    
    // ✅ 关键：requiresAdjacentVision — 用户提到的"不紧邻不显示"
    CreateMemberControl(grid, "requiresAdjacentVision", typeof(bool),
        () => grid.requiresAdjacentVision, (v) => { grid.requiresAdjacentVision = (bool)v; });
    
    // ✅ 新增：瞭望塔标识
    CreateMemberControl(grid, "isWatchtower", typeof(bool),
        () => grid.isWatchtower, (v) => { grid.isWatchtower = (bool)v; });

    // 自定义伤害
    var damageLabel = new Label();
    damageLabel.Text = "=== 自定义伤害 ===";
    damageLabel.AddThemeFontSizeOverride("font_size", 14);
    damageLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
    propertyContainer.AddChild(damageLabel);

    CreateMemberControl(grid, "canDestroyUnit", typeof(bool),
        () => grid.canDestroyUnit, (v) => { grid.canDestroyUnit = (bool)v; });
    CreateMemberControl(grid, "fixedDamagePerTurn", typeof(int),
        () => grid.fixedDamagePerTurn, (v) => { grid.fixedDamagePerTurn = (int)v; });
    CreateMemberControl(grid, "fixedAttackPerTurn", typeof(int),
        () => grid.fixedAttackPerTurn, (v) => { grid.fixedAttackPerTurn = (int)v; });
    CreateMemberControl(grid, "canOverMaxHealth", typeof(bool),
        () => grid.canOverMaxHealth, (v) => { grid.canOverMaxHealth = (bool)v; });

    // 弹药/燃料
    var ammoLabel = new Label();
    ammoLabel.Text = "=== 弹药/燃料 ===";
    ammoLabel.AddThemeFontSizeOverride("font_size", 14);
    ammoLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.7f, 1f));
    propertyContainer.AddChild(ammoLabel);

    CreateMemberControl(grid, "fixedAmmoChangePerTurn", typeof(int),
        () => grid.fixedAmmoChangePerTurn, (v) => { grid.fixedAmmoChangePerTurn = (int)v; });
    CreateMemberControl(grid, "ammoCanOverMax", typeof(bool),
        () => grid.ammoCanOverMax, (v) => { grid.ammoCanOverMax = (bool)v; });
    CreateMemberControl(grid, "ammoCanReachZero", typeof(bool),
        () => grid.ammoCanReachZero, (v) => { grid.ammoCanReachZero = (bool)v; });
    CreateMemberControl(grid, "fixedFuelChangePerTurn", typeof(int),
        () => grid.fixedFuelChangePerTurn, (v) => { grid.fixedFuelChangePerTurn = (int)v; });
    CreateMemberControl(grid, "fuelCanOverMax", typeof(bool),
        () => grid.fuelCanOverMax, (v) => { grid.fuelCanOverMax = (bool)v; });
    CreateMemberControl(grid, "fuelCanReachZero", typeof(bool),
        () => grid.fuelCanReachZero, (v) => { grid.fuelCanReachZero = (bool)v; });
    
    // ✅ 新增：格子上的单位/兵器列表（只读）
    var unitsLabel = new Label();
    unitsLabel.Text = "=== 当前格子内容 ===";
    unitsLabel.AddThemeFontSizeOverride("font_size", 14);
    unitsLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 1f));
    propertyContainer.AddChild(unitsLabel);
    
    var unitCountLabel = new Label();
    unitCountLabel.Text = $"单位: {grid.infantries.Count}个 | 兵器: {grid.weapons.Count}个";
    unitCountLabel.AddThemeFontSizeOverride("font_size", 11);
    unitCountLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
    propertyContainer.AddChild(unitCountLabel);
}
    private void AddUnitVisionFields(Infantry unit)
    {
        var visionLabel = new Label();
        visionLabel.Text = "=== 战争迷雾视野 ===";
        visionLabel.AddThemeFontSizeOverride("font_size", 14);
        visionLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        propertyContainer.AddChild(visionLabel);

        CreateMemberControl(unit, "visionRange", typeof(int),
            () => unit.visionRange, (v) => { unit.visionRange = (int)v; });
        CreateMemberControl(unit, "useConfigVision", typeof(bool),
            () => unit.useConfigVision, (v) => { unit.useConfigVision = (bool)v; });
        CreateMemberControl(unit, "overrideGlobalTerrainBonus", typeof(bool),
            () => unit.overrideGlobalTerrainBonus, (v) => { unit.overrideGlobalTerrainBonus = (bool)v; });

        // ✅ 地形视野加成矩阵编辑器
        if (unit.overrideGlobalTerrainBonus)
        {
            var matrixLabel = new Label();
            matrixLabel.Text = "--- 单位专属地形视野加成 ---";
            matrixLabel.AddThemeFontSizeOverride("font_size", 12);
            matrixLabel.AddThemeColorOverride("font_color", Colors.Yellow);
            propertyContainer.AddChild(matrixLabel);

            var matrixEditor = CreateTerrainBonusMatrixEditor(
                unit.unitTerrainVisionBonus,
                (newDict) => { unit.unitTerrainVisionBonus = newDict; }
            );
            propertyContainer.AddChild(matrixEditor);
        }
    }

    // ✅ 新增：兵器视野字段
    private void AddWeaponVisionFields(Weapon weapon)
    {
        var visionLabel = new Label();
        visionLabel.Text = "=== 战争迷雾视野 ===";
        visionLabel.AddThemeFontSizeOverride("font_size", 14);
        visionLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        propertyContainer.AddChild(visionLabel);

        CreateMemberControl(weapon, "visionRange", typeof(int),
            () => weapon.visionRange, (v) => { weapon.visionRange = (int)v; });
        CreateMemberControl(weapon, "useConfigVision", typeof(bool),
            () => weapon.useConfigVision, (v) => { weapon.useConfigVision = (bool)v; });
        CreateMemberControl(weapon, "visionMode", typeof(VisionMode),
            () => weapon.visionMode, (v) => { weapon.visionMode = (VisionMode)v; });
        CreateMemberControl(weapon, "overrideGlobalTerrainBonus", typeof(bool),
            () => weapon.overrideGlobalTerrainBonus, (v) => { weapon.overrideGlobalTerrainBonus = (bool)v; });

        if (weapon.overrideGlobalTerrainBonus)
        {
            var matrixLabel = new Label();
            matrixLabel.Text = "--- 兵器专属地形视野加成 ---";
            matrixLabel.AddThemeFontSizeOverride("font_size", 12);
            matrixLabel.AddThemeColorOverride("font_color", Colors.Yellow);
            propertyContainer.AddChild(matrixLabel);

            var matrixEditor = CreateTerrainBonusMatrixEditor(
                weapon.weaponTerrainVisionBonus,
                (newDict) => { weapon.weaponTerrainVisionBonus = newDict; }
            );
            propertyContainer.AddChild(matrixEditor);
        }
    }

    // ✅ 新增：地形视野加成矩阵编辑器
    private Control CreateTerrainBonusMatrixEditor(Godot.Collections.Dictionary<GridType, int> dict, Action<Godot.Collections.Dictionary<GridType, int>> onChanged)
    {
        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", 4);

        var hintLabel = new Label();
        hintLabel.Text = "每个地形的视野加成（可正可负）";
        hintLabel.AddThemeFontSizeOverride("font_size", 10);
        hintLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.6f));
        mainVbox.AddChild(hintLabel);

        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(340, 200);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

        var container = new VBoxContainer();
        container.AddThemeConstantOverride("separation", 2);

        var terrainTypes = Enum.GetValues(typeof(GridType));

        foreach (GridType terrain in terrainTypes)
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 8);
            hbox.CustomMinimumSize = new Vector2(0, 28);

            var nameLabel = new Label();
            nameLabel.Text = terrain.ToString();
            nameLabel.CustomMinimumSize = new Vector2(120, 24);
            nameLabel.AddThemeFontSizeOverride("font_size", 11);
            hbox.AddChild(nameLabel);

            int currentValue = dict.GetValueOrDefault(terrain, 0);

            var spinBox = new SpinBox();
            spinBox.MinValue = -10;
            spinBox.MaxValue = 10;
            spinBox.Value = currentValue;
            spinBox.CustomMinimumSize = new Vector2(70, 26);
            spinBox.AddThemeFontSizeOverride("font_size", 11);

            GridType capturedTerrain = terrain;
            spinBox.ValueChanged += (newVal) => {
                dict[capturedTerrain] = (int)newVal;
                onChanged(dict);
            };

            hbox.AddChild(spinBox);

            var signLabel = new Label();
            string sign = currentValue > 0 ? "+" : "";
            signLabel.Text = $"({sign}{currentValue})";
            signLabel.AddThemeFontSizeOverride("font_size", 10);
            signLabel.AddThemeColorOverride("font_color",
                currentValue > 0 ? new Color(0.3f, 1, 0.3f) :
                currentValue < 0 ? new Color(1, 0.3f, 0.3f) : new Color(0.7f, 0.7f, 0.7f));
            hbox.AddChild(signLabel);

            container.AddChild(hbox);
        }

        scroll.AddChild(container);
        mainVbox.AddChild(scroll);

        return mainVbox;
    }

private void OnPropertyChanged(object target, string propertyName)
{
    GD.Print($"[PropertyEditor] {target.GetType().Name}.{propertyName} 已修改");

    if (target is Grids grid)
    {

        if (propertyName == "gridType")
        {
            UpdateGridVisual(grid);

            if (terrainMenuPanel.Visible)
            {
                terrainTitleLabel.Text = $"选择地形类型 (当前: {GetTerrainDisplayName(grid.gridType)})";
            }
        }
        else if (propertyName.Contains("Vision") || propertyName == "requiresAdjacentVision" || propertyName == "isWatchtower")
        {

            var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
            gm?.fogOfWarManager?.ForceRefresh();
        }
        return;
    }

    if (propertyName == "team" && target is Node2D node)
    {
        RefreshTeamVisual(target);
    }
        else if (propertyName.Contains("health") || propertyName.Contains("Health"))
        {
            if (target is Infantry infantry) infantry.UpdateHpLabel();
            if (target is Weapon weapon) weapon.UpdateHpLabel();
        }
        else if (propertyName.Contains("Ammo") || propertyName.Contains("ammo"))
        {
            if (target is BlackCannon cannon) cannon.UpdateAmmoVisual();
        }
        else if (propertyName == "direction" && target is BlackCannon bc)
        {
            bc.UpdateDirectionVisual();
        }
    }

    private void RefreshTeamVisual(object target)
    {
        if (target is Infantry infantry)
        {
            infantry.UpdateHpLabel();

            if (infantry is Mech mech && mech.animSprite != null)
            {
                string animName = (mech.team == "Player2") ? "mech2" : "mech1";
                if (mech.animSprite.SpriteFrames.HasAnimation(animName))
                {
                    mech.animSprite.Play(animName);
                    mech.animSprite.Modulate = mech.normal;
                }
            }
            else if (infantry is LightTank tank && tank.animSprite != null)
            {
                string animName = (tank.team == "Player2") ? "lighttank2" : "lighttank1";
                if (tank.animSprite.SpriteFrames.HasAnimation(animName))
                {
                    tank.ShowAnimState(animName);
                    tank.animSprite.Modulate = tank.normal;
                }
            }
            else if (infantry is Artillery artillery && artillery.animSprite != null)
            {
                string animName = (artillery.team == "Player2") ? "artillery2" : "artillery1";
                if (artillery.animSprite.SpriteFrames.HasAnimation(animName))
                {
                    artillery.ShowAnimState(animName);
                    artillery.animSprite.Modulate = artillery.normal;
                }
            }
            else if (infantry is APC apc && apc.animSprite != null)
            {
                string animName = (apc.team == "Player2") ? "apc2" : "apc1";
                if (apc.animSprite.SpriteFrames.HasAnimation(animName))
                {
                    apc.ShowAnimState(animName);
                    apc.animSprite.Modulate = apc.normal;
                }
            }
            else if (infantry is Oozium oozium && oozium.animSprite != null)
            {
                string animName = (oozium.team == "Player2") ? "oozium2" : "oozium1";
                if (oozium.animSprite.SpriteFrames.HasAnimation(animName))
                {
                    oozium.animSprite.Play(animName);
                    oozium.animSprite.Modulate = oozium.normal;
                }
            }
            else if (infantry is MdTank mdTank && mdTank.MdSprite != null)
            {
                string animName = (mdTank.team == "Player2") ? "Md2" : "Md1";
                if (mdTank.MdSprite.SpriteFrames.HasAnimation(animName))
                {
                    mdTank.MdSprite.Play(animName);
                    mdTank.MdSprite.Modulate = mdTank.normal;
                }
            }
            else
            {
                infantry.UpdateTeamVisual();
            }

            var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
            gm?.UpdateUnitLists();
        }

        if (target is Weapon weapon && weapon.animSprite != null)
        {
            string teamPrefix = (weapon.team == "Player2") ? "cannon2" : "cannon1";

            if (weapon is BlackCannon cannon)
            {
                string dirSuffix = cannon.direction switch
                {
                    BlackCannon.CannonDirection.Up => "up",
                    BlackCannon.CannonDirection.Down => "down",
                    BlackCannon.CannonDirection.Left => "left",
                    BlackCannon.CannonDirection.Right => "right",
                    _ => "up"
                };
                string animName = $"{teamPrefix}_{dirSuffix}";
                if (weapon.animSprite.SpriteFrames.HasAnimation(animName))
                {
                    weapon.animSprite.Play(animName);
                    weapon.animSprite.Modulate = TeamHelper.GetTeamColor(weapon.team);
                }
            }
            else
            {
                if (weapon.animSprite.SpriteFrames.HasAnimation(teamPrefix))
                {
                    weapon.animSprite.Play(teamPrefix);
                    weapon.animSprite.Modulate = TeamHelper.GetTeamColor(weapon.team);
                }
            }

            weapon.UpdateHpLabel();
        }
    }



private void OnSaveProperties()
{
    if (currentEditingTarget == null) return;

    GD.Print($"[PropertyEditor] 保存 {currentEditingTarget.GetType().Name} 的修改");

    if (currentEditingTarget is Infantry unit)
    {
        unit.UpdateHpLabel();
        if (unit.sprite != null) unit.sprite.Modulate = unit.normal;

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.UpdateUnitLists();
    }

    if (currentEditingTarget is Weapon weapon)
    {
        weapon.UpdateHpLabel();
        if (weapon is BlackCannon cannon)
        {
            cannon.UpdateAmmoVisual();
        }
    }
    
    // ✅ 新增：处理格子保存
    if (currentEditingTarget is Grids grid)
    {
        UpdateGridVisual(grid);
        // 刷新战争迷雾（因为视野属性可能变了）
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.fogOfWarManager?.ForceRefresh();
        GD.Print($"[PropertyEditor] 格子 {grid.GridIndex} 属性已保存");
    }

    ClosePropertyEditor();
}

    private string GetExportGroupName(MemberInfo member)
    {
        var groupAttr = member.GetCustomAttribute<ExportGroupAttribute>();
        if (groupAttr != null) return groupAttr.Name;

        var declaringType = member.DeclaringType;
        if (declaringType != null)
        {
            var members = declaringType.GetMembers(BindingFlags.Public | BindingFlags.Instance);
            string currentGroup = "";
            foreach (var m in members)
            {
                var group = m.GetCustomAttribute<ExportGroupAttribute>();
                if (group != null) currentGroup = group.Name;
                if (m == member) return currentGroup;
            }
        }

        return "";
    }

    private void CloseSelectionMenu()
    {
        selectionMenuPanel.Visible = false;
    }

// 替换原有的 ClosePropertyEditor
private void ClosePropertyEditor()
{
    propertyEditorPanel.Visible = false;
    
    if (currentEditingTarget is Grids)
    {
        terrainMenuPanel.Visible = false;
        selectedGrid = null;
    }
    
    currentEditingTarget = null;
    isMenuOpen = false;
}
private void OpenTerrainMenu(Grids grid)
{
    CloseSelectionMenu();
    selectedGrid = grid;
    

    terrainTitleLabel.Text = $"选择地形类型 (当前: {GetTerrainDisplayName(grid.gridType)})";
    LoadCustomDamageValues(grid);
    LoadAmmoFuelValues(grid);
    terrainMenuPanel.Visible = true;
    
    currentEditingTarget = grid;
    propertyEditorTitle.Text = $"编辑格子: {grid.GridIndex} ({grid.gridType})";
    BuildPropertyEditor(grid);  
    propertyEditorPanel.Visible = true;
}

// ✅ 设施更改菜单：生成/摧毁/编辑设施
private void OpenFacilityMenu(Grids grid)
{
    CloseSelectionMenu();
    selectedGrid = grid;
    currentEditingTarget = grid.city;
    
    propertyEditorTitle.Text = $"设施更改 - 格子 {grid.GridIndex}";
    
    // 清空属性容器
    foreach (var child in propertyContainer.GetChildren())
        child.QueueFree();
    
    if (grid.city == null)
    {
        // 没有设施：显示生成按钮
        var noFacilityLabel = new Label();
        noFacilityLabel.Text = "当前格子没有设施";
        noFacilityLabel.AddThemeFontSizeOverride("font_size", 14);
        noFacilityLabel.AddThemeColorOverride("font_color", Colors.Gray);
        propertyContainer.AddChild(noFacilityLabel);
        
        var spawnBtn = new Button();
        spawnBtn.Text = "🏙️ 生成 City 设施";
        spawnBtn.CustomMinimumSize = new Vector2(200, 40);
        spawnBtn.Pressed += () => SpawnFacilityAtGrid(grid);
        propertyContainer.AddChild(spawnBtn);
    }
    else
    {
        // 有设施：显示编辑 + 摧毁按钮
        var facilityLabel = new Label();
        facilityLabel.Text = $"当前设施: {grid.city.GetType().Name}";
        facilityLabel.AddThemeFontSizeOverride("font_size", 14);
        facilityLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        propertyContainer.AddChild(facilityLabel);
        
        // 编辑设施属性
        BuildPropertyEditor(grid.city);
        
        // 摧毁按钮
        var destroyBtn = new Button();
        destroyBtn.Text = "💥 摧毁设施";
        destroyBtn.CustomMinimumSize = new Vector2(200, 40);
        destroyBtn.AddThemeColorOverride("font_color", Colors.Red);
        destroyBtn.Pressed += () => DestroyFacilityAtGrid(grid);
        propertyContainer.AddChild(destroyBtn);
    }
    
    propertyEditorPanel.Visible = true;
}

// ✅ 在格子生成 City 设施（OpenFacilityMenu 用，默认 Player0）
private void SpawnFacilityAtGrid(Grids grid)
{
    SpawnFacilityAtGrid(grid, "res://Prefabs/City.tscn", "Player0");
    OpenFacilityMenu(grid);
}

// ✅ 在格子生成设施（spawnMenu 用，支持模板参数）
private void SpawnFacilityAtGrid(Grids grid, string scenePath, string team)
{
    if (grid.city != null)
    {
        spawnHintLabel.Text = "❌ 该格子已有设施";
        spawnHintLabel.AddThemeColorOverride("font_color", Colors.Red);
        return;
    }
    
    if (string.IsNullOrEmpty(scenePath))
    {
        spawnHintLabel.Text = "❌ 场景路径为空";
        spawnHintLabel.AddThemeColorOverride("font_color", Colors.Red);
        return;
    }
    
    var scene = GD.Load<PackedScene>(scenePath);
    if (scene == null)
    {
        GD.PushError($"[TerrainEditor] 无法加载场景: {scenePath}");
        spawnHintLabel.Text = $"❌ 无法加载场景: {scenePath}";
        spawnHintLabel.AddThemeColorOverride("font_color", Colors.Red);
        return;
    }
    
    var facility = scene.Instantiate<Facility>();
    if (facility == null)
    {
        spawnHintLabel.Text = "❌ 设施实例化失败";
        spawnHintLabel.AddThemeColorOverride("font_color", Colors.Red);
        return;
    }
    
    facility.Name = "Facility";
    grid.AddChild(facility);
    grid.city = facility;
    
    // 设置势力
    facility.facilityTeam = team;
    facility.isSupplyPoint = true;
    facility.healAmount = 20;
    facility.capturePointsRequired = 20;
    
    if (facility is City city)
    {
        city.UpdateCityVisual();
    }
    else
    {
        facility.UpdateCityVisual();
    }
    
    GD.Print($"[TerrainEditor] 在 {grid.GridIndex} 生成 {facility.GetType().Name} 设施，势力={team}");
    
    spawnHintLabel.Text = $"✅ 生成设施: {facility.GetType().Name} ({team})";
    spawnHintLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
    
    gameManager?.UpdateUnitLists();
}

// ✅ 摧毁格子上的设施
private void DestroyFacilityAtGrid(Grids grid)
{
    if (grid.city == null) return;
    
    grid.city.QueueFree();
    grid.city = null;
    
    GD.Print($"[TerrainEditor] 在 {grid.GridIndex} 摧毁设施");
    
    // 刷新菜单
    OpenFacilityMenu(grid);
}

// 替换原有的 CloseTerrainMenu
private void CloseTerrainMenu()
{
    SaveCustomDamageValues(selectedGrid);
    SaveAmmoFuelValues(selectedGrid);
    terrainMenuPanel.Visible = false;

    if (currentEditingTarget is Grids)
    {
        propertyEditorPanel.Visible = false;
        currentEditingTarget = null;
    }
    
    isMenuOpen = false;
    selectedGrid = null;
}
    private void CreateCustomDamageUI()
    {
        customDamageContainer = new VBoxContainer();
        customDamageContainer.Position = new Vector2(10, 290);
        customDamageContainer.Size = new Vector2(260, 160);
        customDamageContainer.AddThemeConstantOverride("separation", 5);

        var sectionTitle = new Label();
        sectionTitle.Text = "=== 格子自定义伤害 ===";
        sectionTitle.AddThemeFontSizeOverride("font_size", 13);
        sectionTitle.AddThemeColorOverride("font_color", Colors.Yellow);
        customDamageContainer.AddChild(sectionTitle);

        var destroyHBox = new HBoxContainer();
        var destroyLabel = new Label { Text = "可摧毁单位:", CustomMinimumSize = new Vector2(100, 22) };
        destroyLabel.AddThemeFontSizeOverride("font_size", 11);
        canDestroyCheckBox = new CheckBox { ButtonPressed = true };
        destroyHBox.AddChild(destroyLabel);
        destroyHBox.AddChild(canDestroyCheckBox);
        customDamageContainer.AddChild(destroyHBox);

        var damageHBox = new HBoxContainer();
        var damageLabel = new Label { Text = "固定伤害/回合:", CustomMinimumSize = new Vector2(100, 22) };
        damageLabel.AddThemeFontSizeOverride("font_size", 11);
        fixedDamageSpinBox = new SpinBox { MinValue = -999, MaxValue = 999, Value = 0, CustomMinimumSize = new Vector2(80, 22) };
        fixedDamageSpinBox.AddThemeFontSizeOverride("font_size", 11);
        damageHBox.AddChild(damageLabel);
        damageHBox.AddChild(fixedDamageSpinBox);
        customDamageContainer.AddChild(damageHBox);

        var attackHBox = new HBoxContainer();
        var attackLabel = new Label { Text = "固定攻击/回合:", CustomMinimumSize = new Vector2(100, 22) };
        attackLabel.AddThemeFontSizeOverride("font_size", 11);
        fixedAttackSpinBox = new SpinBox { MinValue = -999, MaxValue = 999, Value = 0, CustomMinimumSize = new Vector2(80, 22) };
        fixedAttackSpinBox.AddThemeFontSizeOverride("font_size", 11);
        attackHBox.AddChild(attackLabel);
        attackHBox.AddChild(fixedAttackSpinBox);
        customDamageContainer.AddChild(attackHBox);

        var overMaxHBox = new HBoxContainer();
        var overMaxLabel = new Label { Text = "可超最大血量:", CustomMinimumSize = new Vector2(100, 22) };
        overMaxLabel.AddThemeFontSizeOverride("font_size", 11);
        canOverMaxCheckBox = new CheckBox { ButtonPressed = false };
        overMaxHBox.AddChild(overMaxLabel);
        overMaxHBox.AddChild(canOverMaxCheckBox);
        customDamageContainer.AddChild(overMaxHBox);

        terrainMenuPanel.AddChild(customDamageContainer);
    }

    private void CreateAmmoFuelUI()
    {
        ammoFuelContainer = new VBoxContainer();
        ammoFuelContainer.Position = new Vector2(10, 455);
        ammoFuelContainer.Size = new Vector2(260, 220);
        ammoFuelContainer.AddThemeConstantOverride("separation", 5);

        var sectionTitle = new Label();
        sectionTitle.Text = "=== 弹药/燃料系统 ===";
        sectionTitle.AddThemeFontSizeOverride("font_size", 13);
        sectionTitle.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.2f));
        ammoFuelContainer.AddChild(sectionTitle);

        var ammoTitle = new Label();
        ammoTitle.Text = "【弹药】";
        ammoTitle.AddThemeFontSizeOverride("font_size", 12);
        ammoTitle.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.2f));
        ammoFuelContainer.AddChild(ammoTitle);

        var ammoHBox = new HBoxContainer();
        var ammoLabel = new Label { Text = "弹药变化/回合:", CustomMinimumSize = new Vector2(100, 22) };
        ammoLabel.AddThemeFontSizeOverride("font_size", 11);
        fixedAmmoSpinBox = new SpinBox { MinValue = -999, MaxValue = 999, Value = 0, CustomMinimumSize = new Vector2(80, 22) };
        fixedAmmoSpinBox.AddThemeFontSizeOverride("font_size", 11);
        ammoHBox.AddChild(ammoLabel);
        ammoHBox.AddChild(fixedAmmoSpinBox);
        ammoFuelContainer.AddChild(ammoHBox);

        var ammoOverHBox = new HBoxContainer();
        var ammoOverLabel = new Label { Text = "可超弹药上限:", CustomMinimumSize = new Vector2(100, 22) };
        ammoOverLabel.AddThemeFontSizeOverride("font_size", 11);
        ammoCanOverMaxCheckBox = new CheckBox { ButtonPressed = false };
        ammoOverHBox.AddChild(ammoOverLabel);
        ammoOverHBox.AddChild(ammoCanOverMaxCheckBox);
        ammoFuelContainer.AddChild(ammoOverHBox);

        var ammoZeroHBox = new HBoxContainer();
        var ammoZeroLabel = new Label { Text = "可归零:", CustomMinimumSize = new Vector2(100, 22) };
        ammoZeroLabel.AddThemeFontSizeOverride("font_size", 11);
        ammoCanReachZeroCheckBox = new CheckBox { ButtonPressed = true };
        ammoZeroHBox.AddChild(ammoZeroLabel);
        ammoZeroHBox.AddChild(ammoCanReachZeroCheckBox);
        ammoFuelContainer.AddChild(ammoZeroHBox);

        var separator = new HSeparator();
        separator.CustomMinimumSize = new Vector2(0, 4);
        ammoFuelContainer.AddChild(separator);

        var fuelTitle = new Label();
        fuelTitle.Text = "【燃料】";
        fuelTitle.AddThemeFontSizeOverride("font_size", 12);
        fuelTitle.AddThemeColorOverride("font_color", new Color(0.2f, 0.7f, 0.9f));
        ammoFuelContainer.AddChild(fuelTitle);

        var fuelHBox = new HBoxContainer();
        var fuelLabel = new Label { Text = "燃料变化/回合:", CustomMinimumSize = new Vector2(100, 22) };
        fuelLabel.AddThemeFontSizeOverride("font_size", 11);
        fixedFuelSpinBox = new SpinBox { MinValue = -999, MaxValue = 999, Value = 0, CustomMinimumSize = new Vector2(80, 22) };
        fixedFuelSpinBox.AddThemeFontSizeOverride("font_size", 11);
        fuelHBox.AddChild(fuelLabel);
        fuelHBox.AddChild(fixedFuelSpinBox);
        ammoFuelContainer.AddChild(fuelHBox);

        var fuelOverHBox = new HBoxContainer();
        var fuelOverLabel = new Label { Text = "可超燃料上限:", CustomMinimumSize = new Vector2(100, 22) };
        fuelOverLabel.AddThemeFontSizeOverride("font_size", 11);
        fuelCanOverMaxCheckBox = new CheckBox { ButtonPressed = false };
        fuelOverHBox.AddChild(fuelOverLabel);
        fuelOverHBox.AddChild(fuelCanOverMaxCheckBox);
        ammoFuelContainer.AddChild(fuelOverHBox);

        var fuelZeroHBox = new HBoxContainer();
        var fuelZeroLabel = new Label { Text = "可归零:", CustomMinimumSize = new Vector2(100, 22) };
        fuelZeroLabel.AddThemeFontSizeOverride("font_size", 11);
        fuelCanReachZeroCheckBox = new CheckBox { ButtonPressed = true };
        fuelZeroHBox.AddChild(fuelZeroLabel);
        fuelZeroHBox.AddChild(fuelCanReachZeroCheckBox);
        ammoFuelContainer.AddChild(fuelZeroHBox);

        terrainMenuPanel.AddChild(ammoFuelContainer);
    }

    private Button CreateTerrainButton(GridType terrainType)
    {
        var btn = new Button();
        btn.CustomMinimumSize = new Vector2(240, 36);
        btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        Color terrainColor = terrainColors.GetValueOrDefault(terrainType, Colors.White);
        string displayName = GetTerrainDisplayName(terrainType);
        string defenseText = GetTerrainDefenseText(terrainType);
        btn.Text = $"{displayName}  {defenseText}";

        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = new Color(terrainColor.R * 0.3f, terrainColor.G * 0.3f, terrainColor.B * 0.3f, 0.8f);
        normalStyle.SetCornerRadiusAll(6);
        btn.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(terrainColor.R * 0.5f, terrainColor.G * 0.5f, terrainColor.B * 0.5f, 0.9f);
        hoverStyle.SetCornerRadiusAll(6);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        var pressedStyle = new StyleBoxFlat();
        pressedStyle.BgColor = new Color(terrainColor.R * 0.7f, terrainColor.G * 0.7f, terrainColor.B * 0.7f, 1.0f);
        pressedStyle.SetCornerRadiusAll(6);
        btn.AddThemeStyleboxOverride("pressed", pressedStyle);

        var colorRect = new ColorRect();
        colorRect.Color = terrainColor;
        colorRect.CustomMinimumSize = new Vector2(6, 28);
        colorRect.Position = new Vector2(4, 4);
        btn.AddChild(colorRect);

        btn.AddThemeFontSizeOverride("font_size", 13);
        btn.AddThemeColorOverride("font_color", Colors.White);

        GridType capturedType = terrainType;
        btn.Pressed += () => OnTerrainSelected(capturedType);

        return btn;
    }

    private string GetTerrainDisplayName(GridType type)
    {
        return type switch
        {
            GridType.GROUND => "🟫 平原",
            GridType.FOREST => "🌲 森林",
            GridType.ROAD => "🛣️ 公路",
            GridType.SEA => "🌊 海洋",
            GridType.RIVER => "🏞️ 河流",
            GridType.HILL => "⛰️ 山地",
            GridType.METEORITE => "☄️ 陨石(兵器设施)",
            GridType.PIPE => "🔧 管道",
            GridType.LAVA => "🌋 岩浆",
            GridType.BEACH => "🏖️ 沙滩",
            GridType.TP => "✨ 传送点",
            GridType.REEF => "🪸 珊瑚礁",
            GridType.WHIRLPOOL => "🌀 漩涡",
            GridType.LAVASIDE => "🔥 岩浆滩",
            GridType.SEAFOG => "🌫️ 海上迷雾",
            GridType.LANDFOG => "🌫️ 陆上迷雾",
            GridType.WATERFALL => "💧 瀑布",
            GridType.CLIFF => "🪨 悬崖",
            GridType.SLOPE => "📐 斜坡",
            GridType.CAVE => "🕳️ 塌方穴",
            GridType.HOLE => "🕳️ 人工洞",
            GridType.PIPESEAM => "⚙️ 管道接缝(兵器设施)",
            GridType.TRACK => "🛤️ 铁轨",
            GridType.STATION => "🚉 站台",
            GridType.BRIDGE => "🌉 桥梁",
            GridType.LAVABRIDGE => "🔥🌉 岩浆桥梁",
            GridType.PASSABLEPIPE => "🔧空心管道",
            GridType.SHIPGATE => "⚓ 船闸",
            GridType.OVERPASS => "🌉 立交桥",
            GridType.BROKENPIPE => "💥 破碎管道",
            GridType.RUINS => "🏚️ 废墟",
            GridType.BROKENTRACK => "💥🛤️ 破碎铁路",
            GridType.LAVAFOG => "🌋🌫️ 岩浆迷雾",
            _ => type.ToString()
        };
    }

    private string GetTerrainDefenseText(GridType type)
    {
        float bonus = type switch
        {
            GridType.GROUND => 0.10f, GridType.FOREST => 0.20f,
            GridType.HILL => 0.40f, GridType.ROAD => 0.0f, GridType.SEA => 0.0f,
            GridType.RIVER => 0.0f, GridType.BEACH => 0.10f, GridType.TP => 0.0f,
            GridType.REEF => 0.10f, GridType.WHIRLPOOL => 0.10f, GridType.LAVASIDE => 0.10f,
            GridType.SEAFOG => 0.50f, GridType.LANDFOG => 0.50f, GridType.WATERFALL => 0.20f,
            GridType.CLIFF => 0.30f, GridType.SLOPE => 0.20f, GridType.CAVE => 0.40f,
            GridType.HOLE => 0.40f, GridType.PIPESEAM => 0.10f, GridType.METEORITE => 0.10f,
            GridType.TRACK => 0.0f, GridType.STATION => 0.20f, GridType.BRIDGE => 0.0f,
            GridType.LAVABRIDGE => 0.0f, GridType.PASSABLEPIPE => 0.10f, GridType.SHIPGATE => 0.10f,
            GridType.OVERPASS => 0.30f, GridType.BROKENPIPE => 0.20f, GridType.RUINS => 0.30f,
            GridType.BROKENTRACK => 0.20f, GridType.LAVAFOG => 0.50f,
            _ => 0.0f
        };
        return bonus > 0 ? $"[防御{bonus * 100:F0}%]" : "[无防御]";
    }

    private void OnModeTogglePressed() => SetEditMode(!IsEditMode);


public void SetEditMode(bool editMode)
{
    if (IsEditMode == editMode) return;
    
    IsEditMode = editMode;

    if (IsEditMode)
    {
        modeToggleButton.Text = "🎮 游玩模式";
        var style = new StyleBoxFlat { BgColor = new Color(0.5f, 0.3f, 0.3f, 0.9f) };
        style.SetCornerRadiusAll(8);
        modeToggleButton.AddThemeStyleboxOverride("normal", style);
        var hoverStyle = new StyleBoxFlat { BgColor = new Color(0.6f, 0.4f, 0.4f, 0.95f) };
        hoverStyle.SetCornerRadiusAll(8);
        modeToggleButton.AddThemeStyleboxOverride("hover", hoverStyle);

        gameManager?.gridManager?.CloseRange();
        gameManager?.gridManager?.HideAttackRange();
        gameManager?.gridManager?.ClearWeaponRange();
        var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
        actionMenu?.Hide();
        gameManager?.ClearSelectedInfantry();

        // ✅ 关键修复：记录当前迷雾状态（从 FogOfWarManager 直接读取）
        var fowManager = GetTree()?.GetFirstNodeInGroup("fog_of_war_manager") as FogOfWarManager;
        
        // ✅ 确保 FogOfWarManager 已初始化
        if (fowManager != null && !fowManager.isInitialized)
        {
            // 强制初始化
            fowManager.CallDeferred(nameof(FogOfWarManager.DeferredInit));
        }
        
        fogStateBeforeEdit = fowManager?.isFogOfWarEnabled ?? true;
        GD.Print($"[TerrainEditor] 进入编辑模式，记录迷雾状态: {fogStateBeforeEdit}");

        // ✅ 关闭迷雾
        gameManager?.SetFogOfWarEnabled(false);
        
        // ✅ 同步全局配置面板中的复选框
        if (fogOfWarCheckBox != null)
        {
            fogOfWarCheckBox.ButtonPressed = false;
            fogOfWarCheckBox.Disabled = true;  // 编辑模式下禁用
        }
    }
    else
    {
        modeToggleButton.Text = "🛠️ 编辑模式";
        var style = new StyleBoxFlat { BgColor = new Color(0.3f, 0.5f, 0.3f, 0.9f) };
        style.SetCornerRadiusAll(8);
        modeToggleButton.AddThemeStyleboxOverride("normal", style);
        var hoverStyle = new StyleBoxFlat { BgColor = new Color(0.4f, 0.6f, 0.4f, 0.95f) };
        hoverStyle.SetCornerRadiusAll(8);
        modeToggleButton.AddThemeStyleboxOverride("hover", hoverStyle);

        CloseAllMenus();

        GD.Print($"[TerrainEditor] 退出编辑模式，恢复迷雾状态: {fogStateBeforeEdit}");
        
        // ✅ 关键修复：先恢复迷雾状态，再解锁UI
        gameManager?.SetFogOfWarEnabled(fogStateBeforeEdit);
        
        // ✅ 关键修复：强制刷新迷雾（确保遮罩已创建）
        var fowManager = GetTree()?.GetFirstNodeInGroup("fog_of_war_manager") as FogOfWarManager;
        if (fogStateBeforeEdit && fowManager != null)
        {
            // 使用 CallDeferred 确保在下一帧执行，给 SetFogOfWarEnabled 时间完成初始化
            CallDeferred(nameof(DeferredRefreshFog));
        }
        
        // ✅ 解锁全局配置面板中的复选框并同步状态
        if (fogOfWarCheckBox != null)
        {
            fogOfWarCheckBox.Disabled = false;
            fogOfWarCheckBox.ButtonPressed = fogStateBeforeEdit;
        }
    }
}

private void DeferredRefreshFog()
{
    var fowManager = GetTree()?.GetFirstNodeInGroup("fog_of_war_manager") as FogOfWarManager;
    if (fowManager != null)
    {
        fowManager.ForceRefresh();
        GD.Print("[TerrainEditor] 延迟刷新迷雾完成");
    }
}
   private void CloseAllMenus()
    {
        selectionMenuPanel.Visible = false;
        terrainMenuPanel.Visible = false;
        propertyEditorPanel.Visible = false;
        spawnMenuPanel.Visible = false;
        globalConfigPanel.Visible = false;
        isMenuOpen = false;
        selectedGrid = null;
        currentEditingTarget = null;
        currentSpawnMode = SpawnMode.None;
        isDestroyMode = false;
        selectedTemplatePath = "";
    }



private void OnTerrainSelected(GridType newTerrain)
{
    if (selectedGrid == null) return;
    
    SaveCustomDamageValues(selectedGrid);
    SaveAmmoFuelValues(selectedGrid);

    GridType oldTerrain = selectedGrid.gridType;
    selectedGrid.gridType = newTerrain;

    UpdateGridVisual(selectedGrid);
    
    // ✅ 新增：刷新属性编辑器标题
    if (propertyEditorPanel.Visible && currentEditingTarget == selectedGrid)
    {
        propertyEditorTitle.Text = $"编辑格子: {selectedGrid.GridIndex} ({selectedGrid.gridType})";
    }
    
    GD.Print($"[TerrainEditor] 地形修改: {oldTerrain} -> {newTerrain} @ {selectedGrid.GridIndex}");

}    private void UpdateGridVisual(Grids grid)
    {
        var sprite = grid.GetNodeOrNull<Sprite2D>("Sprite2D");
        if (sprite == null) return;

        Color terrainColor = terrainColors.GetValueOrDefault(grid.gridType, Colors.White);
        var tween = CreateTween();
        tween.TweenProperty(sprite, "modulate", terrainColor, 0.3f)
             .SetTrans(Tween.TransitionType.Sine)
             .SetEase(Tween.EaseType.InOut);
    }

    private void LoadCustomDamageValues(Grids grid)
    {
        if (grid == null) return;
        canDestroyCheckBox.ButtonPressed = grid.canDestroyUnit;
        fixedDamageSpinBox.Value = grid.fixedDamagePerTurn;
        fixedAttackSpinBox.Value = grid.fixedAttackPerTurn;
        canOverMaxCheckBox.ButtonPressed = grid.canOverMaxHealth;
    }

    private void LoadAmmoFuelValues(Grids grid)
    {
        if (grid == null) return;
        fixedAmmoSpinBox.Value = grid.fixedAmmoChangePerTurn;
        ammoCanOverMaxCheckBox.ButtonPressed = grid.ammoCanOverMax;
        ammoCanReachZeroCheckBox.ButtonPressed = grid.ammoCanReachZero;
        fixedFuelSpinBox.Value = grid.fixedFuelChangePerTurn;
        fuelCanOverMaxCheckBox.ButtonPressed = grid.fuelCanOverMax;
        fuelCanReachZeroCheckBox.ButtonPressed = grid.fuelCanReachZero;
    }

    private void SaveCustomDamageValues(Grids grid)
    {
        if (grid == null) return;
        grid.canDestroyUnit = canDestroyCheckBox.ButtonPressed;
        grid.fixedDamagePerTurn = (int)fixedDamageSpinBox.Value;
        grid.fixedAttackPerTurn = (int)fixedAttackSpinBox.Value;
        grid.canOverMaxHealth = canOverMaxCheckBox.ButtonPressed;
    }

    private void SaveAmmoFuelValues(Grids grid)
    {
        if (grid == null) return;
        grid.fixedAmmoChangePerTurn = (int)fixedAmmoSpinBox.Value;
        grid.ammoCanOverMax = ammoCanOverMaxCheckBox.ButtonPressed;
        grid.ammoCanReachZero = ammoCanReachZeroCheckBox.ButtonPressed;
        grid.fixedFuelChangePerTurn = (int)fixedFuelSpinBox.Value;
        grid.fuelCanOverMax = fuelCanOverMaxCheckBox.ButtonPressed;
        grid.fuelCanReachZero = fuelCanReachZeroCheckBox.ButtonPressed;
    }

    public bool ShouldBlockUnitOperations()
    {
        return IsEditMode || isMenuOpen;
    }

    private bool isDraggingMenu = false;
    private Vector2 dragOffset = Vector2.Zero;
    private Control currentDraggedMenu = null;

    private void MakeMenuDraggable(Control menuPanel, Control dragHandle = null)
    {
        Control handle = dragHandle ?? menuPanel;

        handle.GuiInput += (InputEvent @event) => {
            if (@event is InputEventMouseButton mouseEvent)
            {
                if (mouseEvent.ButtonIndex == MouseButton.Left)
                {
                    if (mouseEvent.Pressed)
                    {
                        isDraggingMenu = true;
                        currentDraggedMenu = menuPanel;
                        dragOffset = menuPanel.GlobalPosition - handle.GetGlobalMousePosition();
                    }
                    else
                    {
                        isDraggingMenu = false;
                        currentDraggedMenu = null;
                    }
                }
            }
        };
    }

    public override void _Process(double delta)
    {
        if (isDraggingMenu && currentDraggedMenu != null)
        {
            Vector2 newPosition = currentDraggedMenu.GetGlobalMousePosition() + dragOffset;

            var viewportSize = GetViewport().GetVisibleRect().Size;
            newPosition.X = Mathf.Clamp(newPosition.X, 0, viewportSize.X - currentDraggedMenu.Size.X);
            newPosition.Y = Mathf.Clamp(newPosition.Y, 0, viewportSize.Y - currentDraggedMenu.Size.Y);

            currentDraggedMenu.GlobalPosition = newPosition;
        }
    }

    // ========== ✅ 全局配置面板 ==========
    private void CreateGlobalConfigPanel()
    {
        openGlobalConfigBtn = new Button();
        openGlobalConfigBtn.Name = "OpenGlobalConfigBtn";
        openGlobalConfigBtn.Text = "⚙️ 全局配置";
        openGlobalConfigBtn.CustomMinimumSize = new Vector2(120, 40);
        openGlobalConfigBtn.AddThemeFontSizeOverride("font_size", 14);

        var btnStyle = new StyleBoxFlat();
        btnStyle.BgColor = new Color(0.3f, 0.4f, 0.6f, 0.9f);
        btnStyle.SetCornerRadiusAll(8);
        openGlobalConfigBtn.AddThemeStyleboxOverride("normal", btnStyle);

        openGlobalConfigBtn.Pressed += OpenGlobalConfigPanel;
        AddChild(openGlobalConfigBtn);

        var viewportSize = GetViewport().GetVisibleRect().Size;
        openGlobalConfigBtn.SetAnchorsPreset(LayoutPreset.TopLeft);
        openGlobalConfigBtn.Position = new Vector2(viewportSize.X - 290, viewportSize.Y - 70);

        globalConfigPanel = new Panel();
        globalConfigPanel.Name = "GlobalConfigPanel";
        globalConfigPanel.CustomMinimumSize = new Vector2(400, 600);
        globalConfigPanel.SetAnchorsPreset(LayoutPreset.Center);
        globalConfigPanel.Visible = false;
        globalConfigPanel.ZIndex = 1300;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.08f, 0.1f, 0.15f, 0.98f);
        panelStyle.SetCornerRadiusAll(16);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.BorderColor = new Color(0.5f, 0.7f, 0.9f);
        globalConfigPanel.AddThemeStyleboxOverride("panel", panelStyle);

        var titleLabel = new Label();
        titleLabel.Text = "⚙️ 全局配置面板";
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.9f, 1f));
        titleLabel.CustomMinimumSize = new Vector2(0, 40);
        titleLabel.Position = new Vector2(0, 15);
        titleLabel.Size = new Vector2(400, 40);
        globalConfigPanel.AddChild(titleLabel);

        var closeBtn = new Button();
        closeBtn.Text = "✕";
        closeBtn.CustomMinimumSize = new Vector2(32, 32);
        closeBtn.Position = new Vector2(360, 12);
        closeBtn.Pressed += CloseGlobalConfigPanel;
        globalConfigPanel.AddChild(closeBtn);

        var scroll = new ScrollContainer();
        scroll.Position = new Vector2(20, 60);
        scroll.Size = new Vector2(360, 520);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

        globalConfigContainer = new VBoxContainer();
        globalConfigContainer.AddThemeConstantOverride("separation", 10);
        globalConfigContainer.Size = new Vector2(360, 0);

        scroll.AddChild(globalConfigContainer);
        globalConfigPanel.AddChild(scroll);

        AddChild(globalConfigPanel);
        MakeMenuDraggable(globalConfigPanel);

        BuildGlobalConfigContent();
    }
private Label CreateConfigSectionTitle(string text)
{
    var label = new Label();
    label.Text = text;
    label.AddThemeFontSizeOverride("font_size", 15);
    label.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.3f));
    label.CustomMinimumSize = new Vector2(0, 28);
    return label;
}

private HBoxContainer CreateVisionConfigRow(string name, int currentValue, Action<int> onChanged)
{
    var hbox = new HBoxContainer();
    hbox.AddThemeConstantOverride("separation", 8);
    hbox.CustomMinimumSize = new Vector2(0, 32);

    var nameLabel = new Label();
    nameLabel.Text = name;
    nameLabel.CustomMinimumSize = new Vector2(100, 24);
    nameLabel.AddThemeFontSizeOverride("font_size", 12);
    hbox.AddChild(nameLabel);

    var spinBox = new SpinBox();
    spinBox.MinValue = 0;
    spinBox.MaxValue = 20;
    spinBox.Value = currentValue;
    spinBox.CustomMinimumSize = new Vector2(80, 28);
    spinBox.AddThemeFontSizeOverride("font_size", 12);
    spinBox.ValueChanged += (newVal) => onChanged((int)newVal);
    hbox.AddChild(spinBox);

    var hint = new Label();
    hint.Text = "格";
    hint.AddThemeFontSizeOverride("font_size", 11);
    hint.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
    hbox.AddChild(hint);

    return hbox;
}

private VBoxContainer CreateWeaponVisionConfigRow(string name, int currentRange, bool isIndependent,
    Action<int, bool> onChanged)
{
    var vbox = new VBoxContainer();
    vbox.AddThemeConstantOverride("separation", 4);

    var hbox1 = new HBoxContainer();
    hbox1.AddThemeConstantOverride("separation", 8);

    var nameLabel = new Label();
    nameLabel.Text = name;
    nameLabel.CustomMinimumSize = new Vector2(100, 24);
    nameLabel.AddThemeFontSizeOverride("font_size", 12);
    hbox1.AddChild(nameLabel);

    var spinBox = new SpinBox();
    spinBox.MinValue = 0;
    spinBox.MaxValue = 20;
    spinBox.Value = currentRange;
    spinBox.CustomMinimumSize = new Vector2(80, 28);
    spinBox.AddThemeFontSizeOverride("font_size", 12);
    hbox1.AddChild(spinBox);

    vbox.AddChild(hbox1);

    var hbox2 = new HBoxContainer();
    hbox2.AddThemeConstantOverride("separation", 8);

    var modeLabel = new Label();
    modeLabel.Text = "  模式:";
    modeLabel.CustomMinimumSize = new Vector2(50, 24);
    modeLabel.AddThemeFontSizeOverride("font_size", 11);
    hbox2.AddChild(modeLabel);

    var modeOption = new OptionButton();
    modeOption.AddItem("正常(菱形)", 0);
    modeOption.AddItem("独立(攻击范围)", 1);
    modeOption.Selected = isIndependent ? 1 : 0;
    modeOption.CustomMinimumSize = new Vector2(140, 28);
    modeOption.AddThemeFontSizeOverride("font_size", 11);
    hbox2.AddChild(modeOption);

    vbox.AddChild(hbox2);

    spinBox.ValueChanged += (newRange) => {
        onChanged((int)newRange, modeOption.Selected == 1);
    };
    modeOption.ItemSelected += (index) => {
        onChanged((int)spinBox.Value, index == 1);
    };

    return vbox;
}

private HBoxContainer CreateTerrainVisionRow(GridType terrain, int currentBonus, Action<int> onChanged)
{
    var hbox = new HBoxContainer();
    hbox.AddThemeConstantOverride("separation", 8);
    hbox.CustomMinimumSize = new Vector2(0, 28);

    var nameLabel = new Label();
    nameLabel.Text = terrain.ToString();
    nameLabel.CustomMinimumSize = new Vector2(120, 24);
    nameLabel.AddThemeFontSizeOverride("font_size", 11);
    hbox.AddChild(nameLabel);

    var spinBox = new SpinBox();
    spinBox.MinValue = -10;
    spinBox.MaxValue = 10;
    spinBox.Value = currentBonus;
    spinBox.CustomMinimumSize = new Vector2(70, 26);
    spinBox.AddThemeFontSizeOverride("font_size", 11);
    spinBox.ValueChanged += (newVal) => onChanged((int)newVal);
    hbox.AddChild(spinBox);

    var signLabel = new Label();
    string sign = currentBonus > 0 ? "+" : "";
    signLabel.Text = $"({sign}{currentBonus})";
    signLabel.AddThemeFontSizeOverride("font_size", 10);
    signLabel.AddThemeColorOverride("font_color",
        currentBonus > 0 ? new Color(0.3f, 1, 0.3f) :
        currentBonus < 0 ? new Color(1, 0.3f, 0.3f) : new Color(0.7f, 0.7f, 0.7f));
    hbox.AddChild(signLabel);

    return hbox;
}
private void OpenGlobalConfigPanel()
{
    CloseAllMenus();
    globalConfigPanel.Visible = true;
    isMenuOpen = true;

    // ✅ 关键修复：从 FogOfWarManager 直接读取当前状态，而不是依赖复选框
    var fowManager = GetTree()?.GetFirstNodeInGroup("fog_of_war_manager") as FogOfWarManager;
    bool currentFogState = fowManager?.isFogOfWarEnabled ?? gameManager?.IsFogOfWarEnabled() ?? true;
    
    if (fogOfWarCheckBox != null)
    {
        // 断开信号避免循环触发
        var callable = new Callable(this, nameof(OnFogCheckBoxToggled));
        if (fogOfWarCheckBox.IsConnected(BaseButton.SignalName.Toggled, callable))
        {
            fogOfWarCheckBox.Disconnect(BaseButton.SignalName.Toggled, callable);
        }
        
        fogOfWarCheckBox.ButtonPressed = currentFogState;
        fogOfWarCheckBox.Disabled = IsEditMode;  // 编辑模式下禁用
        
        // 重新连接信号
        fogOfWarCheckBox.Connect(BaseButton.SignalName.Toggled, callable);
    }
}
private void RefreshAllUnitVision()
    {
        if (gameManager?.unitManager?.AllUnits != null)
        {
            foreach (var unit in gameManager.unitManager.AllUnits)
            {
                if (unit != null && IsInstanceValid(unit) && unit.useConfigVision)
                {
                    unit.visionRange = VisionConfig.GetUnitVisionRange(unit.GetType().Name);
                }
            }
        }

        if (gameManager?.weaponManager?.AllWeapons != null)
        {
            foreach (var weapon in gameManager.weaponManager.AllWeapons)
            {
                if (weapon != null && IsInstanceValid(weapon) && weapon.useConfigVision)
                {
                    weapon.visionRange = VisionConfig.GetWeaponVisionRange(weapon.GetType().Name);
                    weapon.visionMode = VisionConfig.IsWeaponIndependentMode(weapon.GetType().Name)
                        ? VisionMode.Independent : VisionMode.Normal;
                }
            }
        }

        gameManager?.fogOfWarManager?.RefreshFog();
        GD.Print("[GlobalConfig] 所有单位视野已刷新");
    }



    private void BuildGlobalConfigContent()
    {
        foreach (var child in globalConfigContainer.GetChildren())
            child.QueueFree();

        var fogSection = CreateConfigSectionTitle("🌫️ 战争迷雾");
        globalConfigContainer.AddChild(fogSection);

        var fogHBox = new HBoxContainer();
        fogHBox.AddThemeConstantOverride("separation", 10);

        var fogLabel = new Label();
        fogLabel.Text = "启用战争迷雾:";
        fogLabel.CustomMinimumSize = new Vector2(120, 28);
        fogLabel.AddThemeFontSizeOverride("font_size", 13);
        fogHBox.AddChild(fogLabel);

        fogOfWarCheckBox = new CheckBox();
        fogOfWarCheckBox.ButtonPressed = gameManager?.IsFogOfWarEnabled() ?? true;
        fogOfWarCheckBox.CustomMinimumSize = new Vector2(28, 28);
        fogOfWarCheckBox.Toggled += OnFogCheckBoxToggled;
        fogHBox.AddChild(fogOfWarCheckBox);

        var fogHint = new Label();
        fogHint.Text = "(编辑模式自动关闭)";
        fogHint.AddThemeFontSizeOverride("font_size", 10);
        fogHint.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        fogHBox.AddChild(fogHint);

        globalConfigContainer.AddChild(fogHBox);

        var unitSection = CreateConfigSectionTitle("👁️ 单位基础视野配置");
        globalConfigContainer.AddChild(unitSection);

        var unitConfigs = VisionConfig.GetAllUnitVisionConfigs();
        foreach (var kvp in unitConfigs)
        {
            var row = CreateVisionConfigRow(kvp.Key, kvp.Value, (newVal) => {
                VisionConfig.SetUnitVisionRange(kvp.Key, newVal);
                RefreshAllUnitVision();
            });
            globalConfigContainer.AddChild(row);
        }

        var weaponSection = CreateConfigSectionTitle("⚔️ 兵器基础视野配置");
        globalConfigContainer.AddChild(weaponSection);

        var weaponConfigs = VisionConfig.GetAllWeaponVisionConfigs();
        foreach (var kvp in weaponConfigs)
        {
            var row = CreateWeaponVisionConfigRow(kvp.Key, kvp.Value,
                VisionConfig.IsWeaponIndependentMode(kvp.Key),
                (newRange, newMode) => {
                    VisionConfig.SetWeaponVisionRange(kvp.Key, newRange);
                    VisionConfig.SetWeaponIndependentMode(kvp.Key, newMode);
                    RefreshAllUnitVision();
                });
            globalConfigContainer.AddChild(row);
        }

        // ✅ 全局默认地形加成
        var terrainSection = CreateConfigSectionTitle("🗺️ 全局默认地形视野加成");
        globalConfigContainer.AddChild(terrainSection);

        var terrainConfigs = VisionConfig.GetAllTerrainVisionConfigs();
        foreach (var kvp in terrainConfigs)
        {
            var row = CreateTerrainVisionRow(kvp.Key, kvp.Value, (newVal) => {
                VisionConfig.SetTerrainVisionBonus(kvp.Key, newVal);
                RefreshAllUnitVision();
            });
            globalConfigContainer.AddChild(row);
        }

        // ✅ 新增：单位类型×地形 专属加成矩阵配置
        var unitMatrixSection = CreateConfigSectionTitle("👁️ 单位类型×地形 专属加成矩阵");
        globalConfigContainer.AddChild(unitMatrixSection);

        var unitMatrixBtn = new Button();
        unitMatrixBtn.Text = "📊 打开单位地形加成矩阵编辑器";
        unitMatrixBtn.CustomMinimumSize = new Vector2(300, 40);
        unitMatrixBtn.AddThemeFontSizeOverride("font_size", 13);
        unitMatrixBtn.Pressed += OpenUnitTerrainMatrixEditor;
        globalConfigContainer.AddChild(unitMatrixBtn);

        // ✅ 新增：兵器类型×地形 专属加成矩阵配置
        var weaponMatrixSection = CreateConfigSectionTitle("⚔️ 兵器类型×地形 专属加成矩阵");
        globalConfigContainer.AddChild(weaponMatrixSection);

        var weaponMatrixBtn = new Button();
        weaponMatrixBtn.Text = "📊 打开兵器地形加成矩阵编辑器";
        weaponMatrixBtn.CustomMinimumSize = new Vector2(300, 40);
        weaponMatrixBtn.AddThemeFontSizeOverride("font_size", 13);
        weaponMatrixBtn.Pressed += OpenWeaponTerrainMatrixEditor;
        globalConfigContainer.AddChild(weaponMatrixBtn);

        // ✅ AI 与地图导出配置
        var aiSection = CreateConfigSectionTitle("🤖 AI 控制与地图导出");
        globalConfigContainer.AddChild(aiSection);

        var aiMgr = GetTree()?.GetFirstNodeInGroup("ai_manager") as AI_Manager;

        var p1AiHBox = new HBoxContainer();
        p1AiHBox.AddThemeConstantOverride("separation", 10);
        var p1AiLabel = new Label();
        p1AiLabel.Text = "P1 AI 操控:";
        p1AiLabel.CustomMinimumSize = new Vector2(120, 28);
        p1AiLabel.AddThemeFontSizeOverride("font_size", 13);
        p1AiHBox.AddChild(p1AiLabel);
        var p1AiCheck = new CheckBox();
        p1AiCheck.ButtonPressed = aiMgr?.p1AIEnabled ?? false;
        p1AiCheck.CustomMinimumSize = new Vector2(28, 28);
        p1AiCheck.Toggled += (pressed) => {
            if (aiMgr != null) aiMgr.p1AIEnabled = pressed;
            GD.Print($"[全局配置] P1 AI = {pressed}");
        };
        p1AiHBox.AddChild(p1AiCheck);
        globalConfigContainer.AddChild(p1AiHBox);

        var p2AiHBox = new HBoxContainer();
        p2AiHBox.AddThemeConstantOverride("separation", 10);
        var p2AiLabel = new Label();
        p2AiLabel.Text = "P2 AI 操控:";
        p2AiLabel.CustomMinimumSize = new Vector2(120, 28);
        p2AiLabel.AddThemeFontSizeOverride("font_size", 13);
        p2AiHBox.AddChild(p2AiLabel);
        var p2AiCheck = new CheckBox();
        p2AiCheck.ButtonPressed = aiMgr?.p2AIEnabled ?? false;
        p2AiCheck.CustomMinimumSize = new Vector2(28, 28);
        p2AiCheck.Toggled += (pressed) => {
            if (aiMgr != null) aiMgr.p2AIEnabled = pressed;
            GD.Print($"[全局配置] P2 AI = {pressed}");
        };
        p2AiHBox.AddChild(p2AiCheck);
        globalConfigContainer.AddChild(p2AiHBox);

        var autoEndHBox = new HBoxContainer();
        autoEndHBox.AddThemeConstantOverride("separation", 10);
        var autoEndLabel = new Label();
        autoEndLabel.Text = "自动切换回合:";
        autoEndLabel.CustomMinimumSize = new Vector2(120, 28);
        autoEndLabel.AddThemeFontSizeOverride("font_size", 13);
        autoEndHBox.AddChild(autoEndLabel);
        var autoEndCheck = new CheckBox();
        autoEndCheck.ButtonPressed = aiMgr?.autoEndTurn ?? false;
        autoEndCheck.CustomMinimumSize = new Vector2(28, 28);
        autoEndCheck.Toggled += (pressed) => {
            if (aiMgr != null) aiMgr.autoEndTurn = pressed;
            GD.Print($"[全局配置] 自动切换回合 = {pressed}");
        };
        autoEndHBox.AddChild(autoEndCheck);
        globalConfigContainer.AddChild(autoEndHBox);

        var exportMapBtn = new Button();
        exportMapBtn.Text = "📤 导出地图配置为 txt";
        exportMapBtn.CustomMinimumSize = new Vector2(300, 40);
        exportMapBtn.AddThemeFontSizeOverride("font_size", 13);
        exportMapBtn.Pressed += ExportMapToTxt;
        globalConfigContainer.AddChild(exportMapBtn);

        var btnSeparator = new HSeparator();
        btnSeparator.CustomMinimumSize = new Vector2(0, 10);
        globalConfigContainer.AddChild(btnSeparator);

        var btnHBox = new HBoxContainer();
        btnHBox.AddThemeConstantOverride("separation", 10);

        var resetBtn = new Button();
        resetBtn.Text = "🔄 重置默认";
        resetBtn.CustomMinimumSize = new Vector2(150, 36);
        resetBtn.AddThemeFontSizeOverride("font_size", 13);
        resetBtn.Pressed += () => {
            VisionConfig.ResetToDefaults();
            BuildGlobalConfigContent();
            RefreshAllUnitVision();
        };
        btnHBox.AddChild(resetBtn);

        var exportBtn = new Button();
        exportBtn.Text = "📋 导出配置";
        exportBtn.CustomMinimumSize = new Vector2(150, 36);
        exportBtn.AddThemeFontSizeOverride("font_size", 13);
        exportBtn.Pressed += () => {
            var info = VisionConfig.ExportConfig();
            GD.Print(info);
        };
        btnHBox.AddChild(exportBtn);

        globalConfigContainer.AddChild(btnHBox);
    }

    // ✅ 新增：单位地形加成矩阵编辑器窗口
    private Panel unitMatrixEditorPanel;
private void OpenUnitTerrainMatrixEditor()
{
    if (unitMatrixEditorPanel == null) CreateUnitMatrixEditorPanel();

    var container = unitMatrixEditorPanel.GetNodeOrNull<VBoxContainer>("MatrixContainer");
    if (container != null)
    {
        foreach (var child in container.GetChildren()) 
            child.QueueFree();

        var allUnitTypes = new List<string>(VisionConfig.GetAllUnitVisionConfigs().Keys);
        var allTerrainTypes = new List<GridType>();
        foreach (GridType gt in Enum.GetValues(typeof(GridType)))
            allTerrainTypes.Add(gt);

        if (allUnitTypes.Count == 0)
        {
            var emptyLabel = new Label();
            emptyLabel.Text = "⚠️ 无单位类型配置";
            emptyLabel.AddThemeFontSizeOverride("font_size", 14);
            emptyLabel.AddThemeColorOverride("font_color", Colors.Red);
            container.AddChild(emptyLabel);
        }
        else
        {
            foreach (var unitType in allUnitTypes)
            {
                var unitLabel = new Label();
                unitLabel.Text = $"=== {unitType} ===";
                unitLabel.AddThemeFontSizeOverride("font_size", 14);
                unitLabel.AddThemeColorOverride("font_color", Colors.Yellow);
                container.AddChild(unitLabel);

                var table = VisionConfig.GetUnitTerrainBonusTable(unitType);
                foreach (var terrain in allTerrainTypes)
                {
                    var hbox = new HBoxContainer();
                    hbox.AddThemeConstantOverride("separation", 8);

                    var terrainLabel = new Label();
                    terrainLabel.Text = terrain.ToString();
                    terrainLabel.CustomMinimumSize = new Vector2(100, 24);
                    terrainLabel.AddThemeFontSizeOverride("font_size", 11);
                    hbox.AddChild(terrainLabel);

                    int currentValue = table.ContainsKey(terrain) ? table[terrain] : 0;

                    var spinBox = new SpinBox();
                    spinBox.MinValue = -10;
                    spinBox.MaxValue = 10;
                    spinBox.Value = currentValue;
                    spinBox.CustomMinimumSize = new Vector2(70, 26);
                    spinBox.AddThemeFontSizeOverride("font_size", 11);

                    string capturedUnit = unitType;
                    GridType capturedTerrain = terrain;
                    spinBox.ValueChanged += (newVal) => {
                        VisionConfig.SetUnitTerrainBonus(capturedUnit, capturedTerrain, (int)newVal);
                        RefreshAllUnitVision();
                    };

                    hbox.AddChild(spinBox);

                    var signLabel = new Label();
                    string sign = currentValue > 0 ? "+" : "";
                    signLabel.Text = $"({sign}{currentValue})";
                    signLabel.AddThemeFontSizeOverride("font_size", 10);
                    signLabel.AddThemeColorOverride("font_color",
                        currentValue > 0 ? new Color(0.3f, 1, 0.3f) :
                        currentValue < 0 ? new Color(1, 0.3f, 0.3f) : new Color(0.7f, 0.7f, 0.7f));
                    hbox.AddChild(signLabel);

                    container.AddChild(hbox);
                }
            }
        }
    }

    unitMatrixEditorPanel.Visible = true;
    isMenuOpen = true;
    container?.QueueSort();
}

private void OpenWeaponTerrainMatrixEditor()
{
    if (weaponMatrixEditorPanel == null) CreateWeaponMatrixEditorPanel();

    var container = weaponMatrixEditorPanel.GetNodeOrNull<VBoxContainer>("MatrixContainer");
    if (container != null)
    {
        foreach (var child in container.GetChildren()) 
            child.QueueFree();

        var allWeaponTypes = new List<string>(VisionConfig.GetAllWeaponVisionConfigs().Keys);
        var allTerrainTypes = new List<GridType>();
        foreach (GridType gt in Enum.GetValues(typeof(GridType)))
            allTerrainTypes.Add(gt);

        if (allWeaponTypes.Count == 0)
        {
            var emptyLabel = new Label();
            emptyLabel.Text = "⚠️ 无兵器类型配置";
            emptyLabel.AddThemeFontSizeOverride("font_size", 14);
            emptyLabel.AddThemeColorOverride("font_color", Colors.Red);
            container.AddChild(emptyLabel);
        }
        else
        {
            foreach (var weaponType in allWeaponTypes)
            {
                var weaponLabel = new Label();
                weaponLabel.Text = $"=== {weaponType} ===";
                weaponLabel.AddThemeFontSizeOverride("font_size", 14);
                weaponLabel.AddThemeColorOverride("font_color", Colors.Yellow);
                container.AddChild(weaponLabel);

                foreach (var terrain in allTerrainTypes)
                {
                    var hbox = new HBoxContainer();
                    hbox.AddThemeConstantOverride("separation", 8);

                    var terrainLabel = new Label();
                    terrainLabel.Text = terrain.ToString();
                    terrainLabel.CustomMinimumSize = new Vector2(100, 24);
                    terrainLabel.AddThemeFontSizeOverride("font_size", 11);
                    hbox.AddChild(terrainLabel);

                    int currentVal = VisionConfig.GetWeaponTerrainVisionBonus(weaponType, terrain);

                    var spinBox = new SpinBox();
                    spinBox.MinValue = -10;
                    spinBox.MaxValue = 10;
                    spinBox.Value = currentVal;
                    spinBox.CustomMinimumSize = new Vector2(70, 26);
                    spinBox.AddThemeFontSizeOverride("font_size", 11);

                    string capturedWeapon = weaponType;
                    GridType capturedTerrain = terrain;
                    spinBox.ValueChanged += (newVal) => {
                        VisionConfig.SetWeaponTerrainBonus(capturedWeapon, capturedTerrain, (int)newVal);
                        RefreshAllUnitVision();
                    };

                    hbox.AddChild(spinBox);
                    container.AddChild(hbox);
                }
            }
        }
    }

    weaponMatrixEditorPanel.Visible = true;
    isMenuOpen = true;
    container?.QueueSort();
}

    private void CreateUnitMatrixEditorPanel()
    {
        unitMatrixEditorPanel = new Panel();
        unitMatrixEditorPanel.Name = "UnitMatrixEditorPanel";
        unitMatrixEditorPanel.CustomMinimumSize = new Vector2(450, 600);
        unitMatrixEditorPanel.SetAnchorsPreset(LayoutPreset.Center);
        unitMatrixEditorPanel.Visible = false;
        unitMatrixEditorPanel.ZIndex = 1400;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.08f, 0.1f, 0.15f, 0.98f);
        panelStyle.SetCornerRadiusAll(16);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.BorderColor = new Color(0.5f, 0.7f, 0.9f);
        unitMatrixEditorPanel.AddThemeStyleboxOverride("panel", panelStyle);

        var titleLabel = new Label();
        titleLabel.Text = "📊 单位类型×地形 视野加成矩阵";
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.AddThemeFontSizeOverride("font_size", 20);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.9f, 1f));
        titleLabel.CustomMinimumSize = new Vector2(0, 40);
        titleLabel.Position = new Vector2(0, 15);
        titleLabel.Size = new Vector2(450, 40);
        unitMatrixEditorPanel.AddChild(titleLabel);

        var closeBtn = new Button();
        closeBtn.Text = "✕";
        closeBtn.CustomMinimumSize = new Vector2(32, 32);
        closeBtn.Position = new Vector2(410, 12);
        closeBtn.Pressed += () => { unitMatrixEditorPanel.Visible = false; isMenuOpen = false; };
        unitMatrixEditorPanel.AddChild(closeBtn);

        var scroll = new ScrollContainer();
        scroll.Position = new Vector2(20, 60);
        scroll.Size = new Vector2(410, 520);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

        var container = new VBoxContainer();
        container.Name = "MatrixContainer";
        container.AddThemeConstantOverride("separation", 6);
        container.Size = new Vector2(410, 0);

        scroll.AddChild(container);
        unitMatrixEditorPanel.AddChild(scroll);

        AddChild(unitMatrixEditorPanel);
        MakeMenuDraggable(unitMatrixEditorPanel);
    }

    // ✅ 新增：兵器地形加成矩阵编辑器
    private Panel weaponMatrixEditorPanel;
    private void CreateWeaponMatrixEditorPanel()
    {
        weaponMatrixEditorPanel = new Panel();
        weaponMatrixEditorPanel.Name = "WeaponMatrixEditorPanel";
        weaponMatrixEditorPanel.CustomMinimumSize = new Vector2(450, 400);
        weaponMatrixEditorPanel.SetAnchorsPreset(LayoutPreset.Center);
        weaponMatrixEditorPanel.Visible = false;
        weaponMatrixEditorPanel.ZIndex = 1400;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.08f, 0.1f, 0.15f, 0.98f);
        panelStyle.SetCornerRadiusAll(16);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.BorderColor = new Color(0.7f, 0.5f, 0.9f);
        weaponMatrixEditorPanel.AddThemeStyleboxOverride("panel", panelStyle);

        var titleLabel = new Label();
        titleLabel.Text = "📊 兵器类型×地形 视野加成矩阵";
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.AddThemeFontSizeOverride("font_size", 20);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.5f, 1f));
        titleLabel.CustomMinimumSize = new Vector2(0, 40);
        titleLabel.Position = new Vector2(0, 15);
        titleLabel.Size = new Vector2(450, 40);
        weaponMatrixEditorPanel.AddChild(titleLabel);

        var closeBtn = new Button();
        closeBtn.Text = "✕";
        closeBtn.CustomMinimumSize = new Vector2(32, 32);
        closeBtn.Position = new Vector2(410, 12);
        closeBtn.Pressed += () => { weaponMatrixEditorPanel.Visible = false; isMenuOpen = false; };
        weaponMatrixEditorPanel.AddChild(closeBtn);

        var scroll = new ScrollContainer();
        scroll.Position = new Vector2(20, 60);
        scroll.Size = new Vector2(410, 320);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

        var container = new VBoxContainer();
        container.Name = "MatrixContainer";
        container.AddThemeConstantOverride("separation", 6);
        container.Size = new Vector2(410, 0);

        scroll.AddChild(container);
        weaponMatrixEditorPanel.AddChild(scroll);

        AddChild(weaponMatrixEditorPanel);
        MakeMenuDraggable(weaponMatrixEditorPanel);
    }

    private void OnFogCheckBoxToggled(bool pressed)
{
    if (IsEditMode) return; // 编辑模式下忽略
    gameManager?.SetFogOfWarEnabled(pressed);
}
    private void CloseGlobalConfigPanel()
    {
        globalConfigPanel.Visible = false;
        isMenuOpen = false;
    }

    // ✅ 新的统一入口
    private struct EditTarget
    {
        public string TargetType;
        public string DisplayName;
        public object TargetObject;
        public Color Color;
    }

    // 所有可用地形类型
    private readonly GridType[] availableTerrainTypes = new GridType[]
    {
        GridType.GROUND, GridType.FOREST, GridType.ROAD,
        GridType.SEA, GridType.RIVER, GridType.HILL, GridType.METEORITE,
        GridType.PIPE, GridType.LAVA, GridType.BEACH, GridType.TP,
        GridType.REEF, GridType.WHIRLPOOL, GridType.LAVASIDE,
        GridType.SEAFOG, GridType.LANDFOG, GridType.WATERFALL,
        GridType.CLIFF, GridType.SLOPE, GridType.CAVE, GridType.HOLE,
        GridType.PIPESEAM, GridType.TRACK, GridType.STATION,
        GridType.BRIDGE, GridType.LAVABRIDGE, GridType.PASSABLEPIPE,
        GridType.SHIPGATE, GridType.OVERPASS, GridType.BROKENPIPE,
        GridType.RUINS, GridType.BROKENTRACK, GridType.LAVAFOG
    };

    private readonly Dictionary<GridType, Color> terrainColors = new()
    {
        { GridType.GROUND, new Color(0.4f, 0.7f, 0.3f) },
        { GridType.FOREST, new Color(0.1f, 0.5f, 0.1f) },
        { GridType.ROAD, new Color(0.6f, 0.6f, 0.5f) },
        { GridType.SEA, new Color(0.2f, 0.4f, 0.8f) },
        { GridType.RIVER, new Color(0.3f, 0.6f, 0.9f) },
        { GridType.HILL, new Color(0.5f, 0.4f, 0.2f) },
        { GridType.METEORITE, new Color(0.25f, 0.25f, 0.28f) },
        { GridType.PIPE, new Color(0.4f, 0.4f, 0.5f) },
        { GridType.LAVA, new Color(0.9f, 0.2f, 0.1f) },
        { GridType.BEACH, new Color(0.9f, 0.8f, 0.5f) },
        { GridType.TP, new Color(0.8f, 0.2f, 0.8f) },
        { GridType.REEF, new Color(0.2f, 0.7f, 0.6f) },
        { GridType.WHIRLPOOL, new Color(0.1f, 0.3f, 0.5f) },
        { GridType.LAVASIDE, new Color(0.8f, 0.3f, 0.1f) },
        { GridType.SEAFOG, new Color(0.5f, 0.6f, 0.7f) },
        { GridType.LANDFOG, new Color(0.6f, 0.6f, 0.5f) },
        { GridType.WATERFALL, new Color(0.4f, 0.7f, 0.9f) },
        { GridType.CLIFF, new Color(0.5f, 0.5f, 0.4f) },
        { GridType.SLOPE, new Color(0.6f, 0.5f, 0.3f) },
        { GridType.CAVE, new Color(0.3f, 0.25f, 0.2f) },
        { GridType.HOLE, new Color(0.2f, 0.2f, 0.15f) },
        { GridType.PIPESEAM, new Color(0.5f, 0.5f, 0.6f) },
        { GridType.TRACK, new Color(0.4f, 0.4f, 0.4f) },
        { GridType.STATION, new Color(0.6f, 0.6f, 0.5f) },
        { GridType.BRIDGE, new Color(0.5f, 0.5f, 0.6f) },
        { GridType.LAVABRIDGE, new Color(0.8f, 0.2f, 0.1f) },
        { GridType.PASSABLEPIPE, new Color(0.5f, 0.5f, 0.5f) },
        { GridType.SHIPGATE, new Color(0.3f, 0.5f, 0.6f) },
        { GridType.OVERPASS, new Color(0.7f, 0.7f, 0.8f) },
        { GridType.BROKENPIPE, new Color(0.3f, 0.3f, 0.3f) },
        { GridType.RUINS, new Color(0.4f, 0.35f, 0.3f) },
        { GridType.BROKENTRACK, new Color(0.3f, 0.3f, 0.25f) },
        { GridType.LAVAFOG, new Color(0.7f, 0.2f, 0.1f) }
    };

    // ✅ 导出地图配置为 txt
    private void ExportMapToTxt()
    {
        if (gridManager?.map == null)
        {
            GD.PrintErr("[ExportMap] GridManager 未初始化");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== AW-1-3 Map Export ===");
        sb.AppendLine($"Width: {gridManager.searchRange.X}");
        sb.AppendLine($"Height: {gridManager.searchRange.Y}");
        sb.AppendLine($"ExportTime: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // 1. 地形矩阵
        sb.AppendLine("=== Terrain Matrix [x,y]=Type ===");
        for (int y = 0; y < gridManager.searchRange.Y; y++)
        {
            for (int x = 0; x < gridManager.searchRange.X; x++)
            {
                var grid = gridManager.map[x, y];
                if (grid != null)
                    sb.Append($"[{x},{y}]={grid.gridType}  ");
            }
            sb.AppendLine();
        }
        sb.AppendLine();

        // 2. 单位
        sb.AppendLine("=== Units [x,y] Type Team HP ===");
        if (gameManager?.unitManager?.AllUnits != null)
        {
            foreach (var unit in gameManager.unitManager.AllUnits)
            {
                if (unit != null && IsInstanceValid(unit) && unit.grid != null)
                {
                    int hx = unit.grid.GridIndex.X;
                    int hy = unit.grid.GridIndex.Y;
                    string typeName = unit.GetType().Name;
                    int hp = Mathf.CeilToInt(unit.health / 10f);
                    sb.AppendLine($"[{hx},{hy}] {typeName} {unit.team} HP{hp}");
                }
            }
        }
        sb.AppendLine();

        // 3. 兵器
        sb.AppendLine("=== Weapons [x,y] Type Team HP ===");
        if (gameManager?.weaponManager?.AllWeapons != null)
        {
            foreach (var weapon in gameManager.weaponManager.AllWeapons)
            {
                if (weapon != null && IsInstanceValid(weapon) && weapon.grid != null)
                {
                    int hx = weapon.grid.GridIndex.X;
                    int hy = weapon.grid.GridIndex.Y;
                    string typeName = weapon.GetType().Name;
                    int hp = Mathf.CeilToInt(weapon.health / 10f);
                    sb.AppendLine($"[{hx},{hy}] {typeName} {weapon.team} HP{hp}");
                }
            }
        }
        sb.AppendLine();

        // 4. 设施
        sb.AppendLine("=== Facilities [x,y] Type Team ===");
        if (gridManager?.grids != null)
        {
            foreach (var grid in gridManager.grids)
            {
                if (grid?.city != null && IsInstanceValid(grid.city))
                {
                    int hx = grid.GridIndex.X;
                    int hy = grid.GridIndex.Y;
                    string typeName = grid.city.GetType().Name;
                    string team = grid.city.facilityTeam ?? "None";
                    sb.AppendLine($"[{hx},{hy}] {typeName} {team}");
                }
            }
        }
        sb.AppendLine();

        // 5. 资金统计
        sb.AppendLine("=== Funds Statistics ===");
        long pn1Funds = 0, p0Funds = 0, p1Funds = 0, p2Funds = 0;
        if (gameManager?.unitManager?.AllUnits != null)
        {
            foreach (var unit in gameManager.unitManager.AllUnits)
            {
                if (unit != null && IsInstanceValid(unit))
                {
                    switch (unit.team)
                    {
                        case "Player": pn1Funds += unit.cost; break;
                        case "Player0": p0Funds += unit.cost; break;
                        case "Player1": p1Funds += unit.cost; break;
                        case "Player2": p2Funds += unit.cost; break;
                    }
                }
            }
        }
        if (gameManager?.weaponManager?.AllWeapons != null)
        {
            foreach (var weapon in gameManager.weaponManager.AllWeapons)
            {
                if (weapon != null && IsInstanceValid(weapon))
                {
                    switch (weapon.team)
                    {
                        case "Player": pn1Funds += weapon.cost; break;
                        case "Player0": p0Funds += weapon.cost; break;
                        case "Player1": p1Funds += weapon.cost; break;
                        case "Player2": p2Funds += weapon.cost; break;
                    }
                }
            }
        }
        long totalFunds = pn1Funds + p0Funds + p1Funds + p2Funds;
        sb.AppendLine($"P-1 (Player):  {pn1Funds} G");
        sb.AppendLine($"P0  (Neutral): {p0Funds} G");
        sb.AppendLine($"P1  (Red):     {p1Funds} G");
        sb.AppendLine($"P2  (Blue):    {p2Funds} G");
        sb.AppendLine($"Total:         {totalFunds} G");
        sb.AppendLine();
        sb.AppendLine("=== End of Export ===");

        string content = sb.ToString();
        string fileName = $"AW13_MapExport_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string savedPath = "";
        bool savedToDownloads = false;

        // 尝试保存到下载目录
        try
        {
            string downloadDir = OS.GetSystemDir(OS.SystemDir.Downloads);
            if (!string.IsNullOrEmpty(downloadDir) && Directory.Exists(downloadDir))
            {
                string downloadPath = Path.Combine(downloadDir, fileName);
                File.WriteAllText(downloadPath, content);
                savedPath = downloadPath;
                savedToDownloads = true;
                GD.Print($"[ExportMap] 已保存到下载目录: {downloadPath}");
            }
        }
        catch (Exception ex)
        {
            GD.Print($"[ExportMap] 下载目录保存失败: {ex.Message}，将回退到用户目录");
        }

        // 回退到 user:// 目录
        if (!savedToDownloads)
        {
            try
            {
                string userDir = OS.GetUserDataDir();
                string userPath = Path.Combine(userDir, fileName);
                File.WriteAllText(userPath, content);
                savedPath = userPath;
                GD.Print($"[ExportMap] 已保存到用户目录: {userPath}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[ExportMap] 用户目录保存也失败: {ex.Message}");
            }
        }

        // 复制到剪贴板作为备选
        try
        {
            DisplayServer.ClipboardSet(content);
            GD.Print("[ExportMap] 内容已复制到剪贴板");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[ExportMap] 剪贴板复制失败: {ex.Message}");
        }

        // 显示提示
        string hintText;
        if (savedToDownloads)
            hintText = $"✅ 已导出到下载目录:\n{savedPath}\n📋 内容已复制到剪贴板";
        else if (!string.IsNullOrEmpty(savedPath))
            hintText = $"⚠️ 已保存到应用目录:\n{savedPath}\n📋 内容已复制到剪贴板\n(手机可在剪贴板粘贴分享)";
        else
            hintText = "❌ 文件保存失败\n📋 但内容已复制到剪贴板\n请手动粘贴保存";

        var hintLabel = new Label();
        hintLabel.Text = hintText;
        hintLabel.AddThemeFontSizeOverride("font_size", 13);
        hintLabel.AddThemeColorOverride("font_color", savedToDownloads ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.8f, 0.2f));
        hintLabel.HorizontalAlignment = HorizontalAlignment.Center;
        hintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hintLabel.Position = new Vector2(0, globalConfigPanel.Size.Y - 80);
        hintLabel.Size = new Vector2(globalConfigPanel.Size.X, 70);
        globalConfigPanel.AddChild(hintLabel);

        var timer = new Timer();
        timer.WaitTime = 3.0f;
        timer.OneShot = true;
        timer.Timeout += () => { hintLabel.QueueFree(); timer.QueueFree(); };
        globalConfigPanel.AddChild(timer);
        timer.Start();
    }
}