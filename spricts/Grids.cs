using Godot;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

public enum GridType
{
	GROUND, FOREST, CITY, ROAD, SEA, RIVER, HILL, METEORITE, PIPE, LAVA, BEACH, TP,
	REEF, WHIRLPOOL, LAVASIDE, SEAFOG, LANDFOG, WATERFALL, CLIFF, SLOPE, CAVE, HOLE,
	PIPESEAM, TRACK, STATION, BRIDGE, LAVABRIDGE, PASSABLEPIPE, SHIPGATE, OVERPASS,
	BROKENPIPE, RUINS, BROKENTRACK, LAVAFOG,
}

public enum GridVisionMode { None, TowerCone, TowerRay }
public enum TowerDirection { Up, Right, Down, Left }

public partial class Grids : Node2D
{
    public Sprite2D attackRangeIcon;
    public Vector2I GridIndex;
    public Sprite2D pathIcon;
    [Export] public GridType gridType;
    public List<Infantry> infantries = new();
    public Infantry infantry;
    public Action<Grids> OnClickGrid;
    public Action OnClickEmpty;

    [Export] public bool isSupplyPoint = false;
    [Export] public int healAmount = 20;
    [Export] public string cityTeam = "";
    [Export] public int capturePointsRequired = 20;
    private CaptureContestEffect contestEffect;
    public bool IsInAttackRangeMode = false;
    public List<Weapon> weapons = new();
    public Weapon weapon;
    public bool IsInWeaponRange = false;

    [ExportGroup("格子自定义伤害")]
    [Export] public bool canDestroyUnit = true;
    [Export] public int fixedDamagePerTurn = 0;
    [Export] public int fixedAttackPerTurn = 0;
    [Export] public bool canOverMaxHealth = false;

    [ExportGroup("格子弹药/燃料系统")]
    [Export] public int fixedAmmoChangePerTurn = 0;
    [Export] public bool ammoCanOverMax = false;
    [Export] public bool ammoCanReachZero = true;
    [Export] public int fixedFuelChangePerTurn = 0;
    [Export] public bool fuelCanOverMax = false;
    [Export] public bool fuelCanReachZero = true;

    [ExportGroup("战争迷雾特殊规则")]
    [Export] public bool requiresAdjacentVision = false;
    [Export] public int visionBonus = 0;
    [Export] public bool isWatchtower = false;

    [ExportGroup("格子视野系统")]
    [Export] public GridVisionMode gridVisionMode = GridVisionMode.None;
    [Export] public int gridVisionRange = 1;
    [Export] public TowerDirection towerDirection = TowerDirection.Up;
    [Export] public bool towerCanRotate = false;
    [Export] public float towerRayAngle = 0f;
    [Export] public bool gridVisionIgnoreTerrain = true;

    public Action OnMouseEnteredAttackGrid;
    public Action OnMouseExitedAttackGrid;
    public List<Infantry> capturingUnits = new();

    public bool HasEnemyWeapon(string myTeam)
    {
        return weapons.Any(w => w.team != myTeam && IsInstanceValid(w));
    }

    public override void _Ready()
    {
        pathIcon = GetNode<Sprite2D>("mrhl");
        attackRangeIcon = GetNode<Sprite2D>("AttackRange");
        pathIcon?.Hide();
        attackRangeIcon?.Hide();

        var area2D = GetNodeOrNull<Area2D>("Area2D");
        if (area2D != null)
        {
            area2D.MouseEntered += () => OnMouseEnteredAttackGrid?.Invoke();
            area2D.MouseExited += () => OnMouseExitedAttackGrid?.Invoke();
            area2D.InputPickable = true;
        }

        // ✅ 初始化瞭望塔箭头朝向
        var arrow = GetNodeOrNull<Sprite2D>("TowerDirectionArrow");
        if (arrow != null)
        {
            arrow.Visible = gridVisionMode != GridVisionMode.None && towerCanRotate;
            arrow.RotationDegrees = towerDirection switch
            {
                TowerDirection.Up => 0,
                TowerDirection.Right => 90,
                TowerDirection.Down => 180,
                TowerDirection.Left => 270,
                _ => 0
            };
        }
    }

