using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// A construction ghost sits on this marker. Ghosts live only on the client, so the client reports
/// them and the tracker just reads the flag. With <see cref="Stray"/> it is the nearby miss instead,
/// with <see cref="Wrong"/> a ghost of another recipe on the marker.
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

    /// <summary>
    /// Looks for a ghost of any recipe other than <see cref="Recipe"/> on the marker's tile.
    /// While the step watches for one, the client also refuses to start building such a ghost.
    /// </summary>
    [DataField]
    public bool Wrong;

    [DataField]
    public float StrayRange = 1.5f;
}
