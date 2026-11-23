using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Polonium.Abductors;

/// <summary>
/// Now and then puts a line from a dataset in place of what the carrier is trying to say.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(AbductorSpeechSwapSystem))]
public sealed partial class AbductorSpeechSwapComponent : Component
{
    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> Lines;

    [DataField]
    public TimeSpan MinInterval = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan MaxInterval = TimeSpan.FromMinutes(6);

    /// <summary>
    /// Chance that the first message after the interval gets replaced. Either way the next interval starts.
    /// </summary>
    [DataField]
    public float Chance = 0.5f;

    [DataField]
    public LocId Popup = "abductor-gland-popup-outburst";

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextRoll;
}
