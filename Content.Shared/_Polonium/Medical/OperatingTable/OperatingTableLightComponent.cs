using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Medical.OperatingTable;

/// <summary>
/// An operating table with a toggleable overhead light and a vitals monitor for the patient strapped to it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(OperatingTableLightSystem))]
public sealed partial class OperatingTableLightComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool LightOn;

    [DataField]
    public SoundSpecifier ToggleSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");

    /// <summary>
    /// Fraction of health, between 0 and 1, above which the patient is shown as healthy.
    /// </summary>
    [DataField]
    public float HealthyThreshold = 0.85f;

    /// <summary>
    /// Fraction of health, between 0 and 1, above which the patient is shown as injured rather than critical.
    /// </summary>
    [DataField]
    public float InjuredThreshold = 0.5f;

    [DataField]
    public SoundSpecifier? HeartbeatHealthySound = new SoundPathSpecifier("/Audio/_Polonium/Medical/heartbeat_monitor.ogg")
    {
        Params = AudioParams.Default.WithLoop(true).WithVolume(-5f),
    };

    [DataField]
    public SoundSpecifier? HeartbeatInjuredSound = new SoundPathSpecifier("/Audio/_Polonium/Medical/heartbeat_monitor_injured.ogg")
    {
        Params = AudioParams.Default.WithLoop(true).WithVolume(-5f),
    };

    [DataField]
    public SoundSpecifier? HeartbeatCriticalSound = new SoundPathSpecifier("/Audio/_Polonium/Medical/heartbeat_monitor_critical.ogg")
    {
        Params = AudioParams.Default.WithLoop(true).WithVolume(-5f),
    };

    [DataField]
    public SoundSpecifier? FlatlineSound = new SoundPathSpecifier("/Audio/_Polonium/Medical/flatline.ogg")
    {
        Params = AudioParams.Default.WithLoop(true).WithVolume(-3f),
    };

    /// <summary>
    /// The looping sound that is playing now.
    /// </summary>
    public EntityUid? MonitorStream;

    /// <summary>
    /// The state <see cref="MonitorStream"/> was started for, to restart it only when the state changes.
    /// </summary>
    public VitalsState SoundState;
}
