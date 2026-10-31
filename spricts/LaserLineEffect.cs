// Laser.cs - 高级激光炮系统（修复版）
// 修复内容：
// 1. 多次攻击同步 remainingAttacks 和 attacksRemainingInCycle
// 2. 添加局内角度配置API（EnableAngle/DisableAngle/SetFireAngles等）
// 3. ✅ 移除 attackedThisRound 限制：允许同一回合多次攻击同一目标
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class LaserLineEffect : Node2D
{
    private Vector2 _startPos;
    private Vector2 _endPos;
    private Color _laserColor;
    private float _t = 0f;
    private const float DURATION = 0.5f;

    public void Setup(Vector2 start, Vector2 end, Color color)
    {
        _startPos = start;
        _endPos = end;
        _laserColor = color;
        ZIndex = 500;
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (_t >= DURATION) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float a = 1f - _t / DURATION;
        float width = 4f * (1f - _t / DURATION * 0.5f);
        DrawLine(_startPos - GlobalPosition, _endPos - GlobalPosition,
                new Color(_laserColor.R, _laserColor.G, _laserColor.B, a), width);
    }
}
