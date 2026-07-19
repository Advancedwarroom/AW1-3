using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ========== 建筑状态定义 ==========
public enum FacilityStatusType
{
    None,           // 无状态
    CannotProduce,  // 不可生产
    NoIncome        // 无收入
}

public partial class FacilityStatus
{
    public FacilityStatusType Type { get; set; }
    public int RemainingTurns { get; set; }  // 剩余回合数，-1=永久
    public string AppliedByTeam { get; set; }  // 由哪个势力施加/关联

    public bool IsExpired => RemainingTurns == 0;
    public bool IsPermanent => RemainingTurns < 0;

    public void Tick()
    {
        if (RemainingTurns > 0)
            RemainingTurns--;
    }

    public string GetDisplayName()
    {
        return Type switch
        {
            FacilityStatusType.CannotProduce => "不可生产",
            FacilityStatusType.NoIncome => "无收入",
            _ => "未知"
        };
    }
}

public partial class Facility : Node2D
{
    public Grids ParentGrid { get; private set; }

    [ExportGroup("生产模块")]
    [Export] public bool canProduce = false;
    [Export] public Godot.Collections.Array<string> producibleUnitNames = new();
    [Export] public bool instantMoveAfterProduction = false;
    [Export] public int maxProductionsPerTurn = 1;
    [Export] public int productionsThisTurn = 0;

    // ========== 状态栏系统 ==========
    [ExportGroup("初始状态（运行时生效）")]
    [Export] public Godot.Collections.Array<FacilityStatusData> initialStatuses = new();
    
    // 运行时状态列表（不Export）
    private List<FacilityStatus> activeStatuses = new();

    // 状态栏UI引用（运行时创建）
    private VBoxContainer statusBarContainer;
    private Dictionary<FacilityStatusType, Label> statusLabels = new();

    public bool CanProduceNow() => canProduce && productionsThisTurn < maxProductionsPerTurn && !HasStatus(FacilityStatusType.CannotProduce);

    public void ResetProductionCount()
    {
        productionsThisTurn = 0;
        TickStatuses();
        UpdateStatusBarUI();
    }

    [ExportGroup("设施基础")]
    [Export] public string facilityTeam = "";
    [Export] public int capturePointsRequired = 20;
    [Export] public int healMode = 0;
    [Export] public int healAmount = 0;
    [Export] public float healPercent = 0.0f;

    [ExportGroup("补给配置：数值驱动")]
    [Export] public int flareAmmoSupply = 0;
    [Export] public int explodeAmmoSupply = 0;
    [Export] public int primaryAmmoSupply = 0;
    [Export] public int fuelSupply = 0;

    [ExportGroup("补给支持兵种过滤（为空=不限制）")]
    [Export] public Godot.Collections.Array<string> supportedUnitTypesForHeal = new();
    [Export] public Godot.Collections.Array<string> supportedUnitTypesForFlare = new();
    [Export] public Godot.Collections.Array<string> supportedUnitTypesForExplode = new();
    [Export] public Godot.Collections.Array<string> supportedUnitTypesForPrimaryAmmo = new();
    [Export] public Godot.Collections.Array<string> supportedUnitTypesForFuel = new();

    // ========== P-1设施收入共享配置 ==========
    [ExportGroup("P-1公共设施配置")]
    [Export] public bool isPublicFacility = false;
    [Export] public int fundsPerTurn = 1000;
    [Export] public bool provideIncomeToAllTeams = false;

    public List<Infantry> capturingUnits = new();
    private CaptureContestEffect contestEffect;

    public override void _Ready()
    {
        ZIndex = 5;
        ParentGrid = GetParent() as Grids;
        if (ParentGrid == null)
        {
            GD.PushError("[Facility] Facility 节点必须是 Grids 的子节点！");
            return;
        }

        if (facilityTeam == TeamHelper.Player)
        {
            isPublicFacility = true;
            provideIncomeToAllTeams = true;
        }

        // ========== 初始化初始状态 ==========
        foreach (var data in initialStatuses)
        {
            if (data != null)
            {
                activeStatuses.Add(new FacilityStatus
                {
                    Type = data.Type,
                    RemainingTurns = data.RemainingTurns,
                    AppliedByTeam = data.AppliedByTeam
                });
            }
        }

        UpdateCityVisual();
        CreateStatusBarUI();
    }

