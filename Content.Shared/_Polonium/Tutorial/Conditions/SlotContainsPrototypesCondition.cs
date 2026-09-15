using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// Whatever is worn in the slot holds every listed prototype somewhere inside it. For things that
/// come out of a vendor and so never had an anchor, like tools in a belt.
/// </summary>
public sealed partial class SlotContainsPrototypesCondition : TutorialCondition
{
    [DataField(required: true)]
    public string Slot = string.Empty;

    [DataField(required: true)]
    public List<EntProtoId> Prototypes = new();
}
