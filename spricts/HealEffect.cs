// BlackCannon.cs - 黑炮：三角形范围，固定百分比伤害，支持自定义目标选择和改良伤害模式
// ✅ 新增：完整弹药系统 + 多回合冷却系统（还原原版高级战争原型炮/完全体差异）
using Godot;
using System.Collections.Generic;
using System.Linq;

// 由 BlackCannon.cs 拆出的共享类型

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
