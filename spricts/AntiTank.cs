// AntiTank.cs
using Godot;

public partial class AntiTank : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "AntiTank2" : "AntiTank1";
}
