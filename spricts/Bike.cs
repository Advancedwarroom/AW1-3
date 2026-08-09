// Bike.cs
using Godot;

public partial class Bike : Infantry
{
    protected override string GetIdleAnimName() => team == "Player2" ? "Bike2" : "Bike1";
}
