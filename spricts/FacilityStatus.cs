using Godot;

[GlobalClass]
public partial class FacilityStatusData : Resource
{
    [Export] public FacilityStatusType Type { get; set; } = FacilityStatusType.None;
    [Export] public int RemainingTurns { get; set; } = -1;
    [Export] public string AppliedByTeam { get; set; } = "";
}