// Artillery.cs
using Godot;

public partial class Artillery : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "artillery2" : "artillery1";
}
