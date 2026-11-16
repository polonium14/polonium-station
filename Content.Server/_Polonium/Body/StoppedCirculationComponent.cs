using Content.Shared.FixedPoint;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Polonium.Body;

/// <summary>
/// A body without a working spleen: it stops regenerating blood and slowly loses it.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CirculationSystem))]
public sealed partial class BloodDrainComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;
}

/// <summary>
/// Marks a body that has no working heart. It cannot be defibrillated.
/// </summary>
[RegisterComponent]
[Access(typeof(CirculationSystem))]
public sealed partial class StoppedCirculationComponent : Component;

/// <summary>
/// Limits how much oxygen reaches a body: a stopped or failing heart, or failing lungs.
/// Below the suffocation threshold the body passes out and suffocates, whatever it breathes.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CirculationSystem))]
public sealed partial class OxygenLimitComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>
    /// The most oxygen saturation the body can hold right now. Sinks while <see cref="Loss"/> is above zero.
    /// </summary>
    [DataField]
    public float Ceiling;

    /// <summary>
    /// Saturation lost per second.
    /// </summary>
    [DataField]
    public float Loss;

    /// <summary>
    /// Fraction of the maximum saturation the body can keep at most.
    /// </summary>
    [DataField]
    public float Cap = 1f;
}

/// <summary>
/// A body poisoned by a dead or missing liver or kidneys.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CirculationSystem))]
public sealed partial class OrganPoisoningComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>
    /// Poison damage per second.
    /// </summary>
    [DataField]
    public FixedPoint2 PerSecond;
}

/// <summary>
/// Running average of a patient's oxygen saturation, for the health analyzer.
/// </summary>
[RegisterComponent]
[Access(typeof(CirculationSystem))]
public sealed partial class SpO2ReadingComponent : Component
{
    [DataField]
    public float Average;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan LastRead;
}

/// <summary>
/// Marks a body that <see cref="CirculationSystem"/> made unrevivable because a vital organ is missing,
/// so it only lifts what it added itself and never an unrevivable trait.
/// </summary>
[RegisterComponent]
[Access(typeof(CirculationSystem))]
public sealed partial class MissingVitalOrganComponent : Component;
