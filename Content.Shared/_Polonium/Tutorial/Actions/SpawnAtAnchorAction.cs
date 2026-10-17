using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Actions;

public sealed partial class SpawnAtAnchorAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public string? AssignAnchorId;

    [DataField]
    public bool TutorialNpc = true;

    [DataField]
    public bool PreventDeath = true;

    /// <summary>Drop it inside the anchor's storage instead of on its tile. Anchor has to be a closet or crate.</summary>
    [DataField]
    public bool IntoStorage;
}
