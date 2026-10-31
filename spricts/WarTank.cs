// WarTank.cs - 弩级坦克（DoR 原作 16000 / 移动4 / 视野2 / 油50 / 主炮弹5 + 机枪；跨作适配：造价22000，伤害介于重型坦克与巨型坦克之间）
// ✅ 数值全部在 .tscn 检查器中配置（检查器优先，不在 _Ready 硬编码）。
using Godot;

public partial class WarTank : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "WarTank2" : "WarTank1";

    public override void _Ready()
    {
        base._Ready();
    }
}
