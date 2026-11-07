using Content.Shared._Polonium.Objectives;

namespace Content.Server._Polonium.Objectives;

/// <summary>
/// An objective written by an admin whose progress is set by hand.
/// </summary>
[RegisterComponent, Access(typeof(CustomObjectiveSystem))]
public sealed partial class CustomObjectiveConditionComponent : Component
{
    [DataField]
    public CustomObjectiveStatus Status = CustomObjectiveStatus.Failed;
}
