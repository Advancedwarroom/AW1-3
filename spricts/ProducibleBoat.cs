// ProducibleBoat.cs - 生产舰（DoR 航母：28000 / 移动5 / 视野4 / 油99 / 防空炮射程1 / 载2空军 / 舰载起飞 / 生产水上飞机）
// ✅ 数值全部在 .tscn 检查器中配置（检查器优先，不在 _Ready 硬编码）。
using Godot;

public partial class ProducibleBoat : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "ProducibleBoat2" : "ProducibleBoat1";

    public override void _Ready()
    {
        base._Ready();
    }
}
