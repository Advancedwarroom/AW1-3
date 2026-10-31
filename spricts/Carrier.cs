// Carrier.cs - 航母（AW3 原作值：30000 / 移动5 / 视野4 / 弹9 / 射程3-8 对空 / 油99 / 载2空中单位）
// ✅ 内部补给：给舰载机补燃料/弹药（检查器 internalSupplyEnabled）。
using Godot;

public partial class Carrier : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "Carrier2" : "Carrier1";

    public override void _Ready()
    {
        base._Ready();
    }
}
