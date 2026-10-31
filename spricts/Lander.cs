// Lander.cs - 登陆艇（AW3 原作值：12000 / 移动6 / 视野1 / 油99 / 载2地面单位）
// ✅ 卸载限制（原作）：只能在浅滩（BEACH）和港口装卸。
using Godot;
using System.Linq;

public partial class Lander : Infantry
{
    /// <summary>登陆艇装卸点：浅滩或港口（原作规则）</summary>
    public override bool CanUnloadToGrid(Grids targetGrid)
    {
        if (targetGrid == null) return false;
        if (targetGrid.gridType == GridType.BEACH) return true;
        if (targetGrid.city is Port) return true;
        return false;
    }

    public override string GetIdleAnimName() => team == "Player2" ? "Lander2" : "Lander1";

    public override void _Ready()
    {
        base._Ready();
    }
}
