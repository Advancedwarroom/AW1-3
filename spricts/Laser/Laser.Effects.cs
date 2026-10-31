// Laser.cs - 高级激光炮系统（修复版）
// 修复内容：
// 1. 多次攻击同步 remainingAttacks 和 attacksRemainingInCycle
// 2. 添加局内角度配置API（EnableAngle/DisableAngle/SetFireAngles等）
// 3. ✅ 移除 attackedThisRound 限制：允许同一回合多次攻击同一目标
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Laser · Effects（由 Laser.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class Laser
{

    // ========== 激光特效 ==========

    private Vector2 CalculateLaserEffectEndPoint(float angleDegrees, List<LaserTargetInfo> angleTargets)
    {
        var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.gridManager == null) return grid.Position + new Vector2(16, 16);

        float angleRad = Mathf.DegToRad(angleDegrees);
        Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
        Vector2 startPos = grid.Position + new Vector2(16, 16);

        if (maxPiercePerLine == 0 && useInfiniteRange)
        {
            var allRayGrids = CalculateRayGrids(angleDegrees);
            if (angleTargets.Count > 0)
            {
                var lastTarget = angleTargets.OrderByDescending(t => t.Distance).First();
                return ExtendToBoundary(lastTarget.Grid.Position + new Vector2(16, 16), dir, gm);
            }
            if (allRayGrids.Count > 0)
                return ExtendToBoundary(allRayGrids.Last().Grid.Position + new Vector2(16, 16), dir, gm);
            return startPos + dir * 1000f;
        }

        if (maxPiercePerLine > 0 && useInfiniteRange)
        {
            var allRayGrids = CalculateRayGrids(angleDegrees);
            int pierceCount = 0;
            Grids lastAttackableGrid = null;
            foreach (var rayInfo in allRayGrids)
            {
                if (pierceCount >= maxPiercePerLine) break;
                var targets = GetValidTargetsInGrid(rayInfo.Grid);
                if (targets.Count > 0)
                {
                    lastAttackableGrid = rayInfo.Grid;
                    pierceCount += targets.Count;
                }
            }
            if (lastAttackableGrid != null)
                return lastAttackableGrid.Position + new Vector2(16, 16);
            if (allRayGrids.Count > 0)
                return ExtendToBoundary(allRayGrids.Last().Grid.Position + new Vector2(16, 16), dir, gm);
            return startPos + dir * 1000f;
        }

        if (!useInfiniteRange)
        {
            Vector2 endPos = startPos + dir * maxLaserLength * gm.gridManager.gridSize.X;
            return ExtendToBoundary(endPos, dir, gm);
        }

        return startPos + dir * 500f;
    }

    private Vector2 ExtendToBoundary(Vector2 fromPos, Vector2 dir, GameManager gm)
    {
        float cellSize = gm.gridManager.gridSize.X;
        int mapWidth = gm.gridManager.searchRange.X;
        int mapHeight = gm.gridManager.searchRange.Y;
        float maxDistX = float.MaxValue, maxDistY = float.MaxValue;
        if (dir.X > 0.001f) maxDistX = ((gm.gridManager.startPos.X + mapWidth * cellSize) - fromPos.X) / dir.X;
        else if (dir.X < -0.001f) maxDistX = (gm.gridManager.startPos.X - fromPos.X) / dir.X;
        if (dir.Y > 0.001f) maxDistY = ((gm.gridManager.startPos.Y + mapHeight * cellSize) - fromPos.Y) / dir.Y;
        else if (dir.Y < -0.001f) maxDistY = (gm.gridManager.startPos.Y - fromPos.Y) / dir.Y;
        float maxDist = Mathf.Min(maxDistX, maxDistY);
        if (maxDist < 0 || maxDist > 10000f) maxDist = 1000f;
        return fromPos + dir * (maxDist + cellSize * 2);
    }

    private void PlayGlobalLaserEffect(List<LaserTargetInfo> targets)
    {
        var cam = GetViewport()?.GetCamera2D();
        if (cam != null)
        {
            var tween = cam.CreateTween().SetParallel(false);
            for (int i = 0; i < 3; i++)
            {
                var rnd = new Vector2(GD.Randf() * 6f - 3f, GD.Randf() * 6f - 3f);
                tween.TweenProperty(cam, "position", cam.Position + rnd, 0.05f);
            }
            tween.TweenProperty(cam, "position", cam.Position, 0.05f);
        }

        var angleGroups = targets.GroupBy(t => t.Angle);
        foreach (var group in angleGroups)
        {
            float angle = group.Key;
            var angleTargets = group.ToList();
            if (angleTargets.Count == 0) continue;
            Vector2 endPoint = CalculateLaserEffectEndPoint(angle, angleTargets);
            var laserLine = new LaserLineEffect();
            laserLine.Setup(grid.Position + new Vector2(16, 16), endPoint, GetTeamColor());
            GetTree()?.CurrentScene?.AddChild(laserLine);
            foreach (var t in angleTargets)
            {
                var hit = new LaserHitEffect();
                hit.Position = t.Target.GlobalPosition;
                GetTree()?.CurrentScene?.AddChild(hit);
            }
        }
    }

    private Color GetTeamColor()
    {
        return TeamHelper.GetTeamColor(team); // 激光颜色跟随势力主色（含新增势力）
    }
}