    public void OnArea2DMouseEntered() { if (IsInAttackRangeMode) OnMouseEnteredAttackGrid?.Invoke(); }
    public void OnArea2DMouseExited() { if (IsInAttackRangeMode) OnMouseExitedAttackGrid?.Invoke(); }

    public HashSet<Grids> CalculateGridVision(GameManager gm)
    {
        var visible = new HashSet<Grids>();
        if (gm == null || gm.gridManager?.map == null) return visible;

        if (!string.IsNullOrEmpty(cityTeam) && cityTeam != "Player0")
            visible.Add(this);

        switch (gridVisionMode)
        {
            case GridVisionMode.TowerCone: visible.UnionWith(CalculateTowerConeVision(gm)); break;
            case GridVisionMode.TowerRay: visible.UnionWith(CalculateTowerRayVision(gm)); break;
        }

        if (visionBonus > 0 && !string.IsNullOrEmpty(cityTeam) && cityTeam != "Player0")
            visible.UnionWith(CalculateManhattanVision(gm, GridIndex, visionBonus));

        return visible;
    }

    private HashSet<Grids> CalculateTowerConeVision(GameManager gm)
    {
        var visible = new HashSet<Grids>();
        var dir = towerDirection switch
        {
            TowerDirection.Up => new Vector2I(0, -1), TowerDirection.Right => new Vector2I(1, 0),
            TowerDirection.Down => new Vector2I(0, 1), TowerDirection.Left => new Vector2I(-1, 0),
            _ => new Vector2I(0, -1)
        };
        var perp = new Vector2I(dir.Y, -dir.X);

        for (int depth = 1; depth <= gridVisionRange; depth++)
        {
            Vector2I center = GridIndex + dir * depth;
            int halfWidth = depth;
            for (int w = -halfWidth; w <= halfWidth; w++)
            {
                var pos = center + perp * w;
                if (IsValidGrid(pos, gm))
                {
                    var g = gm.gridManager.map[pos.X, pos.Y];
                    if (g != null)
                    {
                        visible.Add(g);
                        if (!gridVisionIgnoreTerrain && IsVisionBlockingTerrain(g.gridType)) break;
                    }
                }
            }
        }
        return visible;
    }

