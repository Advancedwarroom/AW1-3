// LightTank.cs
using Godot;

public partial class LightTank : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "lighttank2" : "lighttank1";
}
