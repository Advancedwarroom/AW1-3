// BlackCannon.cs - 黑炮：三角形范围，固定百分比伤害，支持自定义目标选择和改良伤害模式
// ✅ 新增：完整弹药系统 + 多回合冷却系统（还原原版高级战争原型炮/完全体差异）
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class BlackCannon : Weapon
{
    [ExportGroup("黑炮专用配置")]
    [Export] public int maxAttackDepth = 9;                    // 射程深度（直线距离）
    [Export] public float fixedDamagePercent = 0.3f;           // 固定扣血30%（传统模式）
    [Export] public CannonDirection direction = CannonDirection.Up;
    [Export] public bool CanDestroy = false;

    // ✅ 新增：能否攻击兵器
    // ✅ 究极自由：使用基类的 CanAttackWeapon 属性

    // ✅ 新增：目标选择模式
    public enum TargetSelectionMode
    {
        AllSelect,      // 可选择所有单位（敌方+己方）
        OnlyEnemyUnits, // 只能选择敌方单位（默认）
        OnlyUserUnits   // 只能选择己方单位
    }
    [Export] public TargetSelectionMode targetSelectionMode = TargetSelectionMode.OnlyEnemyUnits;

    // ✅ 新增：改良伤害模式（支持回血）
    [ExportGroup("改良伤害模式（覆盖传统扣血）")]
    [Export] public bool useModifiedDamage = false;            // 是否使用改良伤害模式
    [Export] public float modifiedHealthPercent = 0.3f;        // 正=扣血，负=回血（如-0.5=回50%血）
    [Export] public float modifiedAmmoPercent = 0f;            // 正=扣弹药，负=回弹药（百分比）
    [Export] public float modifiedFuelPercent = 0f;            // 正=扣油，负=回油（百分比）
    [Export] public bool canOverMaxHp = false;                 // ✅ 新增：是否允许血量超过MaxHp（默认false）

    // ========== ✅ 新增：弹药系统（已在Weapon基类中定义，此处使用override）==========
    // useAmmoSystem, currentAmmo, maxAmmo 继承自 Weapon 基类

    // ========== ✅ 新增：冷却系统 ==========
    [ExportGroup("冷却系统（多回合CD，默认关闭）")]
    [Export] public bool useCooldownSystem = false;              // 🔧 开关：默认false=无冷却（每回合重置）
    [Export] public int cooldownTurns = 2;                     // 冷却周期（回合数）。如2=每2回合才能攻击
    [Export] public int attacksPerCooldown = 1;                // 每个冷却周期内的攻击次数配额
    [Export] public bool storeAttacks = true;                  // 存次数：本回合未用完的次数是否保留到后续回合

    // BlackCannon.cs - 在类顶部添加
private Vector2 swipeStartPos = Vector2.Zero;
private bool isSwiping = false;
private const float SWIPE_THRESHOLD = 50f; // 滑动超过50px算有效

public override void ShowAttackRange()
{
    var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
    if (gm?.gridManager == null) return;

    if (!HasAmmoAndCooldown())
    {
        ShowAttackRangeAsDisabled(gm);
        return;
    }

    gm.gridManager.ClearWeaponRange();
    Grids.IsForceActionMode = true;

    var rangeGrids = CalculateAttackRange();
    var fog = gm.fogOfWarManager;
    bool isFogEnabled = fog != null && fog.isFogOfWarEnabled;

    foreach (var g in rangeGrids)
    {
        if (isFogEnabled && fog != null && !fog.IsGridVisible(g)) continue;
        bool hasValidTarget = HasValidTargetInGrid(g);

        if (hasValidTarget)
        {
            g.attackRangeIcon?.Show();
            if (g.attackRangeIcon != null)
                g.attackRangeIcon.Modulate = new Color(1, 0.2f, 0.2f, 0.9f);

            // ✅ 修复：lambda 中检查实例有效性
            g.OnClickGrid = (to) => {
                if (!IsInstanceValid(this)) {
                    gm.gridManager.ClearWeaponRange();
                    Grids.IsForceActionMode = false;
                    return;
                }
                OnRangeGridClicked(g);
            };
            gm.gridManager.OverrideUnitInput(g, true);
            
            g.IsInWeaponRange = true;
        }
        else
        {
            g.pathIcon?.Show();
            if (g.pathIcon != null)
                g.pathIcon.Modulate = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            
            g.OnClickGrid = (to) => {
                if (!IsInstanceValid(this)) {
                    gm.gridManager.ClearWeaponRange();
                    Grids.IsForceActionMode = false;
                    foreach (var grid in rangeGrids)
                        gm.gridManager.OverrideUnitInput(grid, false);
                    return;
                }
                gm.gridManager.ClearWeaponRange();
                Grids.IsForceActionMode = false;
                foreach (var grid in rangeGrids)
                    gm.gridManager.OverrideUnitInput(grid, false);
                if (!hasActed)
                {
                    var menu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
                    menu?.ShowWeaponMenu(this);
                }
            };
            gm.gridManager.OverrideUnitInput(g, true);
            
            g.IsInWeaponRange = true;
        }
    }

    // 范围外点击
    foreach (var g in gm.gridManager.grids)
    {
        if (!rangeGrids.Contains(g))
        {
            g.OnClickEmpty = () => {
                if (!IsInstanceValid(this)) {
                    gm.gridManager.ClearWeaponRange();
                    Grids.IsForceActionMode = false;
                    foreach (var grid in rangeGrids)
                        gm.gridManager.OverrideUnitInput(grid, false);
                    return;
                }
                gm.gridManager.ClearWeaponRange();
                Grids.IsForceActionMode = false;
                foreach (var grid in rangeGrids)
                    gm.gridManager.OverrideUnitInput(grid, false);
                
                if (!hasActed)
                {
                    var menu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
                    menu?.ShowWeaponMenu(this);
                }
            };
        }
    }
}

private void ShowAttackRangeAsDisabled(GameManager gm)
{
    gm.gridManager.ClearWeaponRange();
    var rangeGrids = CalculateAttackRange();
    var fog = gm.fogOfWarManager;
    bool isFogEnabled = fog != null && fog.isFogOfWarEnabled;

    foreach (var g in rangeGrids)
    {
        if (isFogEnabled && fog != null && !fog.IsGridVisible(g)) continue;
        g.pathIcon?.Show();
        if (g.pathIcon != null)
            g.pathIcon.Modulate = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        g.IsInWeaponRange = true;
    }

    foreach (var g in gm.gridManager.grids)
    {
        if (!rangeGrids.Contains(g))
        {
            g.OnClickEmpty = () => {
                if (!IsInstanceValid(this)) {
                    gm.gridManager.ClearWeaponRange();
                    return;
                }
                gm.gridManager.ClearWeaponRange();
                var menu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
                menu?.ShowWeaponMenu(this);
            };
        }
    }
}

public override void _Input(InputEvent @event)
{
    var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
    
    // 只有本炮被选中、且当前回合是己方、且还没行动才能转
    if (gm?.selectedWeapon != this || !gm.IsTurnPhaseValid(team) || hasActed)
        return;

    // R键（保留）
    if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.R)
    {
        RotateDirection();
        GetViewport().SetInputAsHandled();
        return;
    }

    // ✅ 滑动手势
    if (@event is InputEventScreenTouch touch)
    {
        if (touch.Pressed)
        {
            swipeStartPos = touch.Position;
            isSwiping = true;
        }
        else if (isSwiping)
        {
            Vector2 delta = touch.Position - swipeStartPos;
            if (Mathf.Abs(delta.X) > SWIPE_THRESHOLD)
            {
                if (delta.X > 0)
                {
                    // 向右滑：顺时针转
                    RotateDirection();
                    GD.Print($"[BlackCannon] 右滑转向 → {direction}");
                }
                else
                {
                    // 向左滑：逆时针转
                    direction = (CannonDirection)(((int)direction + 3) % 4); // +3 = -1 (mod 4)
                    UpdateDirectionVisual();
                    GD.Print($"[BlackCannon] 左滑转向 → {direction}");
                }
                GetViewport().SetInputAsHandled();
            }
            isSwiping = false;
        }
    }
    
    // 鼠标拖拽（PC端模拟滑动）
    if (@event is InputEventMouseButton mouseBtn)
    {
        if (mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            swipeStartPos = mouseBtn.Position;
            isSwiping = true;
        }
        else if (!mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left && isSwiping)
        {
            Vector2 delta = mouseBtn.Position - swipeStartPos;
            if (Mathf.Abs(delta.X) > SWIPE_THRESHOLD)
            {
                if (delta.X > 0)
                    RotateDirection();
                else
                {
                    direction = (CannonDirection)(((int)direction + 3) % 4);
                    UpdateDirectionVisual();
                }
                GetViewport().SetInputAsHandled();
            }
            isSwiping = false;
        }
    }
}

    // ========== ✅ 新增：运行时冷却状态 ==========
    public int turnsSinceLastAttack = 0;                       // 距离上次攻击开始的回合数（0=本周期刚开始）
    public int attacksRemainingInCycle = 0;                    // 当前周期内剩余攻击次数
    public bool cooldownReady = true;                          // 冷却是否就绪（周期是否可用）
    public int totalTurnsPassed = 0;                           // 总回合计数（用于周期判断）

    public enum CannonDirection { Up, Right, Down, Left }
    public override bool CanAttack() => !hasActed && remainingAttacks > 0 && HasAmmoAndCooldown();

    // 方向向量（上、右、下、左）
    private static readonly Vector2I[] DirectionVectors = {
        new Vector2I(0, -1),   // Up
        new Vector2I(1, 0),    // Right
        new Vector2I(0, 1),    // Down
        new Vector2I(-1, 0)    // Left
    };

    // 垂直向量（用于计算横向扩散）
    private static readonly Vector2I[] PerpVectors = {
        new Vector2I(1, 0),    // Up的垂直方向是Right
        new Vector2I(0, 1),    // Right的垂直方向是Down
        new Vector2I(-1, 0),   // Down的垂直方向是Left
        new Vector2I(0, -1)    // Left的垂直方向是Up
    };



    private Label rotateTip;

    // 当 GameManager 选中/取消选中本炮时会触发
    public override void SetVisualNormal()
    {
        base.SetVisualNormal();
        UpdateRotateTip();      // 被选中
    }

    public override void SetVisualDark()
    {
        base.SetVisualDark();
        UpdateRotateTip();      // 被取消
    }

    private void UpdateRotateTip()
    {
        if (rotateTip == null) return;
        bool show = !hasActed && IsSelected();   // 可转且选中
        rotateTip.Visible = show;
    }

    private bool IsSelected()
    {
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        return gm?.selectedWeapon == this;
    }
