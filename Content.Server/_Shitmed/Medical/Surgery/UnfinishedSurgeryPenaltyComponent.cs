namespace Content.Server._Shitmed.Medical.Surgery;

/// <summary>
/// Tracks a mob currently being penalized for getting up (unbuckling from a bed/operating
/// table) with unfinished surgery still open on at least one organ - see
/// UnfinishedSurgeryPenaltySystem. Added on unbuckle, removed once every organ's surgery is
/// actually finished (all of SharedSurgerySystem.HasUnfinishedSurgerySteps' markers cleared).
/// </summary>
[RegisterComponent]
public sealed partial class UnfinishedSurgeryPenaltyComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextTick;
}
