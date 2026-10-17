using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// A construction ghost sits on this marker. Ghosts live only on the client, so the client reports
/// them and the tracker just reads the flag. With <see cref="Stray"/> it is the nearby miss instead.
/// </summary>
public sealed partial class ConstructionGhostOnAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    /// <summary>Which recipe the outline has to be. Empty means any ghost counts.</summary>
    [DataField]
    public ProtoId<ConstructionPrototype>? Recipe;

    [DataField]
    public bool Stray;

    [DataField]
    public float StrayRange = 1.5f;
}