public override void _Ready()
{
    base._Ready();
    InitializeCooldownState();
    
    var love = GetNode<Sprite2D>("LoveIcon");
    if (love != null) love.Show();
    UpdateDirectionVisual();
    rotateTip = GetNodeOrNull<Label>("RotateTip");
    UpdateRotateTip();
    UpdateAmmoVisual();
}



// ✅ 新增：初始化冷却状态（开局默认冷却完毕）
private void InitializeCooldownState()
{
    // 基础状态（由 base.OnTurnStart() 提供）
    hasActed = false;
    remainingAttacks = maxAttacksPerTurn;
    
    if (useCooldownSystem)
    {
        // ✅ 开局默认冷却完毕，可以立即攻击
        turnsSinceLastAttack = 0;
        cooldownReady = true;
        attacksRemainingInCycle = attacksPerCooldown;
        totalTurnsPassed = 0;
    }
    else
    {
        // 无冷却系统，使用默认攻击次数
        turnsSinceLastAttack = 0;
        cooldownReady = true;
        attacksRemainingInCycle = maxAttacksPerTurn;
    }
    
    UpdateAmmoVisual();
}

    // ========== ✅ 新增：弹药与冷却综合检查 ==========
    public bool HasAmmoAndCooldown()
    {
        // 1. 检查弹药
        if (useAmmoSystem && currentAmmo <= 0)
        {
            GD.Print($"[BlackCannon] {Name} 弹药耗尽！无法攻击");
            return false;
        }

        // 2. 检查冷却
        if (useCooldownSystem)
        {
            if (!cooldownReady)
            {
                GD.Print($"[BlackCannon] {Name} 冷却中... 还需 {cooldownTurns - turnsSinceLastAttack} 回合");
                return false;
            }

            if (attacksRemainingInCycle <= 0)
            {
                GD.Print($"[BlackCannon] {Name} 本周期攻击次数已用完 ({attacksPerCooldown}次)");
                return false;
            }
        }

        return true;
    }

    // ========== ✅ 新增：消耗弹药 ==========
    public bool ConsumeAmmo()
    {
        if (!useAmmoSystem) return true; // 无限弹药，直接成功

        if (currentAmmo <= 0)
        {
            GD.Print($"[BlackCannon] {Name} 弹药不足！当前:{currentAmmo}");
            return false;
        }

        currentAmmo--;
        GD.Print($"[BlackCannon] {Name} 消耗1发弹药，剩余:{currentAmmo}");
        UpdateAmmoVisual();
        return true;
    }

    // ========== ✅ 新增：补给弹药（APC调用） ==========
    public override bool ResupplyAmmo()
    {
        if (!useAmmoSystem)
        {
            GD.Print($"[BlackCannon] {Name} 使用无限弹药，无需补给");
            return false;
        }

        // 检查是否需要补给（现有弹药 < 最大弹药）
        if (currentAmmo >= maxAmmo)
        {
            GD.Print($"[BlackCannon] {Name} 弹药已满 ({currentAmmo}/{maxAmmo})，无需补给");
            return false;
        }

        // 补给到maxAmmo（即使只差1发也补满，但不超过）
        int oldAmmo = currentAmmo;
        currentAmmo = maxAmmo;

        GD.Print($"[BlackCannon] {Name} 补给完成：{oldAmmo} → {currentAmmo} (上限{maxAmmo})");
        UpdateAmmoVisual();

        // 播放补给特效
        ShowAmmoResupplyEffect();

        return true;
    }

    

    // ========== ✅ 新增：弹药视觉更新 ==========
    public void UpdateAmmoVisual()
    {
        // 如果有弹药标签则更新（可选UI组件）
        var ammoLabel = GetNodeOrNull<Label>("AmmoLabel");
        if (ammoLabel != null)
        {
            if (useAmmoSystem)
            {
                ammoLabel.Text = $"{currentAmmo}/{maxAmmo}";
                ammoLabel.Modulate = currentAmmo <= 0 ? Colors.Red : 
                                    (currentAmmo <= maxAmmo / 3 ? Colors.Yellow : Colors.White);
                ammoLabel.Show();
            }
            else
            {
                ammoLabel.Text = "∞";
                ammoLabel.Modulate = Colors.Green;
                ammoLabel.Show();
            }
        }

        // 低弹药闪烁图标（类似Infantry的noAmmoIcon）
        var noAmmoIcon = GetNodeOrNull<AnimatedSprite2D>("NoAmmoIcon");
        if (noAmmoIcon != null)
        {
            if (!useAmmoSystem)
            {
                noAmmoIcon.Visible = false;
            }
            else if (currentAmmo <= 0)
            {
                noAmmoIcon.Visible = true;
                if (noAmmoIcon.SpriteFrames != null && noAmmoIcon.SpriteFrames.HasAnimation("noammo"))
                    noAmmoIcon.Play("noammo");
            }
            else if (currentAmmo <= maxAmmo / 3)
            {
                // 低弹药闪烁
                noAmmoIcon.Visible = (Time.GetTicksMsec() / 500) % 2 == 0;
                if (noAmmoIcon.SpriteFrames != null && noAmmoIcon.SpriteFrames.HasAnimation("lowammo"))
                    noAmmoIcon.Play("lowammo");
            }
            else
            {
                noAmmoIcon.Visible = false;
            }
        }
    }

    // ========== ✅ 新增：补给特效 ==========
    private void ShowAmmoResupplyEffect()
    {
        var effect = new SupplyEffect();
        effect.Setup(Vector2.Zero, "Ammo+", new Color(0.9f, 0.7f, 0.2f));
        AddChild(effect);
    }

    public override void RotateDirection()
    {
        // 顺时针旋转90度
        direction = (CannonDirection)(((int)direction + 1) % 4);
        UpdateDirectionVisual();
        GD.Print($"[BlackCannon] {Name} 转向 → {direction}");
    }

    public void UpdateDirectionVisual()
    {
        if (animSprite == null) return;
        string anim = GetAnimName();
        if (animSprite.SpriteFrames.HasAnimation(anim))
            animSprite.Play(anim);
        else
            GD.PrintErr($"[BlackCannon] 找不到动画：{anim}");
    }

    // 计算三角形攻击范围内的所有格子
    public override List<Grids>  CalculateAttackRange()
    {
        var range = new List<Grids>();
        if (grid == null) return range;

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.gridManager?.map == null) return range;

        Vector2I dir = DirectionVectors[(int)direction];
        Vector2I perp = PerpVectors[(int)direction];
        Vector2I start = grid.GridIndex;

        // 遍历每一层深度（距离）
        for (int depth = 1; depth <= maxAttackDepth; depth++)
        {
            // 当前层的中心点（沿主方向前进depth格）
            Vector2I center = start + dir * depth;

            // 计算当前层宽度：第1层3格，第2层5格...第n层 (1+2*n) 格，最大19格对应depth=9
            int halfWidth = depth;

            // 遍历当前层的横向所有格子
            for (int w = -halfWidth; w <= halfWidth; w++)
            {
                Vector2I checkPos = center + perp * w;

                // 边界检查
                if (checkPos.X < 0 || checkPos.X >= gm.gridManager.searchRange.X ||
                    checkPos.Y < 0 || checkPos.Y >= gm.gridManager.searchRange.Y)
                    continue;

                var targetGrid = gm.gridManager.map[checkPos.X, checkPos.Y];
                if (targetGrid != null && targetGrid.gridType != GridType.METEORITE)
                {
                    range.Add(targetGrid);
                }
            }
        }

        return range;
    }

