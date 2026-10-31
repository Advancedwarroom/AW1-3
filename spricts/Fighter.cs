// FlyBomb.cs
using Godot;
using System;
using System.Linq;

public partial class Fighter : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "Fighter2" : "Fighter1";

    public override void _Ready()
    {
        base._Ready();
        // FlyBomb 的特殊初始化已通过 UnitRegistry 完成
    }

    public override void OnWaitSelected()
    {
        isMoved = true;
        isAttacked = true;
        state = UnitState.Acted;
        originalGrid = null;
        ShowIdleState();
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        gm?.ClearSelectedInfantry();
    }

}
