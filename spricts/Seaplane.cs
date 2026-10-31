// Seaplane.cs - 水上飞机（DoR 原作：15000 / 移动7 / 视野4 / 油40 / 导弹3；只能由生产舰生产）
// ✅ 数值全部在 .tscn 检查器中配置（检查器优先，不在 _Ready 硬编码）。
using Godot;

public partial class Seaplane : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "Seaplane2" : "Seaplane1";

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
