using Content.Shared.FixedPoint;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Medical.Dialysis;

/// <summary>
/// Filters the blood of the patient its <see cref="IV.IVDripComponent"/> is attached to: removes every chemical
/// but medicine, and takes over for failing kidneys and in part for a failing liver.
/// An emag turns it around, so it strips medicine and leaves everything else.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class DialysisComponent : Component
{
    /// <summary>
    /// Units of chemicals removed from the blood per second.
    /// </summary>
    [DataField]
    public FixedPoint2 RemovePerSecond = 2;

    /// <summary>
    /// Reagent group that is kept in the blood.
    /// </summary>
    [DataField]
    public string KeptGroup = "Medicine";

    /// <summary>
    /// Share of the poison dead or failing kidneys still deal to a patient on dialysis.
    /// </summary>
    [DataField]
    public float KidneyPoisonFactor;

    /// <summary>
    /// Share of the poison a dead or failing liver still deals to a patient on dialysis.
    /// </summary>
    [DataField]
    public float LiverPoisonFactor = 1f / 3f;

    /// <summary>
    /// The patient the machine is working on, kept to notice when it changes.
    /// </summary>
    [ViewVariables]
    public EntityUid? Patient;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;
}
