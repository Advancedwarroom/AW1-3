// Flare.cs
using Godot;

public partial class Flare : Infantry
{
    protected override string GetIdleAnimName() => team == "Player2" ? "Flare2" : "Flare1";
}
