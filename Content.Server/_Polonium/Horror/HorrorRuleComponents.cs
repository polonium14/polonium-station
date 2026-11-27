using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Horror;

/// <summary>
/// Objective counting souls the horror devoured, works together with a counter condition.
/// </summary>
[RegisterComponent, Access(typeof(EldritchHorrorSystem))]
public sealed partial class HorrorDevourConditionComponent : Component;

/// <summary>
/// Game rule that spawns its antagonist at a random vent on the station, like vent critters.
/// </summary>
[RegisterComponent, Access(typeof(HorrorSpawnSystem))]
public sealed partial class HorrorVentSpawnComponent : Component
{
    /// <summary>
    /// Location that was picked.
    /// </summary>
    [DataField]
    public Robust.Shared.Map.EntityCoordinates? Coords;
}

/// <summary>
/// The smuggled pet carrier with a sleeping horror. Whoever holds it when the horror wakes becomes its master.
/// </summary>
[RegisterComponent, Access(typeof(HorrorSpawnSystem))]
public sealed partial class HorrorCarrierComponent : Component
{
    /// <summary>
    /// Objectives given to the woken horror: serving its master and surviving, like in Aquila.
    /// </summary>
    [DataField]
    public List<Robust.Shared.Prototypes.EntProtoId> Objectives = new() { "HorrorServeObjective", "HorrorSurviveObjective" };

    /// <summary>
    /// Added when someone tries to wake the creature: the ghost role and its spawner.
    /// </summary>
    [DataField(required: true)]
    public ComponentRegistry WakeComponents = new();

    /// <summary>
    /// Someone already tried to wake the creature.
    /// </summary>
    [DataField]
    public bool Woken;
}
