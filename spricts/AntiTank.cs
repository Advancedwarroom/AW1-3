// AntiTank.cs
using Godot;

public partial class AntiTank : Infantry
{
    protected override string GetIdleAnimName() => team == "Player2" ? "AntiTank2" : "AntiTank1";
}
