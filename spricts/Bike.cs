// Bike.cs
using Godot;

public partial class Bike : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "Bike2" : "Bike1";
}
