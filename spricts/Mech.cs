// Mech.cs
using Godot;

public partial class Mech : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "mech2" : "mech1";

    public override bool IsArmoredTarget(Infantry target)
    {
        if (target.isArmoredUnit) return true;
        if (target is Mech) return false;
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm != null && gm.unitCategories.TryGetValue(target, out var category))
            return category == UnitCategory.Tank || category == UnitCategory.Vehicle;
        return false;
    }
}
