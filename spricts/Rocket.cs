// Rocket.cs
using Godot;

public partial class Rocket : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "rocket2" : "rocket1";
}
