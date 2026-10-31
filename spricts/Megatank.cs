// Megatank.cs - 巨型坦克（DS 原作：28000 / 移动4 / 视野1 / 油50 / 主炮弹3 + 机枪）
// ✅ 数值全部在 .tscn 检查器中配置（检查器优先，不在 _Ready 硬编码）。
using Godot;

public partial class Megatank : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "Megatank2" : "Megatank1";

    public override void _Ready()
    {
        base._Ready();
    }
}
