// GridManager.cs - 修改 HandleGridClick 和 SetupMoveRangeCallbacks 方法
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

// GridManager · TouchInput（由 GridManager.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class GridManager
{

    // ========== ✅ 移动端触摸轨迹预览状态 ==========
    private bool isMobilePlatform = false;
    private bool touchPathActive = false;
    private System.Collections.Generic.List<Grids> touchPathGrids;   // ✅ 手画路径航点

    // ✅ 单位交互激活态：移动范围显示/画轨迹期间，相机须让位
    public bool IsMoveRangeActive => moveRange != null && moveRange.Count > 0 && selectedUnit != null;
    private int activeTouchCount = 0;
    private Grids touchHoverGrid = null;

    // ========== ✅ 移动端：手指划过显示移动轨迹（移动范围激活时接管触摸输入） ==========
    public override void _Input(InputEvent @event)
    {
        if (!isMobilePlatform) return;

        bool moveRangeActive = moveRange.Count > 0 && selectedUnit != null;

        if (@event is InputEventScreenTouch touch)
        {
            if (touch.Pressed)
            {
                activeTouchCount++;
                if (!moveRangeActive) { touchPathActive = false; return; }
                if (activeTouchCount >= 2)
                {
                    // 双指操作（缩放相机）：退出轨迹模式，交给相机控制器
                    touchPathActive = false;
                    touchHoverGrid = null;
                    ClearPathPreview();
                    return;
                }
                // 起点在 UI 控件上：不干预，让按钮正常响应
                if (IsTouchOverUI(touch.Position)) { touchPathActive = false; return; }
                // 不拦截事件：格子拾取已被禁用，相机平移由 Drag 拦截
                touchPathActive = true;
                UpdateTouchHover(touch.Position);
            }
            else
            {
                activeTouchCount = Mathf.Max(0, activeTouchCount - 1);
                if (!touchPathActive) return;
                touchPathActive = false;
                var target = touchHoverGrid;
                touchHoverGrid = null;
                ClearPathPreview();
                // ✅ 手画路径优先：有航点按航点执行（路径路程计费/绕圈合法/截断于阻挡）
                var drawn = touchPathGrids;
                touchPathGrids = null;
                if (moveRangeActive && target != null)
                {
                    if (drawn != null && drawn.Count > 0 && HandleDrawnPath(drawn))
                        return;
                    if (target.OnClickGrid != null) target.OnClickGrid.Invoke(target);
                    else target.OnClickEmpty?.Invoke();
                }
                // 不拦截：让相机控制器清理触摸状态
            }
        }
        else if (@event is InputEventScreenDrag drag)
        {
            if (!moveRangeActive || !touchPathActive) return;
            UpdateTouchHover(drag.Position);
            GetViewport().SetInputAsHandled(); // 画轨迹期间阻止相机平移
        }
    }

    private void UpdateTouchHover(Vector2 screenPos)
    {
        var grid = ScreenPosToGrid(screenPos);
        touchHoverGrid = grid;
        if (grid != null && moveRange.Contains(grid))
        {
            // ✅ 记录手画航点（去重、断链容忍，校验在松手时）
            if (touchPathGrids == null) touchPathGrids = new System.Collections.Generic.List<Grids>();
            if (touchPathGrids.Count == 0 || touchPathGrids[touchPathGrids.Count - 1] != grid)
                touchPathGrids.Add(grid);
            // ✅ 手画预览：画线走你的手指路线（含截断红标），不再是优化最短路径
            if (touchPathGrids.Count > 0)
                DrawTouchPathPreview(touchPathGrids);
            else
                OnMouseEnteredGrid(grid);
        }
        else
            ClearPathPreview();
    }

    private Grids ScreenPosToGrid(Vector2 screenPos)
    {
        if (map == null) return null;
        Vector2 world = GetViewport().GetCanvasTransform().AffineInverse() * screenPos;
        int x = Mathf.FloorToInt((world.X - startPos.X) / gridSize.X);
        int y = Mathf.FloorToInt((world.Y - startPos.Y) / gridSize.Y);
        if (x < 0 || x >= searchRange.X || y < 0 || y >= searchRange.Y) return null;
        return map[x, y];
    }

    // 检查屏幕坐标是否落在任何可见可交互 UI 控件上（避免拦截按钮点击）
    private bool IsTouchOverUI(Vector2 screenPos)
    {
        return FindUIAtPoint(GetTree().Root, screenPos);
    }

    private bool FindUIAtPoint(Node node, Vector2 screenPos)
    {
        foreach (var child in node.GetChildren())
        {
            // 先递归子控件（更具体的优先）
            if (FindUIAtPoint(child, screenPos)) return true;
            if (child is Control c && c.IsVisibleInTree()
                && c.MouseFilter != Control.MouseFilterEnum.Ignore
                && c.GetGlobalRect().HasPoint(screenPos))
                return true;
        }
        return false;
    }

    // ✅ 手画轨迹预览：沿手指路线画线，第一个可见阻挡格红标截断
    private void DrawTouchPathPreview(System.Collections.Generic.List<Grids> path)
    {
        if (selectedUnit == null) return;
        ResetMoveRangeIconColors();
        previewPath = path;
        pathLine?.ClearPoints();
        if (pathLine != null)
            foreach (var g in path)
                if (g != null) pathLine.AddPoint(g.Position + new Vector2(20, 20));
        for (int i = 0; i < path.Count; i++)
        {
            var g = path[i];
            if (g?.infantries == null) continue;
            if (g.infantries.Any(u => u != null && u != selectedUnit && Infantry.IsVisibleTo(u, selectedUnit.team, grids)))
            {
                g.pathIcon?.Show();
                g.pathIcon?.SetDeferred("modulate", Colors.Red);
                break;   // 截断点之后不再渲染
            }
        }
    }

    // ✅ 手画路径执行：路径路程计费、绕圈合法、终点≠起点、截断于路上单位（可见判定，隐形标准直通雾战）
    private bool HandleDrawnPath(System.Collections.Generic.List<Grids> waypoints)
    {
        if (selectedUnit == null || waypoints == null) return false;
        var infantry = selectedUnit;
        int budget = infantry.movePoints;
        var valid = new System.Collections.Generic.List<Grids>();
        int spent = 0;
        Grids prev = infantry.grid;
        foreach (var wp in waypoints)
        {
            if (wp == null || wp == prev) continue;
            // 终点=起点：本回合拒绝绕回原格
            if (wp == infantry.grid) break;
            int dist = Mathf.Abs(wp.GridIndex.X - prev.GridIndex.X) + Mathf.Abs(wp.GridIndex.Y - prev.GridIndex.Y);
            if (dist != 1) break;   // 断链（快速滑动跳格）
            int c = GetActualMoveCost(infantry, wp);
            // 路上有单位即截断（可见判定——隐形的存在不截断、不泄漏，雾战同标准）
            var blockerU = wp.infantries.FirstOrDefault(u => u != null && u != infantry && Infantry.IsVisibleTo(u, infantry.team, grids));
            if (blockerU != null)
            {
                pendingEncounterBlocker = blockerU;   // ✅ 记录截断者，落定后结算遭遇战
                break;
            }
            if (spent + c > budget) break;
            spent += c;
            valid.Add(wp);
            prev = wp;
        }
        if (valid.Count == 0) return false;
        var dest = valid[valid.Count - 1];
        if (dest == infantry.grid) return false;
        CloseRange();   // ✅ 手画路径直接执行，须自清范围（点按流由回调收拾；不清=全局输入卡死）
        MoveTo(infantry, dest, showMenu: true, forcedPath: valid);
        // ✅ 截断遭遇结算：落定后按双方Export仲裁（总开关=是否攻击）
        var bk = pendingEncounterBlocker; pendingEncounterBlocker = null;
        if (bk != null)
        {
            Infantry.ResolveTruncationEncounter(infantry, bk);   // ✅ 回合中回合由入口判定人类/AI
            // ✅ 非强制截断：新格重绑+剩余实际移动力+二次移动
            if (!infantry.truncForcedStop && infantry.movePoints > 0 && infantry.health > 0)
            {
                infantry.state = UnitState.Idle;
                infantry.isMoved = false;
                infantry.isAttacked = false;
                infantry.hasActed = false;
                infantry.SetWaitVisual(false);
                ShowMoveRange(infantry);
            }
        }
        return true;
    }
}
