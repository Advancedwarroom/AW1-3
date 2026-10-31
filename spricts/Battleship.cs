// Battleship.cs - 战列舰（AW3 原作值：28000 / 移动5 / 视野2 / 射程2-6 / 弹9 / 油99）
using Godot;

public partial class Battleship : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "Battleship2" : "Battleship1";

    public override void _Ready()
    {
        base._Ready();
    }
}
