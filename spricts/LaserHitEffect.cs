// Laser.cs - 高级激光炮系统（修复版）
// 修复内容：
// 1. 多次攻击同步 remainingAttacks 和 attacksRemainingInCycle
// 2. 添加局内角度配置API（EnableAngle/DisableAngle/SetFireAngles等）
// 3. ✅ 移除 attackedThisRound 限制：允许同一回合多次攻击同一目标
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class LaserHitEffect : Node2D
{
    private float _t = 0f;
    private const float DURATION = 0.4f;

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (_t >= DURATION) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float a = 1f - _t / DURATION;
        float r = 5f + _t * 30f;
        DrawCircle(Vector2.Zero, r, new Color(1, 0.8f, 0.2f, a * 0.8f));
        DrawCircle(Vector2.Zero, r * 0.5f, new Color(1, 1, 0.8f, a));
    }

}