    private HashSet<Grids> CalculateTowerRayVision(GameManager gm)
    {
        var visible = new HashSet<Grids>();
        if (gm?.gridManager == null) return visible;

        float angleRad = Mathf.DegToRad(towerRayAngle);
        Godot.Vector2 dir = new(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
        float cellSize = gm.gridManager.gridSize.X;
        Godot.Vector2 startPos = Position + new Godot.Vector2(cellSize * 0.5f, cellSize * 0.5f);

        Godot.Vector2 currentPos = startPos;
        int currentX = GridIndex.X, currentY = GridIndex.Y;
        int stepX = dir.X > 0 ? 1 : (dir.X < 0 ? -1 : 0);
        int stepY = dir.Y > 0 ? 1 : (dir.Y < 0 ? -1 : 0);
        int steps = 0;
        int maxSteps = gridVisionRange * 2;
        HashSet<Grids> visited = new();

        for (int step = 0; step < maxSteps && steps < gridVisionRange; step++)
        {
            float x0 = gm.gridManager.startPos.X + currentX * cellSize;
            float x1 = x0 + cellSize;
            float y0 = gm.gridManager.startPos.Y + currentY * cellSize;
            float y1 = y0 + cellSize;

            float tX = float.MaxValue, tY = float.MaxValue;
            if (stepX > 0) tX = (x1 - currentPos.X) / dir.X;
            else if (stepX < 0) tX = (x0 - currentPos.X) / dir.X;
            if (stepY > 0) tY = (y1 - currentPos.Y) / dir.Y;
            else if (stepY < 0) tY = (y0 - currentPos.Y) / dir.Y;

            float tStep = Mathf.Min(tX, tY);
            if (tStep < 0.0001f) tStep = 0.0001f;

            var targetGrid = gm.gridManager.map[currentX, currentY];
            if (targetGrid != null && !visited.Contains(targetGrid) && targetGrid != this)
            {
                visited.Add(targetGrid);
                visible.Add(targetGrid);
                steps++;
                if (!gridVisionIgnoreTerrain && IsVisionBlockingTerrain(targetGrid.gridType)) break;
            }

            currentPos += dir * tStep;
            bool xExit = Mathf.Abs(tX - tStep) < 0.0001f;
            bool yExit = Mathf.Abs(tY - tStep) < 0.0001f;
            if (xExit && yExit) { currentX += stepX; currentY += stepY; }
            else if (xExit) currentX += stepX;
            else if (yExit) currentY += stepY;

            if (!IsValidGrid(new Vector2I(currentX, currentY), gm)) break;
        }
        return visible;
    }

    private HashSet<Grids> CalculateManhattanVision(GameManager gm, Vector2I center, int range)
    {
        var visible = new HashSet<Grids>();
        for (int dx = -range; dx <= range; dx++)
            for (int dy = -range; dy <= range; dy++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > range) continue;
                var pos = new Vector2I(center.X + dx, center.Y + dy);
                if (IsValidGrid(pos, gm))
                {
                    var g = gm.gridManager.map[pos.X, pos.Y];
                    if (g != null) visible.Add(g);
                }
            }
        return visible;
    }

    private bool IsValidGrid(Vector2I pos, GameManager gm)
    {
        return pos.X >= 0 && pos.X < gm.gridManager.searchRange.X &&
               pos.Y >= 0 && pos.Y < gm.gridManager.searchRange.Y;
    }

    private bool IsVisionBlockingTerrain(GridType type)
    {
        return type == GridType.METEORITE || type == GridType.PIPE || type == GridType.CLIFF;
    }

    public void ApplyTerrainDamage(Infantry unit)
    {
        if (unit == null || !IsInstanceValid(unit) || unit.health <= 0) return;
        int damage = 0; bool isPercentage = false; float percentage = 0f; string damageSource = "";
        switch (gridType)
        {
            case GridType.WHIRLPOOL:
                if (unit.moveType == MoveType.Naval || unit.moveType == MoveType.Hover)
                { damage = 80; damageSource = "WHIRLPOOL"; }
                break;
            case GridType.LAVA:
                bool isExemptLava = unit.moveType == MoveType.AirPlane || unit.moveType == MoveType.AirShip ||
                    unit.moveType == MoveType.AeroSpacer || unit.moveType == MoveType.SpaceShiper ||
                    unit.moveType == MoveType.LAVARUNNER || unit.moveType == MoveType.LAVAHOVER;
                if (unit.moveType == MoveType.Drone || unit.moveType == MoveType.HeliCopter) isExemptLava = false;
                if (!isExemptLava) { isPercentage = true; percentage = 0.10f; damageSource = "LAVA"; }
                break;
            case GridType.LAVASIDE:
                bool isExemptLavaSide = unit.moveType == MoveType.LAVAHOVER || unit.moveType == MoveType.AirPlane ||
                    unit.moveType == MoveType.AirShip || unit.moveType == MoveType.AeroSpacer ||
                    unit.moveType == MoveType.HeliCopter || unit.moveType == MoveType.SpaceShiper ||
                    unit.moveType == MoveType.LAVARUNNER;
                if (!isExemptLavaSide) { damage = 98; damageSource = "LAVASIDE"; }
                break;
            case GridType.LAVAFOG:
                bool isExemptLavaFOG = unit.moveType == MoveType.AeroSpacer || unit.moveType == MoveType.SpaceShiper;
                if (unit.moveType == MoveType.Drone || unit.moveType == MoveType.HeliCopter) isExemptLavaFOG = false;
                if (!isExemptLavaFOG) { isPercentage = true; percentage = 0.20f; damageSource = "LAVAFOG"; }
                break;
        }
        if (isPercentage)
        {
            int actualDamage = Mathf.Max(1, Mathf.RoundToInt(unit.maxHealth * percentage));
            unit.health -= actualDamage; unit.UpdateHpLabel();
            ShowTerrainDamageEffect(unit, $"-{actualDamage} ({damageSource})", Colors.Red);
        }
        else if (damage > 0)
        {
            int finalDamage = Mathf.Max(1, CalculateDamageThroughDefense(unit, damage));
            unit.health -= finalDamage; unit.UpdateHpLabel();
            ShowTerrainDamageEffect(unit, $"-{finalDamage} ({damageSource})", Colors.Red);
        }
        CheckUnitDeath(unit, damageSource);
    }

