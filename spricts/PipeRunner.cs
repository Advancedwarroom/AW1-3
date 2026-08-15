// PipeRunner.cs
using Godot;

public partial class PipeRunner : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "PipeRunner2" : "PipeRunner1";
}
