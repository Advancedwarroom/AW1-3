// Duster.cs - 螺旋桨机（DoR 原作：13000 / 移动8 / 视野4 / 油99 / 机枪弹9）
// ✅ 数值全部在 .tscn 检查器中配置（检查器优先，不在 _Ready 硬编码）。
using Godot;

public partial class Duster : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "Duster2" : "Duster1";

    public override void _Ready()
    {
        base._Ready();
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