    public void ApplyGridCustomDamage(Infantry unit)
    {
        if (unit == null || !IsInstanceValid(unit) || fixedDamagePerTurn == 0) return;
        if (fixedDamagePerTurn > 0)
        {
            unit.health -= fixedDamagePerTurn; unit.UpdateHpLabel();
            ShowTerrainDamageEffect(unit, $"-{fixedDamagePerTurn} (地形)", new Color(1, 0.3f, 0.3f));
        }
        else
        {
            int healAmount = Mathf.Abs(fixedDamagePerTurn);
            if (canOverMaxHealth) unit.health += healAmount;
            else unit.health = Mathf.Min(unit.maxHealth, unit.health + healAmount);
            unit.UpdateHpLabel();
            ShowTerrainDamageEffect(unit, $"+{healAmount} (地形)", new Color(0.3f, 1, 0.3f));
        }
        CheckUnitDeath(unit, "GridCustomDamage");
    }

    public void ApplyGridCustomAttack(Infantry unit)
    {
        if (unit == null || !IsInstanceValid(unit) || fixedAttackPerTurn == 0) return;
        if (fixedAttackPerTurn > 0)
        {
            int finalDamage = Mathf.Max(1, CalculateDamageThroughDefense(unit, fixedAttackPerTurn));
            unit.health -= finalDamage; unit.UpdateHpLabel();
            ShowTerrainDamageEffect(unit, $"-{finalDamage} (地形攻击)", new Color(1, 0.5f, 0.2f));
        }
        else
        {
            int healAmount = Mathf.Abs(fixedAttackPerTurn);
            if (canOverMaxHealth) unit.health += healAmount;
            else unit.health = Mathf.Min(unit.maxHealth, unit.health + healAmount);
            unit.UpdateHpLabel();
            ShowTerrainDamageEffect(unit, $"+{healAmount} (地形攻击)", new Color(0.3f, 1, 0.5f));
        }
        CheckUnitDeath(unit, "GridCustomAttack");
    }

