// KamikazeBoat.cs - 突击舰（DoR 战列舰：25000 / 移动5 / 视野3 / 油99 / 弹6 / 射程3–5 / 可移动后攻击）
// ✅ 数值全部在 .tscn 检查器中配置（检查器优先，不在 _Ready 硬编码）。
using Godot;

public partial class KamikazeBoat : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "KamikazeBoat2" : "KamikazeBoat1";

    public override void _Ready()
    {
        base._Ready();
    }
}
