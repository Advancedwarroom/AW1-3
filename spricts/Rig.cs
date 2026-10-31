// Rig.cs - 工程车（DoR 原作：5000 / 移动6 / 视野1 / 油99 / 载1步兵系 / 补给 / 搭建临时机场·临时港口）
// ✅ 数值全部在 .tscn 检查器中配置（检查器优先，不在 _Ready 硬编码）。
using Godot;

public partial class Rig : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "Rig2" : "Rig1";

    public override void _Ready()
    {
        base._Ready();
    }
}
