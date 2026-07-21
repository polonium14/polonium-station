using Content.Shared.Body;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery.Conditions;

/// <summary>
/// Requires that an organ of a given category is (not) present on the targeted body.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryOrganConditionComponent : Component
{
    [DataField(required: true)]
    public ProtoId<OrganCategoryPrototype> Category = default!;

    [DataField]
    public bool Inverse;

    [DataField]
    public bool Reattaching;
}
