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
/// Set by the body debug window: the circulation stays stopped even though the heart works, until the window clears it.
/// </summary>
[RegisterComponent]
public sealed partial class CirculationArrestComponent : Component;

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
/// The last breath cycle of a body's oxygen saturation, for the health analyzer. One breath swings the saturation
/// through most of its range, so a single value says little about the patient.
/// </summary>
[RegisterComponent, UnsavedComponent]
[Access(typeof(CirculationSystem))]
public sealed partial class SpO2ReadingComponent : Component
{
    /// <summary>
    /// Samples kept: two for each of the four respirator updates a full breath takes.
    /// </summary>
    public const int Window = 8;

    public readonly float[] Samples = new float[Window];

    /// <summary>
    /// How many of <see cref="Samples"/> are filled.
    /// </summary>
    public int Count;

    /// <summary>
    /// Where the next sample goes.
    /// </summary>
    public int Next;

    public TimeSpan NextSample;
}

/// <summary>
/// Marks a body that <see cref="CirculationSystem"/> made unrevivable because a vital organ is missing,
/// so it only lifts what it added itself and never an unrevivable trait.
/// </summary>
[RegisterComponent]
[Access(typeof(CirculationSystem))]
public sealed partial class MissingVitalOrganComponent : Component;
