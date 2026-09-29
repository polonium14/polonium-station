using Content.Shared.AlertLevel;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Objectives;

/// <summary>
/// Done once the station switches to one of the levels after the objective was given.
/// </summary>
[RegisterComponent, Access(typeof(ObjectiveConditionsSystem))]
public sealed partial class AlertLevelReachedConditionComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<AlertLevelPrototype>> Levels = new();

    [DataField]
    public bool Reached;
}