    public void ApplyGridAmmoChange(Infantry unit)
    {
        if (unit == null || !IsInstanceValid(unit) || fixedAmmoChangePerTurn == 0) return;
        if (!unit.hasPrimaryWeapon || !unit.primaryHasLimitedAmmo || unit.maxPrimaryAmmo >= 99) return;
        if (fixedAmmoChangePerTurn > 0)
        {
            int oldAmmo = unit.currentPrimaryAmmo;
            if (ammoCanOverMax) unit.currentPrimaryAmmo += fixedAmmoChangePerTurn;
            else unit.currentPrimaryAmmo = Mathf.Min(unit.maxPrimaryAmmo, unit.currentPrimaryAmmo + fixedAmmoChangePerTurn);
            if (unit.currentPrimaryAmmo - oldAmmo > 0)
                ShowTerrainDamageEffect(unit, $"Ammo+{unit.currentPrimaryAmmo - oldAmmo} (地形)", new Color(0.9f, 0.7f, 0.2f));
        }
        else
        {
            int consumeAmount = Mathf.Abs(fixedAmmoChangePerTurn);
            unit.currentPrimaryAmmo -= consumeAmount;
            if (!ammoCanReachZero) unit.currentPrimaryAmmo = Mathf.Max(1, unit.currentPrimaryAmmo);
            else unit.currentPrimaryAmmo = Mathf.Max(0, unit.currentPrimaryAmmo);
            ShowTerrainDamageEffect(unit, $"Ammo-{consumeAmount} (地形)", new Color(1, 0.5f, 0.2f));
        }
    }

    public void ApplyGridFuelChange(Infantry unit)
    {
        if (unit == null || !IsInstanceValid(unit) || fixedFuelChangePerTurn == 0 || !unit.consumeFuel) return;
        if (fixedFuelChangePerTurn > 0)
        {
            int oldFuel = unit.fuel;
            if (fuelCanOverMax) unit.fuel += fixedFuelChangePerTurn;
            else unit.fuel = Mathf.Min(unit.maxFuel, unit.fuel + fixedFuelChangePerTurn);
            if (unit.fuel - oldFuel > 0)
                ShowTerrainDamageEffect(unit, $"Fuel+{unit.fuel - oldFuel} (地形)", new Color(0.2f, 0.8f, 0.9f));
        }
        else
        {
            int consumeAmount = Mathf.Abs(fixedFuelChangePerTurn);
            unit.fuel -= consumeAmount;
            if (!fuelCanReachZero) unit.fuel = Mathf.Max(1, unit.fuel);
            else unit.fuel = Mathf.Max(0, unit.fuel);
            ShowTerrainDamageEffect(unit, $"Fuel-{consumeAmount} (地形)", new Color(0.3f, 0.5f, 1f));
        }
    }

    public void LockHealthIfNeeded(Infantry unit)
    {
        if (unit == null || !IsInstanceValid(unit) || canDestroyUnit || unit.health > 0) return;
        unit.health = 1; unit.UpdateHpLabel();
    }

    private int CalculateDamageThroughDefense(Infantry unit, int baseDamage)
    {
        return Mathf.Max(0, baseDamage - unit.GetEffectiveDefense(this));
    }

    private void CheckUnitDeath(Infantry unit, string source)
    {
        if (unit.health <= 0 && canDestroyUnit)
        {
            var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
            gm?.RemovePiece(unit);
            unit.OnDestroyed();
            if (unit.grid != null)
            {
                unit.grid.infantries.Remove(unit);
                if (unit.grid.infantry == unit) unit.grid.infantry = unit.grid.infantries.Count > 0 ? unit.grid.infantries[0] : null;
            }
            unit.QueueFree();
        }
    }

    private void ShowTerrainDamageEffect(Infantry unit, string text, Color color)
    {
        var label = new Label();
        label.Text = text; label.Modulate = color;
        label.Position = unit.Position + new Godot.Vector2(0, -20);
        label.ZIndex = 300;
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeConstantOverride("outline_size", 2);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        GetTree()?.CurrentScene?.AddChild(label);
        var tween = CreateTween();
        tween.TweenProperty(label, "position", label.Position + new Godot.Vector2(0, -40), 1.0f);
        tween.TweenProperty(label, "modulate:a", 0.0f, 0.5f);
        tween.TweenCallback(Callable.From(() => label.QueueFree()));
    }

    public void ShowCaptureContestVisual()
    {
        if (contestEffect != null && IsInstanceValid(contestEffect)) contestEffect.QueueFree();
        contestEffect = new CaptureContestEffect { Position = new Godot.Vector2(0, -20), ZIndex = 200 };
        AddChild(contestEffect);
    }

