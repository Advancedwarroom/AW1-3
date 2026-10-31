// Cruiser.cs - 巡洋舰（AW3 原作值：18000 / 移动6 / 视野3 / 弹9 / 油99；可载2架直升机并补给）
using Godot;

public partial class Cruiser : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "Cruiser2" : "Cruiser1";

    public override void _Ready()
    {
        base._Ready();
    }
}
