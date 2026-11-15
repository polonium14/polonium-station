using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Polonium.Body;

/// <summary>
/// Marks a body that has no working heart. Oxygen stops reaching it, so it passes out and suffocates
/// no matter what it breathes, and it cannot be defibrillated.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CirculationSystem))]
public sealed partial class StoppedCirculationComponent : Component
{
    /// <summary>
    /// When the body's oxygen is drained next.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>
    /// The most oxygen saturation the body can hold right now. It sinks until there is none left.
    /// </summary>
    [DataField]
    public float SaturationCeiling;
}

/// <summary>
/// Marks a body that <see cref="CirculationSystem"/> made unrevivable because a vital organ is missing,
/// so it only lifts what it added itself and never an unrevivable trait.
/// </summary>
[RegisterComponent]
[Access(typeof(CirculationSystem))]
public sealed partial class MissingVitalOrganComponent : Component;
