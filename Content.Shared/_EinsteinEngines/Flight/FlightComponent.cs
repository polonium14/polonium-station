using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared._EinsteinEngines.Flight;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(SharedFlightSystem))]
public sealed partial class FlightComponent : Component
{
    [DataField]
    public EntProtoId ToggleAction = "ActionToggleFlight";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

    [DataField, AutoNetworkedField]
    public bool On;

    /// <summary>
    /// Stamina damage taken per second while flying.
    /// </summary>
    [DataField]
    public float StaminaDrainRate = 6f;

    [DataField]
    public TimeSpan ActivationDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Replaces the weightless speed modifier while flying, relative to the base walk and sprint speed.
    /// </summary>
    [DataField]
    public float SpeedModifier = 2f;

    [DataField]
    public SoundSpecifier FlapSound = new SoundCollectionSpecifier("WingFlaps");

    /// <summary>
    /// Time between wing flaps. Stamina is drained once per flap.
    /// </summary>
    [DataField]
    public TimeSpan FlapInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextFlap;

    /// <summary>
    /// Sprite layers using this RSI are animated while flying.
    /// </summary>
    [DataField]
    public ResPath? AnimatedRsi;

    [DataField]
    public float ShaderSpeed = 6f;

    [DataField]
    public float ShaderMultiplier = 0.01f;

    [DataField]
    public float ShaderOffset = 0.25f;
}
