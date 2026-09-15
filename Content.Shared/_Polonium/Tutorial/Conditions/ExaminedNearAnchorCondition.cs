using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// The trainee examined something of these prototypes standing at the anchor. For construction, where
/// every stage is a brand new entity that can never carry an anchor of its own.
/// </summary>
public sealed partial class ExaminedNearAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField(required: true)]
    public List<EntProtoId> Prototypes = new();

    [DataField]
    public float Range = 0.5f;
}