    // ========== 状态栏系统核心方法 ==========

    public void AddStatus(FacilityStatusType type, int durationTurns = -1, string appliedByTeam = "")
    {
        var existing = activeStatuses.FirstOrDefault(s => s.Type == type);
        if (existing != null)
        {
            existing.RemainingTurns = durationTurns;
            existing.AppliedByTeam = appliedByTeam;
        }
        else
        {
            activeStatuses.Add(new FacilityStatus
            {
                Type = type,
                RemainingTurns = durationTurns,
                AppliedByTeam = appliedByTeam
            });
        }
        UpdateStatusBarUI();
    }

    public void RemoveStatus(FacilityStatusType type)
    {
        activeStatuses.RemoveAll(s => s.Type == type);
        UpdateStatusBarUI();
    }

    public bool HasStatus(FacilityStatusType type)
    {
        return activeStatuses.Any(s => s.Type == type && !s.IsExpired);
    }

    public int GetStatusRemainingTurns(FacilityStatusType type)
    {
        var status = activeStatuses.FirstOrDefault(s => s.Type == type && !s.IsExpired);
        return status?.RemainingTurns ?? 0;
    }

    private void TickStatuses()
    {
        foreach (var status in activeStatuses.ToList())
        {
            status.Tick();
            if (status.IsExpired)
            {
                activeStatuses.Remove(status);
                GD.Print($"[Facility] {ParentGrid?.Name} 的状态 '{status.GetDisplayName()}' 已过期");
            }
        }
    }

    private void CreateStatusBarUI()
    {
        if (ParentGrid == null) return;

        statusBarContainer = new VBoxContainer();
        statusBarContainer.Name = "StatusBar";
        statusBarContainer.ZIndex = 300;
        statusBarContainer.Position = new Vector2(-20, -40);
        statusBarContainer.Size = new Vector2(40, 0);
        statusBarContainer.AddThemeConstantOverride("separation", 2);

        AddChild(statusBarContainer);
        UpdateStatusBarUI();
    }

    private void UpdateStatusBarUI()
    {
        if (statusBarContainer == null) return;

        foreach (var child in statusBarContainer.GetChildren())
            child.QueueFree();
        statusLabels.Clear();

        if (activeStatuses.Count == 0)
        {
            statusBarContainer.Hide();
            return;
        }

        statusBarContainer.Show();

        foreach (var status in activeStatuses.Where(s => !s.IsExpired))
        {
            var label = new Label();
            label.Name = $"Status_{status.Type}";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", 10);

            Color statusColor = status.Type switch
            {
                FacilityStatusType.CannotProduce => new Color(1f, 0.3f, 0.3f),
                FacilityStatusType.NoIncome => new Color(0.8f, 0.8f, 0.2f),
                _ => Colors.White
            };

            label.AddThemeColorOverride("font_color", statusColor);

            string durationText = status.IsPermanent ? "" : $" ({status.RemainingTurns})";
            label.Text = $"{status.GetDisplayName()}{durationText}";

            var bg = new StyleBoxFlat();
            bg.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            bg.SetCornerRadiusAll(4);
            label.AddThemeStyleboxOverride("normal", bg);

            statusBarContainer.AddChild(label);
            statusLabels[status.Type] = label;
        }

        statusBarContainer.CustomMinimumSize = new Vector2(40, activeStatuses.Count * 16);
    }

    // ========== 生产权限判定（支持P-1轮流生产） ==========

