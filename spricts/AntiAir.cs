// AntiAir.cs
using Godot;

public partial class AntiAir : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "antiair2" : "antiair1";
}
