using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Facility : Node2D
{
    public Grids ParentGrid { get; private set; }

    [Export] public string facilityTeam = "";
    [Export] public int capturePointsRequired = 20;
    [Export] public bool isSupplyPoint = true;
    [Export] public int healAmount = 20;

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

        UpdateCityVisual();
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
                GD.Print($"[Facility] 争夺状态！不同阵营在同时占领 {ParentGrid?.GridIndex}");
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
        facilityTeam = unit.team;
        ClearCaptureVisual();
        ShowCaptureCompletedVisual(unit.team);
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

        GD.Print($"[UpdateCityVisual] 城市 {ParentGrid.GridIndex} 变为 {facilityTeam} 颜色");
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
        if (!isSupplyPoint || ParentGrid == null) return;
        ParentGrid.infantries?.RemoveAll(i => !IsInstanceValid(i));
        bool anySupplied = false;
        foreach (var unit in ParentGrid.infantries)
        {
            if (unit == null || !IsInstanceValid(unit)) continue;
            bool canSupply = TeamHelper.CanFacilitySupply(facilityTeam, unit.team);
            if (!canSupply) continue;
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
        effect.Setup(unit.Position - GlobalPosition, text, color);
    }

    private void ShowGridSupplyEffectNew() { var effect = new GridSupplyEffect(); AddChild(effect); }

    // ========== 设施视野（所属势力提供自身格子的视野）==========

    public HashSet<Grids> CalculateFacilityVision(GameManager gm)
    {
        var visible = new HashSet<Grids>();
        if (gm == null || gm.gridManager?.map == null) return visible;
        if (TeamHelper.DoesFacilityProvideVision(facilityTeam) && ParentGrid != null)
            visible.Add(ParentGrid);
        return visible;
    }
}
