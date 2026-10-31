// Gunboat.cs - 炮艇（DoR 原作跨作适配：5000 / 移动7 / 视野5 / 油99 / 弹1 / 载1步兵系）
// ✅ 跨作智能适配：DoR 数值体系折算为本作原创数值（见 .tscn 检查器）。
using Godot;

public partial class Gunboat : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "Gunboat2" : "Gunboat1";

    public override void _Ready()
    {
        base._Ready();
    }
}
