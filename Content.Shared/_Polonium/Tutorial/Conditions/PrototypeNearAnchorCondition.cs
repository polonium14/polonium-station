using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// Counts how many of a prototype sit near an anchor, or on the trainee. Lets a step ask for
/// "two slabs of meat" or "twelve cores on the markers" instead of a vague "something nearby".
/// </summary>
public sealed partial class PrototypeNearAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    /// <summary>Where to look. Unset means around the trainee.</summary>
    [DataField]
    public string? AnchorId;

    [DataField]
    public float Range = 6f;

    [DataField]
    public int Count = 1;

    /// <summary>
    /// Count only what is inside the anchor - a microwave's tray, a crate. Without this a bun held
    /// in hand answers "is the bun in the microwave" with yes.
    /// </summary>
    [DataField]
    public bool Inside;

    /// <summary>Skip hands and pockets, so "leave it on the table" means actually leaving it.</summary>
    [DataField]
    public bool IgnoreCarried;

    /// <summary>Only count things bolted to the floor, like a machine frame after the wrench.</summary>
    [DataField]
    public bool Anchored;
}