    public void OnCaptureInterrupted(Infantry unit)
    {
        capturingUnits.RemoveAll(u => u == null || !IsInstanceValid(u));
        capturingUnits.Remove(unit);
        if (capturingUnits.Count > 0)
        {
            if (capturingUnits.Select(u => u.team).Distinct().Count() > 1)
            {
                GD.Print($"[Grids] 争夺状态！不同阵营在同时占领 {GridIndex}");
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
        cityTeam = unit.team;
        ClearCaptureVisual();
        ShowCaptureCompletedVisual(unit.team);
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
    public void OnCaptureStarted(Infantry unit)
    {
        capturingUnits.RemoveAll(u => u == null || !IsInstanceValid(u));
        if (HasContest()) ShowCaptureContestVisual();
    }

    public bool HasContest()
    {
        if (gridType != GridType.CITY) return false;
        var capturing = infantries.Where(u => u.currentCaptureProgress > 0 && u.capturingGrid == this).ToList();
        capturing.RemoveAll(u => u == null || !IsInstanceValid(u));
        if (capturing.Count < 2) return false;
        return capturing.Select(u => u.team).Distinct().Count() > 1;
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

    public void PerformSupply()
    {
        if (!isSupplyPoint) return;
        infantries?.RemoveAll(i => !IsInstanceValid(i));
        bool anySupplied = false;
        foreach (var unit in infantries)
        {
            if (unit == null || !IsInstanceValid(unit)) continue;
            bool canSupply = false;
            if (string.IsNullOrEmpty(cityTeam)) continue;
            else if (cityTeam == "Player0") canSupply = (unit.team == "Player1" || unit.team == "Player2");
            else canSupply = (unit.team == cityTeam);
            if (!canSupply) continue;

            bool supplied = false; string supplyText = ""; Color supplyColor = Colors.Green;
            int oldHealth = unit.health;
            int healAmt = Mathf.Max(1, Mathf.RoundToInt(unit.maxHealth * 0.2f));
            unit.health = Mathf.Min(unit.maxHealth, unit.health + healAmt);
            int actualHeal = unit.health - oldHealth;
            if (actualHeal > 0) { supplied = true; supplyText = $"+{actualHeal}HP"; supplyColor = new Color(0.2f, 0.9f, 0.3f); }

            if (unit.consumeFuel && unit.fuel < unit.maxFuel)
            { unit.fuel = unit.maxFuel; if (!supplied) { supplyText = "Fuel+"; supplyColor = new Color(0.9f, 0.7f, 0.2f); } else supplyText += " Fuel+"; supplied = true; }
            if (unit.hasPrimaryWeapon && !unit.CanUsePrimaryWeapon())
            { unit.ReloadAmmo(); if (!supplied) { supplyText = "Ammo+"; supplyColor = new Color(0.9f, 0.3f, 0.2f); } else supplyText += " Ammo+"; supplied = true; }
            if (unit.maxPrimaryAmmo > 0 && unit.currentPrimaryAmmo < unit.maxPrimaryAmmo)
            { unit.ReloadAmmo(); if (!supplied) { supplyText = "Ammo+"; supplyColor = new Color(0.9f, 0.3f, 0.2f); } else supplyText += " Ammo+"; supplied = true; }

            if (supplied) { anySupplied = true; unit.UpdateHpLabel(); ShowSupplyEffectNew(unit, supplyText, supplyColor); }
        }
        if (anySupplied) ShowGridSupplyEffectNew();
    }

    private void ShowSupplyEffectNew(Infantry unit, string text, Color color)
    {
        var effect = new SupplyEffect(); AddChild(effect);
        effect.Setup(unit.Position - Position, text, color);
    }
    private void ShowGridSupplyEffectNew() { var effect = new GridSupplyEffect(); AddChild(effect); }

    public float TerrainDefenseBonus
    {
        get
        {
            return gridType switch
            {
                GridType.GROUND => 0.10f, GridType.FOREST => 0.20f, GridType.CITY => 0.30f,
                GridType.HILL => 0.40f, GridType.ROAD => 0.0f, GridType.SEA => 0.0f,
                GridType.RIVER => 0.0f, GridType.BEACH => 0.10f, GridType.TP => 0.0f,
                GridType.REEF => 0.10f, GridType.WHIRLPOOL => 0.10f, GridType.LAVASIDE => 0.10f,
                GridType.SEAFOG => 0.50f, GridType.LANDFOG => 0.50f, GridType.WATERFALL => 0.20f,
                GridType.CLIFF => 0.30f, GridType.SLOPE => 0.20f, GridType.CAVE => 0.40f,
                GridType.HOLE => 0.40f, GridType.PIPESEAM => 0.10f, GridType.METEORITE => 0.10f,
                GridType.TRACK => 0.0f, GridType.STATION => 0.20f, GridType.BRIDGE => 0.0f,
                GridType.LAVABRIDGE => 0.0f, GridType.PASSABLEPIPE => 0.10f, GridType.SHIPGATE => 0.10f,
                GridType.OVERPASS => 0.30f, GridType.BROKENPIPE => 0.20f, GridType.RUINS => 0.30f,
                GridType.BROKENTRACK => 0.20f, GridType.LAVAFOG => 0.50f, _ => 0.0f
            };
        }
    }

    public string GetTerrainDefenseDescription() { float bonus = TerrainDefenseBonus * 100; return bonus > 0 ? $"{bonus:F0}%" : "无"; }

    public bool CanBeCapturedBy(Infantry unit) { if (gridType != GridType.CITY || unit == null || cityTeam == unit.team) return false; return true; }

    public List<Infantry> GetEnemyInfantries(string team) { return infantries.Where(i => i != null && i.team != team).ToList(); }

    private double lastClickTime = 0;
    private const double DOUBLE_CLICK_WINDOW = 0.3;
    public bool HasEnemyInfantry(string team) { return infantries.Any(i => i != null && IsInstanceValid(i) && i.team != team); }
    public static bool IsForceActionMode = false;

    public void ClickGrid(Node viewport, InputEvent inputs, int shape_index)
    {
        if (inputs is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                var terrainEditor = GetTree().GetFirstNodeInGroup("terrain_editor") as TerrainEditor;
                if (terrainEditor != null && terrainEditor.IsEditMode)
                { terrainEditor.OnGridClickedForEdit(this); GetViewport().SetInputAsHandled(); return; }

                var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
                if (IsForceActionMode && OnClickGrid != null) { OnClickGrid.Invoke(this); GetViewport().SetInputAsHandled(); return; }

                if (gm?.selectedInfantry != null && gm.selectedInfantry.state != UnitState.Acted &&
                    gm.gridManager != null && gm.gridManager.moveRange.Contains(this) && gm.selectedInfantry.grid != this)
                { if (OnClickGrid != null) { OnClickGrid.Invoke(this); GetViewport().SetInputAsHandled(); return; } }

                if (weapons.Count > 0)
                {
                    var w = weapons[0];
                    if (gm != null && gm.IsTurnPhaseValid(w.team) && !w.hasActed) { w.OnClickWeapon?.Invoke(w); return; }
                }

                if (infantries == null) infantries = new List<Infantry>();
                infantries.RemoveAll(i => !IsInstanceValid(i));
                if (infantries.Count > 0)
                {
                    if (gm != null)
                    {
                        string currentTeam = gm.turnPhase == 1 ? "Player1" : "Player2";
                        var selectableUnits = infantries.Where(i => i.team == currentTeam && !i.isMoved).OrderByDescending(i => i.health).ToList();
                        if (selectableUnits.Count > 0)
                        {
                            var selectedUnit = selectableUnits[0];
                            if (selectedUnit.grid == null)
                            {
                                if (gm.unitManager != null) gm.unitManager.BindUnitToGrid(selectedUnit, true);
                                else { var um = GetNodeOrNull<UnitManager>("/root/Main/UnitManager"); um?.BindUnitToGrid(selectedUnit, true); }
                            }
                            selectedUnit.InputClick(viewport, inputs, shape_index);
                            return;
                        }
                    }
                }

                // ✅ 检查是否为可旋转的己方瞭望塔
                if (gridVisionMode != GridVisionMode.None && towerCanRotate && !string.IsNullOrEmpty(cityTeam))
                {
                    string currentTeam = gm?.turnPhase == 1 ? "Player1" : "Player2";
                    if (cityTeam == currentTeam && gm?.selectedInfantry == null)
                    {
                        var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
                        actionMenu?.ShowGridRotateMenu(this);
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }

                double now = Time.GetTicksMsec() / 1000.0;
                if (now - lastClickTime < DOUBLE_CLICK_WINDOW) { ShowTerrainInfo(); lastClickTime = 0; GetViewport().SetInputAsHandled(); return; }
                lastClickTime = now;
                if (OnClickGrid != null) OnClickGrid.Invoke(this); else OnClickEmpty?.Invoke();
            }
        }
    }

    private void ShowTerrainInfo()
    {
        var actionMenu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
        if (actionMenu == null) return;
        string info = $"地形: {gridType}\n防御加成: {GetTerrainDefenseDescription()}\n坐标: ({GridIndex.X}, {GridIndex.Y})\n";
        if (gridType == GridType.CITY) { info += $"归属: {cityTeam}\n占领所需: {capturePointsRequired}点\n"; }
        if (gridVisionMode != GridVisionMode.None)
        {
            info += $"\n=== 格子视野 ===\n模式: {gridVisionMode}\n射程: {gridVisionRange}\n";
            if (gridVisionMode == GridVisionMode.TowerCone) { info += $"方向: {towerDirection}\n可旋转: {(towerCanRotate ? "是" : "否")}\n"; }
            else if (gridVisionMode == GridVisionMode.TowerRay) { info += $"角度: {towerRayAngle}°\n"; }
            info += $"无视地形: {(gridVisionIgnoreTerrain ? "是" : "否")}\n";
        }
        if (visionBonus != 0) info += $"视野加成: {visionBonus}\n";
        if (requiresAdjacentVision) info += $"需要紧邻视野: 是\n";
        if (fixedDamagePerTurn != 0) info += $"回合伤害: {fixedDamagePerTurn}\n";
        if (fixedAttackPerTurn != 0) info += $"回合攻击: {fixedAttackPerTurn}\n";
        if (fixedAmmoChangePerTurn != 0) info += $"弹药变化: {fixedAmmoChangePerTurn}\n";
        if (fixedFuelChangePerTurn != 0) info += $"燃料变化: {fixedFuelChangePerTurn}\n";
        actionMenu.unitInfoLabel.Text = info;
        actionMenu.unitInfoLabel.Visible = true;
        actionMenu.closeInfoButton.Visible = true;
        var timer = GetTree().CreateTimer(3.0);
        timer.Timeout += () => { if (actionMenu != null && IsInstanceValid(actionMenu)) { actionMenu.unitInfoLabel.Visible = false; actionMenu.closeInfoButton.Visible = false; } };
    }

    public List<Infantry> GetOtherInfantries(Infantry exclude) { return infantries.Where(i => i != exclude && IsInstanceValid(i)).ToList(); }
    public List<Infantry> GetEnemyInfantries(Infantry reference) { return infantries.Where(i => i != reference && IsInstanceValid(i) && i.team != reference.team).ToList(); }
    public bool HasEnemyInfantry(Infantry reference) { return infantries.Any(i => i != reference && IsInstanceValid(i) && i.team != reference.team); }
}
