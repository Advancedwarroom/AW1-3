// Rocket.cs
using Godot;

public partial class Rocket : Infantry
{
    protected override string GetIdleAnimName() => team == "Player2" ? "rocket2" : "rocket1";
}