    public bool CanBeProducedBy(string team)
    {
        if (!canProduce || string.IsNullOrEmpty(facilityTeam)) return false;
        if (facilityTeam == TeamHelper.Player0) return false;

        if (facilityTeam == TeamHelper.Player || isPublicFacility)
        {
            var cannotProduceStatus = activeStatuses.FirstOrDefault(s => 
                s.Type == FacilityStatusType.CannotProduce && 
                !s.IsExpired &&
                s.AppliedByTeam == team);

            if (cannotProduceStatus != null)
                return false;

            return productionsThisTurn < maxProductionsPerTurn;
        }

        return facilityTeam == team;
    }

    public void OnUnitProducedBy(string team)
    {
        productionsThisTurn++;

        if ((facilityTeam == TeamHelper.Player || isPublicFacility) && team != TeamHelper.Player0)
        {
            AddStatus(FacilityStatusType.CannotProduce, durationTurns: 1, appliedByTeam: team);
            GD.Print($"[Facility] P-1公共设施 {ParentGrid?.Name} 被 {team} 使用，{team} 下回合不可再生产");
        }
    }

    public bool CanProduceUnit(string unitName)
    {
        return canProduce && producibleUnitNames.Contains(unitName);
    }

    // ========== 占领系统 ==========

    public bool CanBeCapturedBy(Infantry unit)
    {
        if (unit == null || facilityTeam == unit.team) return false;
        return true;
    }

    public void OnCaptureStarted(Infantry unit)
    {
        capturingUnits.RemoveAll(u => u == null || !IsInstanceValid(u));
        if (HasContest()) ShowCaptureContestVisual();
    }

    public void OnCaptureInterrupted(Infantry unit)
    {
        capturingUnits.RemoveAll(u => u == null || !IsInstanceValid(u));
        capturingUnits.Remove(unit);
        if (capturingUnits.Count > 0)
        {
            if (capturingUnits.Select(u => u.team).Distinct().Count() > 1)
            {
                foreach (var u in capturingUnits) u.currentCaptureProgress = 0;
                ShowCaptureContestVisual();
            }
        }
        else ClearCaptureVisual();
        UpdateCaptureVisual();
    }

    public void OnCaptureCompleted(Infantry unit)
    {
        foreach (var u in capturingUnits.ToList()) { if (u != unit) { u.currentCaptureProgress = 0; u.capturingGrid = null; } }
        capturingUnits.Clear();

        string oldTeam = facilityTeam;
        facilityTeam = unit.team;

        if (facilityTeam == TeamHelper.Player)
        {
            isPublicFacility = true;
            provideIncomeToAllTeams = true;
        }

        activeStatuses.Clear();

        ClearCaptureVisual();
        ShowCaptureCompletedVisual(unit.team);
        UpdateCityVisual();
        UpdateStatusBarUI();
    }

    public bool HasContest()
    {
        if (ParentGrid == null) return false;
        var capturing = ParentGrid.infantries
            .Where(u => u.currentCaptureProgress > 0 && u.capturingGrid == ParentGrid)
            .ToList();
        capturing.RemoveAll(u => u == null || !IsInstanceValid(u));
        if (capturing.Count < 2) return false;
        return capturing.Select(u => u.team).Distinct().Count() > 1;
    }

    // ========== 视觉系统 ==========

    public virtual void UpdateCityVisual()
    {
        if (ParentGrid == null) return;
        var sprite = ParentGrid.GetNodeOrNull<Sprite2D>("Sprite2D");
        if (sprite == null) return;

        Color teamColor = facilityTeam switch
        {
            "Player1" => new Color(1f, 0.3f, 0.3f),
            "Player2" => new Color(0.3f, 0.5f, 1f),
            "Player0" => new Color(0.8f, 0.8f, 0.8f),
            "Player" => new Color(0.7f, 0.4f, 0.9f),
            _ => Colors.White
        };

        var tween = CreateTween();
        tween.TweenProperty(sprite, "modulate", teamColor, 0.5f);
    }

