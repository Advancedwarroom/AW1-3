using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

// Infantry · Transport（由 Infantry.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class Infantry
{

[ExportGroup("搭载系统")]
[Export] public bool canTransportUnits = false;
[Export] public bool canSupplyUnits = false;  // 能否补给其他单位（独立于搭载系统）

[Export] public int maxTransportCapacity = 0;
[Export] public Godot.Collections.Array<string> canTransportUnitTypes = new();
[Export] public int maxLoadCapacity { get => maxTransportCapacity; set => maxTransportCapacity = value; }
[Export] public int minSupplyRange = 1;
[Export] public int maxSupplyRange = 1;
[Export] public bool canSupplyAfterMove = true;

// ✅ 新增：按单位类别过滤（在Inspector中勾选可搭载的类别）
[Export] public Godot.Collections.Array<UnitCategory> canTransportUnitCategories = new();

// ✅ 新增：按属性过滤（勾选即可生效，零代码适配新单位）
[Export] public bool transportExcludeAirUnits = false;      // 排除空军单位
[Export] public bool transportExcludeArmored = false;      // 排除装甲单位
[Export] public bool transportRequireCanCapture = false;   // 只能运输可占领单位
[Export] public int transportMaxMovePoints = 0;            // 最大移动力限制（0=无限制）

[Export] public AnimatedSprite2D loadedIcon;
[Export] public AnimatedSprite2D noFuelIcon;

// 状态
public bool isUnloading = false;

// 私有
private Tween loadedIconTween;


// ========== 新增：核心伤害公式 ==========




public List<Infantry> transportedUnits = new List<Infantry>();
	[Export]
	public bool isTransported = false;

    [Export]
    public AttackOverlapPolicy attackOverlap = AttackOverlapPolicy.AllowAttackEnemyOnSameTile;
    [Export]
    public UnitOverlapType overlapType = UnitOverlapType.NonOverlapping;   // ✅ 覆盖方式：NonOverlapping=不可重叠 / Overlapping=仅重叠 / Oozium=覆盖+吞噬(自动，无需canDevour)
    [Export]
    public MoveType moveType = MoveType.Infantry;  
    [Export]
    public UnitState state = UnitState.Idle;
    [Export]
    public GridManager gridManager;

    [Export]
    public AttackType attackType = AttackType.CanAttack;

    [Export]
    public int attackRange = 1;
    [Export] public int defaultMovePoints = 3;  // 默认移动力，Inspector可改
    [Export] public int movePoints = 3;  // 当前剩余移动力
    [Export]
    public int maxHealth = 100;
    [Export]
    public int health = 100;
    [Export]
    public int baseAttack = 100;
    [Export]
    public int baseDefense = 0;
    [Export] public int minAttackRange = 1;        // 最小攻击范围（盲区）
    [Export] public int maxAttackRange = 1;        // 最大攻击范围
    [Export] public bool useMinMaxAttackRange = false;  // 是否使用环形攻击范围
    [Export] public bool canAttackAfterMoving = true;   // 移动后能否攻击

    [Export] public AnimatedSprite2D noAmmoIcon;
    // ✅ 新增：通用弹药系统（所有单位可用）




    [Export] public int maxFuel = 99;
    [Export] public int fuel = 99;
    [Export] public bool consumeFuel = false;
    [Export] public int lowFuelThreshold = 15;               // 低燃料阈值，低于此值闪烁 NoFuelIcon
    [Export] public int dailyFuelConsumption = 0;          // 每回合开始自动消耗的燃料
    [Export] public bool destroyOnOutOfFuel = false;       // 燃料耗尽后是否自毁

[ExportGroup("远程搭载")]
[Export] public bool CanFarMounting = false;
[Export] public int minFarMountingDistance = 0;    // 远程搭载最小距离（0=无限制，可贴身上船）
[Export] public int maxFarMountingDistance = 1;    // 远程搭载最大距离（原 FarMountingDistance） 

    // ========== ✅ 究极自由：搭载系统方法 ==========

    /// <summary>
    /// 检查是否可以搭载指定单位
    /// </summary>
    /// <summary>
    /// 检查是否可以搭载指定单位（模块化：通过Inspector配置，零代码适配新单位）
    /// </summary>
    public virtual bool CanTransportUnit(Infantry unit)
    {
        if (!canTransportUnits) return false;
        if (transportedUnits.Count >= maxTransportCapacity) return false;
        if (unit == this) return false;
        if (unit.team != this.team) return false;

        // 1. 按类型名过滤（精确匹配类名，如 "Infantry", "Mech"）
        string unitType = unit.UnitTypeId;
        if (canTransportUnitTypes != null && canTransportUnitTypes.Count > 0)
        {
            if (!canTransportUnitTypes.Contains(unitType)) return false;
        }

        // 2. 按单位类别过滤（如 Infantry, Tank, Mech 等）
        if (canTransportUnitCategories != null && canTransportUnitCategories.Count > 0)
        {
            var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
            if (gm != null && gm.unitCategories.TryGetValue(unit, out var category))
            {
                if (!canTransportUnitCategories.Contains(category)) return false;
            }
        }

        // 3. 按属性标志过滤（零代码扩展新单位）
        if (transportExcludeAirUnits && unit.IsAirUnit) return false;
        if (transportExcludeArmored && unit.isArmoredUnit) return false;
        if (transportRequireCanCapture && unit.captureAbility != CaptureAbility.CanCapture) return false;
        if (transportMaxMovePoints > 0 && unit.defaultMovePoints > transportMaxMovePoints) return false;

        return true;
    }

    /// <summary>
    /// 搭载单位
    /// </summary>
    public virtual bool TransportUnit(Infantry unit)
    {
        if (!CanTransportUnit(unit)) return false;

        transportedUnits.Add(unit);

        unit.Hide();
        unit.ProcessMode = ProcessModeEnum.Disabled;

        if (unit.grid != null)
        {
            unit.grid.infantries.Remove(unit);
            if (unit.grid.infantry == unit)
                unit.grid.infantry = unit.grid.infantries.Count > 0 ? unit.grid.infantries[0] : null;
        }

        return true;
    }

    /// <summary>
    /// 卸下单位到指定格子
    /// </summary>
    public virtual bool UntransportUnit(Infantry unit, Grids targetGrid)
    {
        if (!transportedUnits.Contains(unit)) return false;
        if (targetGrid == null) return false;
        if (targetGrid.gridType == GridType.METEORITE) return false;

        // 检查目标格子是否有阻挡
        var blockingUnit = targetGrid.infantries.FirstOrDefault(u => 
            u != null && IsInstanceValid(u) && 
            u.overlapType == UnitOverlapType.NonOverlapping);
        if (blockingUnit != null) return false;

        transportedUnits.Remove(unit);

        unit.Show();
        unit.ProcessMode = ProcessModeEnum.Inherit;

        unit.Position = targetGrid.Position;
        unit.grid = targetGrid;

        if (!targetGrid.infantries.Contains(unit))
            targetGrid.infantries.Add(unit);
        if (targetGrid.infantry == null)
            targetGrid.infantry = unit;

        // 卸下后本回合不能移动
        unit.isMoved = true;
        unit.isAttacked = true;
        unit.state = UnitState.Acted;
        unit.SetWaitVisual(true);

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.unitManager?.BindUnitToGrid(unit, true);

        return true;
    }

// ========== ✅ 究极自由：通用装载方法（从 APC 迁移） ==========
public virtual bool LoadUnit(Infantry unit)
{
        if (isStealthed && !canLoadUnloadWhileStealthed) return false; // ✅ 隐身行为锁
    if (!CanTransportUnit(unit)) return false;
    
    // ✅ 彻底停止所有视觉组件的动画残留（sprite + animSprite + 嵌套子节点）
    StopAllVisuals(unit);
    
    transportedUnits.Add(unit);
    unit.isTransported = true;
    
    unit.Hide();
    unit.ProcessMode = ProcessModeEnum.Disabled;
    
    if (unit.grid != null)
    {
        unit.grid.infantries.Remove(unit);
        if (unit.grid.infantry == unit)
            unit.grid.infantry = unit.grid.infantries.Count > 0 ? unit.grid.infantries[0] : null;
        unit.grid = null; // ✅ 彻底断开与格子的关联
    }
    
    UpdateLoadedIcon();
    
    // ✅ 装载后刷新迷雾，确保被装载单位正确隐藏
    var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
    gm?.fogOfWarManager?.OnUnitMoved();
    
    return true;
}

    // ✅ 彻底停止并重置单位的所有视觉组件（防止装载后残留动画）
    private void StopAllVisuals(Infantry unit)
    {
        unit.breathTween?.Kill();
        unit.breathTween = null;
        
        void ResetNode(Node node)
        {
            if (node is Sprite2D sp && IsInstanceValid(sp))
            {
                sp.Scale = Vector2.One;
                sp.Modulate = Colors.White;
                sp.Hide();
            }
            else if (node is AnimatedSprite2D anim && IsInstanceValid(anim))
            {
                anim.Scale = Vector2.One;
                anim.Modulate = Colors.White;
                anim.Stop();
                anim.Hide();
            }
            else if (node is Label lbl && IsInstanceValid(lbl))
            {
                lbl.Hide();
            }
            foreach (var child in node.GetChildren())
                ResetNode(child);
        }
        
        ResetNode(unit);
        unit.Hide();
    }
    
    // ✅ 恢复单位的所有视觉组件（卸载后调用）
    private void RestoreAllVisuals(Infantry unit)
    {
        void RestoreNode(Node node)
        {
            if (node is Sprite2D sp && IsInstanceValid(sp))
            {
                sp.Modulate = Colors.White;
            }
            else if (node is AnimatedSprite2D anim && IsInstanceValid(anim))
            {
                anim.Modulate = Colors.White;
            }
            foreach (var child in node.GetChildren())
                RestoreNode(child);
        }
        
        RestoreNode(unit);
        unit.SetWaitVisual(false);
        unit.StartBreath();
    }

// ========== ✅ 究极自由：通用卸载方法（从 APC 迁移） ==========
public virtual bool UnloadUnit(Infantry unit, Grids targetGrid)
{
        if (isStealthed && !canLoadUnloadWhileStealthed) return false; // ✅ 隐身行为锁
    if (!transportedUnits.Contains(unit)) return false;
    if (targetGrid == null) return false;
    if (targetGrid.gridType == GridType.METEORITE) return false;

    var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;

    // ✅ P-1 公共运输单位：只能卸载当前势力的单位
    if (this.team == TeamHelper.Player)
    {
        string currentTurnTeam = TeamHelper.GetPhaseTeamName(gm?.turnPhase ?? 1);
        if (unit.team != currentTurnTeam)
            return false;
    }

    
    var blockingUnit = targetGrid.infantries.FirstOrDefault(u => 
        u != null && IsInstanceValid(u) && 
        u.overlapType == UnitOverlapType.NonOverlapping);
    if (blockingUnit != null) return false;
    
    transportedUnits.Remove(unit);
    unit.isTransported = false;
    
    unit.Show();
    unit.ProcessMode = ProcessModeEnum.Inherit;
    
    // ✅ 恢复被装载单位的所有视觉组件
    RestoreAllVisuals(unit);
    
    unit.Position = targetGrid.Position;
    unit.grid = targetGrid;
    
    if (!targetGrid.infantries.Contains(unit))
        targetGrid.infantries.Add(unit);
    if (targetGrid.infantry == null)
        targetGrid.infantry = unit;
    
    // 卸下后本回合不能移动
    unit.isMoved = true;
    unit.isAttacked = true;
    unit.state = UnitState.Acted;
    unit.SetWaitVisual(true);
    
    unitsCannotMove[unit] = 1;
    
    gm?.unitManager?.BindUnitToGrid(unit, true);
    gm?.fogOfWarManager?.OnUnitMoved(); // ✅ 卸载后刷新迷雾
    
    UpdateLoadedIcon();
    
    return true;
}

// ========== ✅ 究极自由：检查是否可以卸载到格子（从 APC 迁移） ==========
public virtual bool CanUnloadToGrid(Grids targetGrid)
{
    if (transportedUnits.Count == 0) return false;
    if (targetGrid == null) return false;
    if (targetGrid.gridType == GridType.METEORITE) return false;
    
    var blockingUnit = targetGrid.infantries.FirstOrDefault(u => 
        u != null && IsInstanceValid(u) && 
        u.overlapType == UnitOverlapType.NonOverlapping);
    
    return blockingUnit == null;
}

// ========== ✅ 究极自由：获取第一个已装载单位（从 APC 迁移） ==========
public virtual Infantry GetFirstLoadedUnit()
{
    return transportedUnits.FirstOrDefault(u => u != null && IsInstanceValid(u));
}

// ========== ✅ 究极自由：loadedIcon 视觉系统（从 APC 迁移） ==========
private void SetupLoadedIcon()
{
    if (loadedIcon == null)
    {
        return;
    }
    
    var spriteFrames = GD.Load<SpriteFrames>("res://asscets/AnimatedSprite/loaded.tres");
    if (spriteFrames == null)
    {
        return;
    }
    
    loadedIcon.SpriteFrames = spriteFrames;
    
    string[] animNames = loadedIcon.SpriteFrames.GetAnimationNames();
    int count = animNames.Length;
    
    
    if (count > 0)
    {
        string targetAnim = "";
        
        for (int i = 0; i < count; i++)
        {
            string animName = animNames[i];
            if (animName == "loaded")
            {
                targetAnim = animName;
                break;
            }
        }
        
        if (string.IsNullOrEmpty(targetAnim))
        {
            targetAnim = animNames[0];
        }
        
        loadedIcon.Animation = targetAnim;
        loadedIcon.Play();
        
    }
    
    loadedIcon.Hide();
    loadedIcon.ZIndex = 200;
    loadedIcon.Modulate = Colors.White;
}

private void StartLoadedIconBlink()
{
    if (loadedIcon == null) return;
    
    StopLoadedIconBlink();
    
    loadedIcon.Show();
    loadedIcon.Visible = true;
    
    if (!loadedIcon.IsPlaying())
    {
        loadedIcon.Play();
    }
    
    var timer = new Timer();
    timer.WaitTime = 1.0f;
    timer.OneShot = false;
    timer.Autostart = true;
    
    timer.Timeout += OnBlinkTimerTimeout;
    
    AddChild(timer);
    
    SetMeta("blink_timer", timer);
}

private void OnBlinkTimerTimeout()
{
    if (loadedIcon != null && IsInstanceValid(loadedIcon))
    {
        loadedIcon.Visible = !loadedIcon.Visible;
    }
}

private void StopLoadedIconBlink()
{
    if (loadedIconTween != null && loadedIconTween.IsValid())
    {
        loadedIconTween.Kill();
        loadedIconTween = null;
    }
    
    if (HasMeta("blink_timer"))
    {
        var timer = GetMeta("blink_timer").As<Timer>();
        if (timer != null && IsInstanceValid(timer))
        {
            timer.Stop();
            timer.QueueFree();
        }
        RemoveMeta("blink_timer");
    }
    
    if (loadedIcon != null)
    {
        loadedIcon.Hide();
        loadedIcon.Modulate = new Color(1, 1, 1, 1);
        loadedIcon.Visible = false;
    }
}

public void UpdateLoadedIcon()
{
    
    if (transportedUnits.Count > 0)
    {
        StartLoadedIconBlink();
    }
    else
    {
        StopLoadedIconBlink();
    }
}

    /// <summary>
    /// 获取可卸下的相邻格子
    /// </summary>
    public virtual List<Grids> GetAdjacentTransportGrids()
    {
        var result = new List<Grids>();
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.gridManager == null || grid == null) return result;

        Vector2I[] offsets = {
            new Vector2I(0, 1), new Vector2I(-1, 0),
            new Vector2I(0, -1), new Vector2I(1, 0)
        };

        foreach (var offset in offsets)
        {
            var pos = grid.GridIndex + offset;
            if (gm.unitManager.IsValidGrid(pos))
            {
                var targetGrid = gm.gridManager.map[pos.X, pos.Y];
                if (targetGrid != null && targetGrid.gridType != GridType.METEORITE)
                {
                    var blockingUnit = targetGrid.infantries.FirstOrDefault(u =>
                        u != null && IsInstanceValid(u) &&
                        u.overlapType == UnitOverlapType.NonOverlapping);
                    if (blockingUnit == null)
                        result.Add(targetGrid);
                }
            }
        }
        return result;
    }
}
