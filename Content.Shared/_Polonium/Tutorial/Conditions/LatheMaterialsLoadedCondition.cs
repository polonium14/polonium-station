using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>The lathe on this anchor holds some of every one of these materials.</summary>
public sealed partial class LatheMaterialsLoadedCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField(required: true)]
    public List<ProtoId<MaterialPrototype>> Materials = new();
}