    public void ShowCaptureContestVisual()
    {
        if (contestEffect != null && IsInstanceValid(contestEffect)) contestEffect.QueueFree();
        contestEffect = new CaptureContestEffect { Position = new Godot.Vector2(0, -20), ZIndex = 200 };
        AddChild(contestEffect);
    }

    public void ShowCaptureCompletedVisual(string newTeam)
    {
        ClearCaptureVisual();
        var effect = new CaptureEffect
        {
            CaptureProgress = 100f,
            TeamColor = newTeam == "Player1" ? new Color(1f, 0.2f, 0.2f) : new Color(0.2f, 0.4f, 1f),
            IsCapturing = false, Position = new Godot.Vector2(0, -10), ZIndex = 200
        };
        AddChild(effect);
    }

    private void UpdateCaptureVisual() { }

    public void ClearContestVisual()
    {
        if (contestEffect != null && IsInstanceValid(contestEffect)) { contestEffect.QueueFree(); contestEffect = null; }
    }

    private void ClearCaptureVisual()
    {
        if (contestEffect != null && IsInstanceValid(contestEffect)) { contestEffect.QueueFree(); contestEffect = null; }
    }

    // ========== 补给系统 ==========

    public void PerformSupply()
    {
        if (ParentGrid == null) return;
        ParentGrid.infantries?.RemoveAll(i => !IsInstanceValid(i));
        bool anySupplied = false;

        foreach (var unit in ParentGrid.infantries)
        {
            if (unit == null || !IsInstanceValid(unit)) continue;
            bool canSupply = TeamHelper.CanFacilitySupply(facilityTeam, unit.team);
            if (!canSupply) continue;

            string unitType = unit.GetType().Name;
            bool supplied = false;
            string supplyText = "";
            Color supplyColor = Colors.Green;
            var supplyParts = new List<string>();

            int healValue = 0;
            if (healMode == 0)
                healValue = healAmount;
            else if (healMode == 1)
                healValue = Mathf.RoundToInt(unit.maxHealth * healPercent);

            if (healValue != 0)
            {
                if (supportedUnitTypesForHeal.Count == 0 || supportedUnitTypesForHeal.Contains(unitType))
                {
                    int oldHealth = unit.health;
                    unit.health += healValue;
                    if (!unit.explosionCanExceedMaxHealth)
                        unit.health = Mathf.Clamp(unit.health, 1, unit.maxHealth);
                    else
                        unit.health = Mathf.Max(1, unit.health);
                    int delta = unit.health - oldHealth;
                    if (delta != 0)
                    {
                        supplied = true;
                        supplyParts.Add(delta > 0 ? $"+{delta}HP" : $"{delta}HP");
                        supplyColor = new Color(0.2f, 0.9f, 0.3f);
                    }
                }
            }

            if (primaryAmmoSupply != 0)
            {
                if (unit.hasPrimaryWeapon && unit.primaryHasLimitedAmmo && unit.maxPrimaryAmmo < 99)
                {
                    if (supportedUnitTypesForPrimaryAmmo.Count == 0 || supportedUnitTypesForPrimaryAmmo.Contains(unitType))
                    {
                        int oldAmmo = unit.currentPrimaryAmmo;
                        if (primaryAmmoSupply >= 999)
                            unit.currentPrimaryAmmo = unit.maxPrimaryAmmo;
                        else
                            unit.currentPrimaryAmmo = Mathf.Clamp(unit.currentPrimaryAmmo + primaryAmmoSupply, 0, unit.maxPrimaryAmmo);
                        if (unit.currentPrimaryAmmo != oldAmmo)
                        {
                            supplied = true;
                            supplyParts.Add("Ammo+");
                            if (supplyColor == Colors.Green) supplyColor = new Color(0.9f, 0.3f, 0.2f);
                        }
                    }
                }
            }

            if (fuelSupply != 0)
            {
                if (unit.consumeFuel)
                {
                    if (supportedUnitTypesForFuel.Count == 0 || supportedUnitTypesForFuel.Contains(unitType))
                    {
                        int oldFuel = unit.fuel;
                        if (fuelSupply >= 999)
                            unit.fuel = unit.maxFuel;
                        else
                            unit.fuel = Mathf.Clamp(unit.fuel + fuelSupply, 0, unit.maxFuel);
                        if (unit.fuel != oldFuel)
                        {
                            supplied = true;
                            supplyParts.Add("Fuel+");
                            if (supplyColor == Colors.Green) supplyColor = new Color(0.9f, 0.7f, 0.2f);
                        }
                    }
                }
            }

            if (flareAmmoSupply != 0)
            {
                if (unit.canIlluminate)
                {
                    if (supportedUnitTypesForFlare.Count == 0 || supportedUnitTypesForFlare.Contains(unitType))
                    {
                        int oldFlare = unit.currentFlareAmmo;
                        if (flareAmmoSupply >= 999)
                            unit.currentFlareAmmo = unit.maxFlareAmmo;
                        else
                            unit.currentFlareAmmo = Mathf.Clamp(unit.currentFlareAmmo + flareAmmoSupply, 0, unit.maxFlareAmmo);
                        if (unit.currentFlareAmmo != oldFlare)
                        {
                            supplied = true;
                            supplyParts.Add("Flare+");
                            if (supplyColor == Colors.Green) supplyColor = new Color(0.9f, 0.9f, 0.2f);
                        }
                    }
                }
            }

            if (explodeAmmoSupply != 0)
            {
                if (unit.canExplode && !unit.explosionDestroysSelf)
                {
                    if (supportedUnitTypesForExplode.Count == 0 || supportedUnitTypesForExplode.Contains(unitType))
                    {
                        int oldExplode = unit.currentExplodeAmmo;
                        if (explodeAmmoSupply >= 999)
                            unit.currentExplodeAmmo = unit.maxExplodeAmmo;
                        else
                            unit.currentExplodeAmmo = Mathf.Clamp(unit.currentExplodeAmmo + explodeAmmoSupply, 0, unit.maxExplodeAmmo);
                        if (unit.currentExplodeAmmo != oldExplode)
                        {
                            supplied = true;
                            supplyParts.Add("Bomb+");
                            if (supplyColor == Colors.Green) supplyColor = new Color(0.9f, 0.5f, 0.1f);
                        }
                    }
                }
            }

            if (supplied)
            {
                anySupplied = true;
                supplyText = string.Join(" ", supplyParts);
                unit.UpdateHpLabel();
                ShowSupplyEffectNew(unit, supplyText, supplyColor);
            }
        }
        if (anySupplied) ShowGridSupplyEffectNew();
    }

