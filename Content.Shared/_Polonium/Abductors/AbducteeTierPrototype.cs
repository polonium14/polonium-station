using Content.Shared.Roles.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Abductors;

/// <summary>
/// How much trouble a returned specimen is meant to cause, with the objectives of that kind.
/// </summary>
[Prototype]
public sealed partial class AbducteeTierPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public float Weight;

    [DataField(required: true)]
    public EntProtoId<MindRoleComponent> MindRole;

    [DataField(required: true)]
    public LocId Briefing;

    [DataField(required: true)]
    public List<AbducteeObjectiveEntry> Objectives = new();
}

[DataDefinition]
public sealed partial class AbducteeObjectiveEntry
{
    [DataField(required: true)]
    public LocId Title;

    [DataField(required: true)]
    public LocId Description;
}
