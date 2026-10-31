// Neotank.cs - 新型坦克（AW2/DS 原作：22000 / 移动6 / 视野1 / 油99 / 主炮弹9 + 机枪）
// ✅ 数值全部在 .tscn 检查器中配置（检查器优先，不在 _Ready 硬编码）。
using Godot;

public partial class Neotank : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "Neotank2" : "Neotank1";

    public override void _Ready()
    {
        base._Ready();
    }
}
