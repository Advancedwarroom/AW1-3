// LargeCannon.cs - 大型多格黑炮（3×3占据），继承BlackCannon
// 还原原版AW Black Cannon的大型版本：3×3不可通行，仅弱点可攻击，摧毁后Broken形态仍不可通行
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class LargeCannon : BlackCannon
{
    [ExportGroup("大型多格配置")]
    [Export] public Vector2I multiTileSize = new Vector2I(3, 3);

    public override void _Ready()
    {
        // 在base._Ready之前设置多格属性
        isMultiTile = true;
        size = multiTileSize;
        base._Ready();
        
        // 更新弱点偏移（基于当前方向）
        UpdateWeakPointOffset();
    }

    // 根据方向计算弱点在3×3中的偏移
    // 坐标系：锚点(0,0)为左上，x向右，y向下
    // Right: (2,1) - 最右列中间
    // Left:  (0,1) - 最左列中间
    // Up:    (1,2) - 中间列最下方（固定位置）
    // Down:  (1,2) - 中间列最下方（固定位置）
    public Vector2I GetWeakPointOffsetForDirection(CannonDirection dir)
    {
        return dir switch
        {
            CannonDirection.Right => new Vector2I(2, 1),
            CannonDirection.Left  => new Vector2I(0, 1),
            CannonDirection.Up    => new Vector2I(1, 2),
            CannonDirection.Down  => new Vector2I(1, 2),
            _ => new Vector2I(1, 2)
        };
    }

    private void UpdateWeakPointOffset()
    {
        weakPointOffset = GetWeakPointOffsetForDirection(direction);
    }

    public override void RotateDirection()
    {
        base.RotateDirection();
        UpdateWeakPointOffset();
        UpdateMultiTileVisual();
    }

    public override void UpdateDirectionVisual()
    {
        base.UpdateDirectionVisual();
        UpdateMultiTileVisual();
    }

    public override void UpdateMultiTileVisual()
    {
        // 大贴图位置调整：锚点是(0,0)，但贴图中心需要覆盖3×3区域
        // 3×3的中心是(1,1)，相对于锚点的偏移是(1,1)格 = 32,32像素
        if (animSprite != null)
        {
            // 将贴图中心对准3×3的中心
            animSprite.Position = new Vector2(
                16 + (multiTileSize.X - 1) * 16f,
                16 + (multiTileSize.Y - 1) * 16f
            );
        }
    }

    public override Grids GetWeakPointGrid()
    {
        if (occupiedGrids.Count == 0) return grid;
        var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.gridManager?.map == null) return grid;
        
        var anchorIndex = grid?.GridIndex ?? new Vector2I(0, 0);
        var wpOffset = GetWeakPointOffsetForDirection(direction);
        var wpIndex = anchorIndex + wpOffset;
        
        if (wpIndex.X >= 0 && wpIndex.X < gm.gridManager.searchRange.X &&
            wpIndex.Y >= 0 && wpIndex.Y < gm.gridManager.searchRange.Y)
        {
            return gm.gridManager.map[wpIndex.X, wpIndex.Y];
        }
        return grid;
    }

    public override bool IsWeakPointGrid(Grids checkGrid)
    {
        if (!isMultiTile || occupiedGrids.Count == 0) return true;
        return checkGrid == GetWeakPointGrid();
    }

    // 计算攻击范围：从弱点格子开始，而不是从锚点开始
    public override List<Grids> CalculateAttackRange()
    {
        var range = new List<Grids>();
        var weakPointGrid = GetWeakPointGrid();
        if (weakPointGrid == null) return range;

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.gridManager?.map == null) return range;

        Vector2I dir = DirectionVectors[(int)direction];
        Vector2I perp = PerpVectors[(int)direction];
        Vector2I start = weakPointGrid.GridIndex;

        // 遍历每一层深度（距离）
        for (int depth = 1; depth <= maxAttackDepth; depth++)
        {
            Vector2I center = start + dir * depth;
            int halfWidth = depth;

            for (int w = -halfWidth; w <= halfWidth; w++)
            {
                Vector2I checkPos = center + perp * w;

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
        // 如果被摧毁，不再接受伤害
        if (isDestroyed) return;
        
        base.TakeDamage(damage);
    }

    protected override void OnDestroyed()
    {
        // 标记为多格摧毁状态
        OnMultiTileDestroyed();
        
        // 从所有管理结构中移除，但格子仍保持被占据（不可通行）
        var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.gridManager?.ClearWeaponRange();
        Grids.IsForceActionMode = false;
        
        if (gm != null && gm.selectedWeapon == this)
            gm.selectedWeapon = null;
        
        // 禁用交互
        var area = GetNodeOrNull<Area2D>("Area2D");
        if (area != null)
        {
            area.InputPickable = false;
            area.Monitoring = false;
            area.Monitorable = false;
        }
        
        // 隐藏血条和旋转提示
        if (hpLabel != null) hpLabel.Hide();
        var rotateTip = GetNodeOrNull<Label>("RotateTip");
        if (rotateTip != null) rotateTip.Hide();
        
        // 播放摧毁特效
        ShakeCamera(amplitude: 10f, times: 5);
        var flash = new CannonFlash();
        flash.Position = GlobalPosition;
        GetTree().CurrentScene?.AddChild(flash);
        
        // 胜利判定检查
        gm?.CheckVictoryCondition();
    }

    public override string GetBrokenAnimName()
    {
        string dirSuffix = direction switch
        {
            CannonDirection.Up    => "Up",
            CannonDirection.Down  => "Down",
            CannonDirection.Left  => "Left",
            CannonDirection.Right => "Right",
            _ => "Up"
        };
        return $"Broken{dirSuffix}";
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

    public override string GetBlackCannonFullInfo()
    {
        string info = base.GetBlackCannonFullInfo();
        info = info.Replace("[黑炮]", "[大型黑炮]");
        info += $"\n=== 多格占据 ===\n";
        info += $"尺寸: {size.X}×{size.Y}\n";
        info += $"弱点位置: {direction} ({GetWeakPointOffsetForDirection(direction)})\n";
        info += $"状态: {(isDestroyed ? "已摧毁" : "运作中")}\n";
        if (contributesToVictory)
        {
            info += $"\n[胜利条件] 摧毁此兵器计入胜利判定\n";
        }
        return info;
    }

    public override void OnTurnStart()
    {
        if (isDestroyed) return; // 已摧毁不再行动
        base.OnTurnStart();
    }

    public override void ShowAttackRange()
    {
        if (isDestroyed) return; // 已摧毁不显示范围
        base.ShowAttackRange();
    }

    public override bool CanAttack()
    {
        if (isDestroyed) return false;
        return base.CanAttack();
    }
}