    private void ShowSupplyEffectNew(Infantry unit, string text, Color color)
    {
        var effect = new SupplyEffect(); AddChild(effect);
        effect.Setup(unit.Position - GlobalPosition, text, color);
    }

    private void ShowGridSupplyEffectNew() { var effect = new GridSupplyEffect(); AddChild(effect); }

    // ========== 设施视野（P-1设施给所有势力共享） ==========

    public HashSet<Grids> CalculateFacilityVision(GameManager gm)
    {
        var visible = new HashSet<Grids>();
        if (gm == null || gm.gridManager?.map == null) return visible;

        if ((facilityTeam == TeamHelper.Player || isPublicFacility) && ParentGrid != null)
        {
            visible.Add(ParentGrid);
        }
        else if (TeamHelper.DoesFacilityProvideVision(facilityTeam) && ParentGrid != null)
        {
            visible.Add(ParentGrid);
        }
        return visible;
    }

    public int GetCurrentIncome()
    {
        if (HasStatus(FacilityStatusType.NoIncome))
            return 0;
        return fundsPerTurn;
    }

    public bool ProvidesIncomeTo(string team)
    {
        if (HasStatus(FacilityStatusType.NoIncome))
            return false;

        if (facilityTeam == TeamHelper.Player || isPublicFacility || provideIncomeToAllTeams)
            return true;

        return facilityTeam == team;
    }
}