// Artillery.cs
using Godot;

public partial class Artillery : Infantry
{
    protected override string GetIdleAnimName() => team == "Player2" ? "artillery2" : "artillery1";
}