public override void TakeDamage(int damage)
{
    health -= damage;
    health = Mathf.Max(0, health);
    UpdateHpLabel();

    // 1. 受伤闪烁
    if (animSprite != null)
    {
        var tween = CreateTween();
        tween.TweenProperty(animSprite, "modulate", Colors.Red, 0.1f);
        tween.TweenProperty(animSprite, "modulate", Colors.White, 0.1f);
    }

    // 2. 标记攻击者已行动
    var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
    if (gm?.selectedInfantry != null)
    {
        var attacker = gm.selectedInfantry;
        attacker.isAttacked = true;
        attacker.state   = UnitState.Acted;
        attacker.originalGrid = null;
        attacker.SetWaitVisual(true);
        gm.gridManager.HideAttackRange();
        gm.ClearSelectedInfantry();
    }

    // 3. 血量归零 → 延迟自毁
    if (health <= 0)
    {
        // ✅ 修复：保存 grid 引用，不要在这里清空
        var destroyedGrid = grid;
        
        // 从格子移除引用（但不要清空自己的 grid 字段）
        if (grid != null)
        {
            grid.weapons.Remove(this);
            if (grid.weapon == this)
                grid.weapon = null;
            // ❌ 不要：grid = null;
        }

        if (gm != null && gm.selectedWeapon == this)
            gm.selectedWeapon = null;

        // ✅ 修复：直接在这里处理地形切换，不依赖 OnDestroyed
        if (destroyedGrid != null && (destroyedGrid.gridType == GridType.PIPESEAM || destroyedGrid.gridType == GridType.METEORITE))
        {
            string terrainName = destroyedGrid.gridType == GridType.PIPESEAM ? "PIPESEAM" : "METEORITE";
            GD.Print($"[BlackCannon] {Name} 被消灭，所在 {terrainName} 将切换为 BROKENPIPE");

            // 切换地形
            destroyedGrid.gridType = GridType.BROKENPIPE;
            destroyedGrid.isSupplyPoint = false;

            // 更新格子视觉
            var sprite = destroyedGrid.GetNodeOrNull<Sprite2D>("Sprite2D");
            if (sprite != null)
            {
                var tween = CreateTween();
                tween.TweenProperty(sprite, "modulate", new Color(0.3f, 0.3f, 0.3f), 0.5f)
                     .SetTrans(Tween.TransitionType.Sine)
                     .SetEase(Tween.EaseType.InOut);
            }

            GD.Print($"[BlackCannon] {destroyedGrid.GridIndex} 已从 {terrainName} 切换为 BROKENPIPE");
        }

        // 现在可以安全清空 grid
        grid = null;

        CallDeferred(nameof(OnDestroyed));
    }
}




    public bool HasValidTargetInGrid(Grids targetGrid)
    {
        switch (targetSelectionMode)
        {
            case TargetSelectionMode.AllSelect:
                // ✅ 检查单位
                if (targetGrid.infantries.Count > 0) return true;
                // ✅ 检查兵器（根据CanAttackWeapon设置）
                if (CanAttackWeapon && targetGrid.weapon != null) return true;
                return false;

            case TargetSelectionMode.OnlyEnemyUnits:
                // ✅ 检查敌方单位
                if (targetGrid.HasEnemyInfantry(team)) return true;
                // ✅ 检查敌方兵器（根据CanAttackWeapon设置）
                if (CanAttackWeapon && targetGrid.weapon != null && targetGrid.weapon.team != team) return true;
                return false;

            case TargetSelectionMode.OnlyUserUnits:
                // ✅ 检查己方单位
                if (targetGrid.infantries.Any(i => i.team == team)) return true;
                // ✅ 检查己方兵器（根据CanAttackWeapon设置）
                if (CanAttackWeapon && targetGrid.weapon != null && targetGrid.weapon.team == team) return true;
                return false;

            default:
                return false;
        }
    }

    private string GetAnimName()
    {
        string teamPrefix = team == "Player2" ? "cannon2" : "cannon1";
        string dirSuffix = direction switch
        {
            CannonDirection.Up    => "up",
            CannonDirection.Down  => "down",
            CannonDirection.Left  => "left",
            CannonDirection.Right => "right",
            _ => "up"
        };
        return $"{teamPrefix}_{dirSuffix}";
    }

    private void OnRangeGridClicked(Grids targetGrid)
    {
        // ✅ 关键修复：检查格子是否还有效
        if (targetGrid == null || !IsInstanceValid(targetGrid)) return;

        // ✅ 再次检查弹药和冷却
        if (!HasAmmoAndCooldown())
        {
            GD.Print($"[BlackCannon] {Name} 攻击失败：弹药或冷却不足");
            return;
        }

        var targets = GetValidTargetsInGrid(targetGrid);
        if (targets.Count == 0) return;

        var target = targets[0];

        // ✅ 关键修复：再次检查目标有效性
        if (target == null || !IsInstanceValid(target)) return;

        // ✅ 消耗弹药（如果开启弹药系统）
        if (!ConsumeAmmo()) return;

        // ✅ 消耗冷却次数
        if (useCooldownSystem)
        {
            attacksRemainingInCycle--;
            GD.Print($"[BlackCannon] {Name} 周期剩余次数:{attacksRemainingInCycle}/{attacksPerCooldown}");
        }

        if (target is Infantry infantry)
        {
            PerformAttack(infantry);
        }
        else if (target is Weapon weapon)
        {
            // ✅ 关键修复：攻击前检查兵器是否已被销毁
            if (IsInstanceValid(weapon))
            {
                PerformAttackOnWeapon(weapon);
            }
        }
    }

    private List<Node2D> GetValidTargetsInGrid(Grids targetGrid)
    {
        var targets = new List<Node2D>();

        switch (targetSelectionMode)
        {
            case TargetSelectionMode.AllSelect:
                // ✅ 添加所有单位
                targets.AddRange(targetGrid.infantries.Where(i => IsInstanceValid(i)).Cast<Node2D>());
                // ✅ 添加兵器（根据CanAttackWeapon设置）
                if (CanAttackWeapon && targetGrid.weapon != null && IsInstanceValid(targetGrid.weapon))
                    targets.Add(targetGrid.weapon);
                break;

            case TargetSelectionMode.OnlyEnemyUnits:
                // ✅ 添加敌方单位
                targets.AddRange(targetGrid.infantries
                    .Where(i => IsInstanceValid(i) && i.team != team)
                    .Cast<Node2D>());
                // ✅ 添加敌方兵器（根据CanAttackWeapon设置）
                if (CanAttackWeapon && targetGrid.weapon != null && IsInstanceValid(targetGrid.weapon) && targetGrid.weapon.team != team)
                    targets.Add(targetGrid.weapon);
                break;

            case TargetSelectionMode.OnlyUserUnits:
                // ✅ 添加己方单位
                targets.AddRange(targetGrid.infantries
                    .Where(i => IsInstanceValid(i) && i.team == team)
                    .Cast<Node2D>());
                // ✅ 添加己方兵器（根据CanAttackWeapon设置）
                if (CanAttackWeapon && targetGrid.weapon != null && IsInstanceValid(targetGrid.weapon) && targetGrid.weapon.team == team)
                    targets.Add(targetGrid.weapon);
                break;
        }

        return targets;
    }

    // 黑炮攻击兵器 - 完全重写，避免任何延迟调用
    private void PerformAttackOnWeapon(Weapon targetWeapon)
    {
        if (remainingAttacks <= 0) return;

        // 严格有效性检查
        if (targetWeapon == null || !IsInstanceValid(targetWeapon)) 
        {
            GD.Print("[PerformAttackOnWeapon] 目标无效，取消攻击");
            return;
        }

        // 保存关键数据（在修改前）
        Vector2 targetPos = targetWeapon.GlobalPosition;
        string targetName = targetWeapon.Name;
        int targetHealth = targetWeapon.health;

        GD.Print($"[PerformAttackOnWeapon] {Name} 攻击 {targetName}，当前血量:{targetHealth}");

        // 计算伤害
        int damage;
        bool willDestroy = false;

        if (useModifiedDamage)
        {
            damage = Mathf.RoundToInt(targetWeapon.maxHealth * Mathf.Abs(modifiedHealthPercent));
            damage = Mathf.Max(1, damage);

            if (useModifiedDamage)
            {
                damage = Mathf.RoundToInt(targetWeapon.maxHealth * Mathf.Abs(modifiedHealthPercent));
                damage = Mathf.Max(1, damage);

                if (modifiedHealthPercent > 0) // 扣血
                {
                    if (!CanDestroy)
                    {
                        targetWeapon.health = Mathf.Max(1, targetWeapon.health - damage);
                    }
                    else
                    {
                        targetWeapon.health -= damage;
                        if (targetWeapon.health <= 0)
                        {
                            willDestroy = true;
                            targetWeapon.health = 0;
                        }
                    }
                }
                else // ← 回血（modifiedHealthPercent < 0）
                {
                    int oldHealth = targetWeapon.health;

                    if (canOverMaxHp)
                    {
                        targetWeapon.health += damage;
                    }
                    else
                    {
                        targetWeapon.health = Mathf.Min(targetWeapon.maxHealth, targetWeapon.health + damage);
                        int actualHeal = targetWeapon.health - oldHealth;
                    }

                    // 播放回血特效
                    ShowHealEffect(targetWeapon);
                }
            }
        }
        else // 传统模式
        {
            damage = Mathf.RoundToInt(targetWeapon.maxHealth * fixedDamagePercent);
            damage = Mathf.Max(1, damage);

            if (!CanDestroy)
            {
                targetWeapon.health = Mathf.Max(1, targetWeapon.health - damage);
            }
            else
            {
                targetWeapon.health -= damage;
                if (targetWeapon.health <= 0)
                {
                    willDestroy = true;
                    targetWeapon.health = 0;
                }
            }
        }

        // 更新血量显示（必须在可能销毁前）
        if (IsInstanceValid(targetWeapon))
        {
            targetWeapon.UpdateHpLabel();
        }


        // 播放特效（使用保存的位置，不依赖目标对象）
        PlayHitEffectAt(targetPos);

        // 减少攻击次数
        remainingAttacks--;

        // 处理摧毁 - 关键：立即清理引用，但延迟销毁节点
        if (willDestroy)
        {

            // ✅ 关键：立即从所有管理结构中移除，防止其他代码访问
            RemoveWeaponFromAllSystems(targetWeapon);

            // ✅ 关键：使用 Godot 的延迟销毁，而不是自定义方法
            // 这样 Godot 会自己处理生命周期
            targetWeapon.SetDeferred("process_mode", (int)ProcessModeEnum.Disabled);

            // 创建一个定时器来延迟 QueueFree，避免当前帧问题
            var timer = new Godot.Timer();
            timer.WaitTime = 0.1f;
            timer.OneShot = true;
            timer.Autostart = true;
            AddChild(timer);

            // 使用 Callable，避免捕获 targetWeapon 引用
            // 改用目标名称查找（更安全）
            string destroyedWeaponName = targetName;
            timer.Timeout += () => {
                FindAndDestroyWeapon(destroyedWeaponName);
                timer.QueueFree();
            };
        }

        // 处理攻击后状态
        HandlePostAttack();
    }
    

    // ✅ 新增：从所有系统中移除兵器引用（立即执行）
    private void RemoveWeaponFromAllSystems(Weapon weapon)
    {
        if (weapon == null) return;


        // 1. 从格子移除
        if (weapon.grid != null)
        {
            weapon.grid.weapons.Remove(weapon);
            if (weapon.grid.weapon == weapon)
            {
                weapon.grid.weapon = null;
            }
            weapon.grid = null;
        }

        // 2. 从 GameManager 移除选中
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm != null)
        {
            if (gm.selectedWeapon == weapon)
            {
                gm.selectedWeapon = null;
            }

            // 3. 从 WeaponManager 列表移除（但不销毁）
            if (gm.weaponManager != null && gm.weaponManager.AllWeapons.Contains(weapon))
            {
                gm.weaponManager.AllWeapons.Remove(weapon);
            }
        }

        // 4. 禁用交互
        var area = weapon.GetNodeOrNull<Area2D>("Area2D");
        if (area != null)
        {
            area.InputPickable = false;
            area.Monitoring = false;
            area.Monitorable = false;
        }

        // 5. 隐藏视觉
        if (weapon.animSprite != null)
        {
            weapon.animSprite.Modulate = Colors.Transparent;
        }
        if (weapon.hpLabel != null)
        {
            weapon.hpLabel.Hide();
        }
    }

    // ✅ 新增：通过名称查找并销毁兵器（避免引用问题）
    private void FindAndDestroyWeapon(string weaponName)
    {

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.weaponManager == null)
        {
            return;
        }

        // 在场景中查找（可能还在列表中，也可能不在）
        foreach (var weapon in gm.weaponManager.AllWeapons.ToList())
        {
            if (weapon != null && IsInstanceValid(weapon) && weapon.Name == weaponName)
            {
                // ✅ 特殊地形处理
                if (weapon.grid != null && (weapon.grid.gridType == GridType.PIPESEAM || weapon.grid.gridType == GridType.METEORITE))
                {
                    var targetGrid = weapon.grid;
                    targetGrid.weapons.Remove(weapon);
                    if (targetGrid.weapon == weapon)
                        targetGrid.weapon = null;
                    targetGrid.gridType = GridType.BROKENPIPE;
                    targetGrid.isSupplyPoint = false;

                    var sprite = targetGrid.GetNodeOrNull<Sprite2D>("Sprite2D");
                    if (sprite != null)
                    {
                        var tween = CreateTween();
                        tween.TweenProperty(sprite, "modulate", new Color(0.3f, 0.3f, 0.3f), 0.5f);
                    }
                }

                // 再次确保从列表移除
                gm.weaponManager.AllWeapons.Remove(weapon);

                // 安全销毁
                SafeQueueFree(weapon);
                return;
            }
        }

        // 如果没找到，可能在根节点下
        var root = GetTree().CurrentScene;
        if (root != null)
        {
            var node = root.FindChild(weaponName, true, false);
            if (node is Weapon w)
            {
                SafeQueueFree(w);
            }
        }

    }

    // ✅ 新增：安全销毁节点
    private void SafeQueueFree(Node node)
    {
        if (node == null) return;

        try
        {
            if (IsInstanceValid(node) && !node.IsQueuedForDeletion())
            {
                node.QueueFree();
            }
            else
            {
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[SafeQueueFree] 销毁失败: {e.Message}");
        }
    }

    // ✅ 新增：播放命中特效（不依赖目标对象）
    private void PlayHitEffectAt(Vector2 position)
    {
        ShakeCamera(amplitude: 6f, times: 3);

        var flash = new CannonFlash();
        flash.Position = GlobalPosition;
        GetTree().CurrentScene.AddChild(flash);

        var hit = new HitEffect();
        hit.Position = position;
        GetTree().CurrentScene.AddChild(hit);
    }
    // 在 BlackCannon.cs 中添加这个方法
    // BlackCannon.cs - 添加对步兵的攻击方法
    public override void PerformAttack(Infantry target)
    {
        if (remainingAttacks <= 0) return;

        // 严格有效性检查
        if (target == null || !IsInstanceValid(target)) 
        {
            GD.Print("[PerformAttack] 目标无效，取消攻击");
            return;
        }

        // 保存关键数据（在修改前）
        Vector2 targetPos = target.GlobalPosition;
        string targetName = target.Name;

        GD.Print($"[PerformAttack] {Name} 攻击 {targetName}");

        // 根据模式应用伤害
        if (useModifiedDamage)
        {
            ApplyModifiedEffect(target);
        }
        else
        {
            ApplyTraditionalDamage(target);
        }

        // 播放特效（使用保存的位置，不依赖目标对象）
        PlayHitEffectAt(targetPos);

        // 减少攻击次数
        remainingAttacks--;

        // 处理攻击后状态
        HandlePostAttack();
    }

    // ========== ✅ 新增：回合开始处理（冷却+弹药系统） ==========
public override void OnTurnStart()
{
    base.OnTurnStart(); // hasActed=false, remainingAttacks=maxAttacksPerTurn

    if (useCooldownSystem)
    {
        // ✅ 关键：只有冷却未就绪时才增加计数
        if (!cooldownReady)
        {
            turnsSinceLastAttack++;
            totalTurnsPassed++;
            
            // 检查是否完成冷却
            if (turnsSinceLastAttack >= cooldownTurns)
            {
                // 新周期开始！
                turnsSinceLastAttack = 0;
                cooldownReady = true;
                
                // 刷新攻击次数
                if (storeAttacks)
                {
                    attacksRemainingInCycle = Mathf.Min(attacksPerCooldown + attacksRemainingInCycle, attacksPerCooldown * 2);
                    GD.Print($"[BlackCannon] {Name} 冷却完成！存次数模式：剩余{attacksRemainingInCycle}次");
                }
                else
                {
                    attacksRemainingInCycle = attacksPerCooldown;
                    GD.Print($"[BlackCannon] {Name} 冷却完成！次数重置为{attacksPerCooldown}次");
                }
            }
            else
            {
                GD.Print($"[BlackCannon] {Name} 冷却中... {turnsSinceLastAttack}/{cooldownTurns}回合");
            }
        }
        else
        {
            // 冷却已就绪，检查是否还有剩余次数
            if (attacksRemainingInCycle <= 0 && !storeAttacks)
            {
                // 次数用完且不留存，需要重新冷却
                cooldownReady = false;
                turnsSinceLastAttack = 0; // 从0开始计数，下次调用时变成1
                GD.Print($"[BlackCannon] {Name} 次数用完，开始新的冷却周期");
            }
            else
            {
                GD.Print($"[BlackCannon] {Name} 冷却就绪，剩余次数:{attacksRemainingInCycle}");
            }
        }
    }
    
    UpdateAmmoVisual();
    
    // 根据弹药和冷却状态设置视觉
    if (!HasAmmoAndCooldown())
    {
        if (animSprite != null)
            animSprite.Modulate = new Color(0.7f, 0.7f, 0.7f, 1f);
    }
    else
    {
        SetVisualNormal();
    }
}

    // ========== ✅ 新增：回合结束处理 ==========
    public override void OnTurnEnd()
    {
        base.OnTurnEnd();

        if (useCooldownSystem)
        {
            if (!storeAttacks)
            {
                // 不存次数：回合结束清空剩余次数
                if (attacksRemainingInCycle > 0)
                {
                    GD.Print($"[BlackCannon] {Name} 回合结束，未使用次数清零（{attacksRemainingInCycle}次浪费）");
                    attacksRemainingInCycle = 0;
                }
            }
            else
            {
                // 存次数：保留到下一周期（已在OnTurnStart中处理累加）
                GD.Print($"[BlackCannon] {Name} 回合结束，保留{attacksRemainingInCycle}次到下一周期");
            }
        }
    }

    public override void HandlePostAttack()
    {
        if (remainingAttacks <= 0 || !HasAmmoAndCooldown())
        {
            hasActed = true;
            SetVisualDark();

            var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
            gm?.gridManager.ClearWeaponRange();

            // ✅ 关键：清除选中状态
            if (gm != null) gm.selectedWeapon = null;

            var menu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
            menu?.Hide();
        }
        else
        {
            // 延迟刷新攻击范围，等待销毁完成
            var refreshTimer = GetTree().CreateTimer(0.15f);
            refreshTimer.Timeout += () => {
                if (IsInstanceValid(this) && !hasActed)
                {
                    ShowAttackRange();
                }
            };
        }
    }

    private void DeferredRemoveWeapon(Weapon weapon)
    {
        // ✅ 关键修复：多重检查确保对象有效
        if (weapon == null) 
        {

            return;
        }

        if (!IsInstanceValid(weapon))
        {

            return;
        }

        // ✅ 特殊地形处理：兵器被消灭前检查所在格子
        if (weapon.grid != null && (weapon.grid.gridType == GridType.PIPESEAM || weapon.grid.gridType == GridType.METEORITE))
        {
            string terrainName = weapon.grid.gridType == GridType.PIPESEAM ? "PIPESEAM" : "METEORITE";
            GD.Print($"[BlackCannon] 被消灭兵器位于 {terrainName}，准备切换为 GROUND");
            var targetGrid = weapon.grid;

            // 清理兵器引用
            targetGrid.weapons.Remove(weapon);
            if (targetGrid.weapon == weapon)
                targetGrid.weapon = null;

            // 切换地形
            targetGrid.gridType = GridType.BROKENPIPE;
            targetGrid.isSupplyPoint = false;

            // 更新视觉
            var sprite = targetGrid.GetNodeOrNull<Sprite2D>("Sprite2D");
            if (sprite != null)
            {
                var tween = CreateTween();
                tween.TweenProperty(sprite, "modulate", new Color(0.3f, 0.3f, 0.3f), 0.5f);
            }

            GD.Print($"[BlackCannon] {targetGrid.GridIndex} 已从 {terrainName} 切换为 GROUND");
        }

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm != null)
        {
            // 如果当前选中的是被摧毁的兵器，清空选中
            if (gm.selectedWeapon == weapon)
            {
                gm.selectedWeapon = null;

            }

            // 通过 WeaponManager 安全移除
            if (gm.weaponManager != null)
            {
                // ✅ 关键修复：检查 weapon 是否还在列表中
                if (gm.weaponManager.AllWeapons.Contains(weapon))
                {
                    gm.weaponManager.RemoveWeapon(weapon);
                }
                else
                {

                    weapon.QueueFree();
                }
            }
            else
            {

                weapon.QueueFree();
            }
        }
        else
        {

            weapon.QueueFree();
        }


    }

    private void ApplyTraditionalDamage(Infantry target)
    {
        int damage = Mathf.RoundToInt(target.maxHealth * fixedDamagePercent);
        damage = Mathf.Max(1, damage);

        if (!CanDestroy)
        {
            target.health = Mathf.Max(1, target.health - damage);
        }
        else
        {
            target.health -= damage;
        }

        target.UpdateHpLabel();
        GD.Print($"[BlackCannon] {Name} 轰击 {target.Name}，" +
                 (CanDestroy ? "造成" : "锁血造成") + $" {damage} 点伤害");

        if (target.health <= 0)
        {
            var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
            gm?.RemovePiece(target);
            target.QueueFree();
        }
    }

    // ✅ 修改：应用改良效果，支持CanOverMaxHp选项
    private void ApplyModifiedEffect(Infantry target)
    {
        // ========== 1. 血量处理 ==========
        int healthChange = Mathf.RoundToInt(target.maxHealth * Mathf.Abs(modifiedHealthPercent));

        if (modifiedHealthPercent > 0)
        {
            // 正数：扣血
            if (!CanDestroy)
            {
                target.health = Mathf.Max(1, target.health - healthChange);
            }
            else
            {
                target.health -= healthChange;
            }
            GD.Print($"[BlackCannon] {Name} 对 {target.Name} 造成 {healthChange} 点伤害（改良模式）");
        }
        else if (modifiedHealthPercent < 0)
        {
            // 负数：回血
            int oldHealth = target.health;

            // ✅ 关键修改：根据canOverMaxHp决定是否限制上限
            if (canOverMaxHp)
            {
                // 允许超出血量上限
                target.health += healthChange;
                GD.Print($"[BlackCannon] {Name} 为 {target.Name} 回复 {healthChange} 点生命（突破上限！当前:{target.health}）");
            }
            else
            {
                // 默认：不能超过maxHealth
                target.health = Mathf.Min(target.maxHealth, target.health + healthChange);
                int actualHeal = target.health - oldHealth;
                GD.Print($"[BlackCannon] {Name} 为 {target.Name} 回复 {actualHeal} 点生命（改良模式）");
            }

            ShowHealEffect(target);
        }

        target.UpdateHpLabel();

        // ========== 2. 弹药处理 ==========
        if (modifiedAmmoPercent != 0 && target.hasPrimaryWeapon && target.primaryHasLimitedAmmo)
        {
            int ammoChange = Mathf.RoundToInt(target.maxPrimaryAmmo * Mathf.Abs(modifiedAmmoPercent));

            if (modifiedAmmoPercent > 0)
            {
                target.currentPrimaryAmmo = Mathf.Max(0, target.currentPrimaryAmmo - ammoChange);
                GD.Print($"[BlackCannon] {Name} 对 {target.Name} 消耗 {ammoChange} 发弹药");
            }
            else
            {
                // ✅ 弹药也可以超上限（如果canOverMaxHp为true）
                if (canOverMaxHp)
                {
                    target.currentPrimaryAmmo += ammoChange;
                    GD.Print($"[BlackCannon] {Name} 为 {target.Name} 补充 {ammoChange} 发弹药（突破上限！当前:{target.currentPrimaryAmmo}）");
                }
                else
                {
                    int oldAmmo = target.currentPrimaryAmmo;
                    target.currentPrimaryAmmo = Mathf.Min(target.maxPrimaryAmmo, target.currentPrimaryAmmo + ammoChange);
                    int actualReload = target.currentPrimaryAmmo - oldAmmo;
                    GD.Print($"[BlackCannon] {Name} 为 {target.Name} 补充 {actualReload} 发弹药");
                }
            }
        }

        // ========== 3. 油量处理 ==========
        if (modifiedFuelPercent != 0 && target.consumeFuel)
        {
            int fuelChange = Mathf.RoundToInt(target.maxFuel * Mathf.Abs(modifiedFuelPercent));

            if (modifiedFuelPercent > 0)
            {
                target.fuel = Mathf.Max(0, target.fuel - fuelChange);
                GD.Print($"[BlackCannon] {Name} 对 {target.Name} 消耗 {fuelChange} 点燃料");
            }
            else
            {
                // ✅ 油量也可以超上限（如果canOverMaxHp为true）
                if (canOverMaxHp)
                {
                    target.fuel += fuelChange;
                    GD.Print($"[BlackCannon] {Name} 为 {target.Name} 补充 {fuelChange} 点燃料（突破上限！当前:{target.fuel}）");
                }
                else
                {
                    int oldFuel = target.fuel;
                    target.fuel = Mathf.Min(target.maxFuel, target.fuel + fuelChange);
                    int actualRefuel = target.fuel - oldFuel;
                    GD.Print($"[BlackCannon] {Name} 为 {target.Name} 补充 {actualRefuel} 点燃料");
                }
            }
        }

        // ========== 4. 死亡检查 ==========
        if (target.health <= 0)
        {
            var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
            gm?.RemovePiece(target);
            target.QueueFree();
        }
    }

    // ✅ 修改：对兵器应用改良效果，支持CanOverMaxHp，并修复死机
    private void ApplyModifiedEffectToWeapon(Weapon targetWeapon)
    {
        // ✅ 关键修复：提前检查
        if (targetWeapon == null || !IsInstanceValid(targetWeapon)) return;

        int healthChange = Mathf.RoundToInt(targetWeapon.maxHealth * Mathf.Abs(modifiedHealthPercent));
        bool willBeDestroyed = false;

        if (modifiedHealthPercent > 0)
        {
            // 正数：扣血
            if (!CanDestroy)
            {
                targetWeapon.health = Mathf.Max(1, targetWeapon.health - healthChange);
            }
            else
            {
                targetWeapon.health -= healthChange;
                if (targetWeapon.health <= 0)
                {
                    willBeDestroyed = true;
                    targetWeapon.health = 0;
                }
            }
            GD.Print($"[BlackCannon] {Name} 对 {targetWeapon.Name} 造成 {healthChange} 点伤害（改良模式）");
        }
        else if (modifiedHealthPercent < 0)
        {
            // 负数：回血（略，不变）
            int oldHealth = targetWeapon.health;

            if (canOverMaxHp)
            {
                targetWeapon.health += healthChange;
                GD.Print($"[BlackCannon] {Name} 为 {targetWeapon.Name} 修复 {healthChange} 点耐久（突破上限！当前:{targetWeapon.health}）");
            }
            else
            {
                targetWeapon.health = Mathf.Min(targetWeapon.maxHealth, targetWeapon.health + healthChange);
                int actualHeal = targetWeapon.health - oldHealth;
                GD.Print($"[BlackCannon] {Name} 为 {targetWeapon.Name} 修复 {actualHeal} 点耐久（改良模式）");
            }

            ShowHealEffect(targetWeapon);
        }

        targetWeapon.UpdateHpLabel();

        // ✅ 关键修复：如果需要销毁，先清理引用再延迟销毁
        if (willBeDestroyed)
        {
            GD.Print($"[BlackCannon] {targetWeapon.Name} 被改良攻击摧毁！");

            // 立即从格子移除引用
            if (targetWeapon.grid != null)
            {
                targetWeapon.grid.weapons.Remove(targetWeapon);
                if (targetWeapon.grid.weapon == targetWeapon)
                    targetWeapon.grid.weapon = null;
                targetWeapon.grid = null;
            }

            CallDeferred(nameof(DeferredRemoveWeapon), targetWeapon);
        }
    }


    private void ShowHealEffect(Node2D target)
    {
        var healEffect = new HealEffect();
        healEffect.Position = target.GlobalPosition;
        GetTree().CurrentScene.AddChild(healEffect);
    }

    public void DoWait()
    {
        hasActed = true;
        SetVisualDark();

        // ✅ 关键：清除 GameManager 的选中状态
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm != null)
        {
            gm.selectedWeapon = null;
        }

        // ✅ 关键：清除攻击范围显示
        var gmGrid = GetTree().GetFirstNodeInGroup("grid_manager") as GridManager;
        gmGrid?.ClearWeaponRange();

        var menu = GetTree().GetFirstNodeInGroup("action_menu") as ActionMenu;
        menu?.Hide();
    }

    private void PlayCannonEffect(Infantry target)
    {
        ShakeCamera(amplitude: 6f, times: 3);

        var flash = new CannonFlash();
        flash.Position = GlobalPosition;
        GetTree().CurrentScene.AddChild(flash);

        if (target != null && IsInstanceValid(target))
        {
            var hit = new HitEffect();
            hit.Position = target.GlobalPosition;
            GetTree().CurrentScene.AddChild(hit);
        }
    }

    private void ShakeCamera(float amplitude, int times)
    {
        var cam = GetViewport().GetCamera2D();
        if (cam == null) return;
        var tween = cam.CreateTween().SetParallel(false);
        for (int i = 0; i < times; i++)
        {
            var rnd = new Vector2(GD.Randf() * amplitude - amplitude * 0.5f,
                                  GD.Randf() * amplitude - amplitude * 0.5f);
            tween.TweenProperty(cam, "position", cam.Position + rnd, 0.05f);
        }
        tween.TweenProperty(cam, "position", cam.Position, 0.05f);
    }

    // ========== ✅ 新增：获取黑炮完整信息（用于信息面板） ==========
    public string GetBlackCannonFullInfo()
    {
        string info = "";

        info += $"[黑炮] {Name}\n";
        info += $"团队: {team}\n";
        info += $"血量: {health}/{maxHealth} ({Mathf.CeilToInt(health/10f)}格)\n";
        info += $"状态: {(hasActed ? "已行动" : "待机")}\n";

        // ✅ 弹药信息
        info += $"\n=== 弹药系统 ===\n";
        if (useAmmoSystem)
        {
            info += $"弹药: {currentAmmo}/{maxAmmo}\n";
            info += $"补给状态: {(currentAmmo >= maxAmmo ? "已满" : "可补给")}\n";
        }
        else
        {
            info += $"弹药: 无限 (∞)\n";
        }

        // ✅ 冷却信息
        info += $"\n=== 冷却系统 ===\n";
        if (useCooldownSystem)
        {
            info += $"冷却周期: {cooldownTurns}回合\n";
            info += $"周期攻击次数: {attacksPerCooldown}次\n";
            info += $"存次数: {(storeAttacks ? "是" : "否")}\n";
            info += $"当前周期进度: {turnsSinceLastAttack}/{cooldownTurns}回合\n";
            info += $"周期剩余次数: {attacksRemainingInCycle}次\n";
            info += $"冷却就绪: {(cooldownReady ? "✓ 是" : "✗ 否")}\n";

            if (!cooldownReady)
            {
                info += $"还需等待: {cooldownTurns - turnsSinceLastAttack}回合\n";
            }
        }
        else
        {
            info += $"冷却: 无（每回合重置）\n";
            info += $"每回合次数: {maxAttacksPerTurn}次\n";
        }

        // ✅ 综合攻击状态
        info += $"\n=== 攻击状态 ===\n";
        info += $"可攻击: {(CanAttack() ? "✓ 是" : "✗ 否")}\n";
        if (!CanAttack())
        {
            if (useAmmoSystem && currentAmmo <= 0)
                info += $"原因: 弹药耗尽\n";
            if (useCooldownSystem && !cooldownReady)
                info += $"原因: 冷却中 ({turnsSinceLastAttack}/{cooldownTurns}回合)\n";
            if (useCooldownSystem && attacksRemainingInCycle <= 0)
                info += $"原因: 周期次数已用完\n";
        }

        info += $"\n=== 配置信息 ===\n";
        info += $"射程深度: {maxAttackDepth}格\n";
        info += $"伤害模式: {(useModifiedDamage ? "改良模式" : "传统模式")}\n";
        info += $"朝向: {direction}\n";
        info += $"可攻击兵器: {(CanAttackWeapon ? "是" : "否")}\n";
        info += $"目标选择: {targetSelectionMode switch 
        { 
            TargetSelectionMode.AllSelect => "所有单位", 
            TargetSelectionMode.OnlyEnemyUnits => "仅敌方单位", 
            TargetSelectionMode.OnlyUserUnits => "仅己方单位", 
            _ => "未知"
        }}\n";

        if (!useModifiedDamage)
        {
            info += $"固定伤害: {fixedDamagePercent * 100:F0}%\n";
            info += $"可摧毁: {(CanDestroy ? "是" : "否")}\n";
        }
        else
        {
            info += $"血量变化: {modifiedHealthPercent * 100:F0}% ({(modifiedHealthPercent > 0 ? "扣血" : modifiedHealthPercent < 0 ? "回血" : "无变化")})\n";
            info += $"弹药变化: {modifiedAmmoPercent * 100:F0}%\n";
            info += $"燃料变化: {modifiedFuelPercent * 100:F0}%\n";
            info += $"可超血量上限: {(canOverMaxHp ? "是" : "否")}\n";
        }

        info += $"\n[操作说明]\n";
        info += $"• 左键点击: 选择/显示攻击范围\n";
        info += $"• R键: 旋转方向\n";
        info += $"• 攻击范围: 三角形区域，深度{maxAttackDepth}格\n";

        if (useAmmoSystem)
        {
            info += $"• 需要APC在范围内补给弹药\n";
        }

        return info;
    }
}

// 回血特效类
public partial class HealEffect : Node2D
{
    private float t = 0f;
    private const float DURATION = 0.5f;

    public override void _Process(double delta)
    {
        t += (float)delta;
        if (t >= DURATION) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float a = 1f - t / DURATION;
        float r = 10f + t * 40f;

        DrawArc(Vector2.Zero, r, 0, Mathf.Pi * 2, 32, new Color(0, 1, 0, a * 0.8f), 3f);
        DrawCircle(Vector2.Zero, 6f, new Color(0.5f, 1, 0.5f, a));
        DrawLine(new Vector2(-15, 0), new Vector2(15, 0), new Color(0, 1, 0, a), 3f);
        DrawLine(new Vector2(0, -15), new Vector2(0, 15), new Color(0, 1, 0, a), 3f);
    }
}
