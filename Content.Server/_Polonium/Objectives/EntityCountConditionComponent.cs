using Content.Shared.Whitelist;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Polonium.Objectives;

/// <summary>
/// Counts entities with a component, e.g. animated objects.
/// The goal comes from NumberObjective.
/// </summary>
[RegisterComponent, Access(typeof(ObjectiveConditionsSystem))]
public sealed partial class EntityCountConditionComponent : Component
{
    [DataField(required: true, customTypeSerializer: typeof(ComponentNameSerializer))]
    public string Component = string.Empty;

    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Skip entities off station grids, so nothing made in an antag base counts.
    /// </summary>
    [DataField]
    public bool StationOnly = true;
}
