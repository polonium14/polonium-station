using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Power.Generation.FissionGenerator;

/// <summary>
/// Client-side glow and drone of a nuclear reactor close to a meltdown, the glow after it,
/// and the fading of the smoke column above its ruins.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ReactorMeltdownVisualsComponent : Component
{
    /// <summary>
    /// How close the reactor is to a meltdown, from 0 to 1.
    /// </summary>
    [AutoNetworkedField]
    public float Danger;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? MeltdownTime;

    [DataField, AutoNetworkedField]
    public EntityUid? SmokeColumn;

    [DataField]
    public Color GlowColor = Color.FromHex("#ff7a2e");

    [DataField]
    public float BuildUpGlowEnergy = 3f;

    [DataField]
    public float BuildUpMinGlowRadius = 3f;

    [DataField]
    public float BuildUpMaxGlowRadius = 6f;

    /// <summary>
    /// Pulses per second of the glow at the start and at the end of the build-up.
    /// </summary>
    [DataField]
    public Vector2 BuildUpPulseFrequency = new(0.4f, 2.5f);

    /// <summary>
    /// Looping drone around the reactor during the build-up. It grows from silence to its own volume with the danger.
    /// </summary>
    [DataField]
    public SoundSpecifier? BuildUpSound = new SoundPathSpecifier("/Audio/_Polonium/Machines/Reactor/reactor_strain.ogg");

    [DataField]
    public float MeltdownGlowEnergy = 5f;

    [DataField]
    public float MeltdownGlowRadius = 9f;

    [DataField]
    public float ResidualGlowEnergy = 1.2f;

    [DataField]
    public float ResidualGlowRadius = 4f;

    /// <summary>
    /// Time for the glow and the smoke column to fade most of the way down to their residual level.
    /// </summary>
    [DataField]
    public TimeSpan GlowFadeTime = TimeSpan.FromMinutes(4);

    /// <summary>
    /// How much the residual glow flickers, as a fraction of its energy.
    /// </summary>
    [DataField]
    public float GlowFlicker = 0.2f;

    [DataField]
    public float ResidualSmokeAlpha = 0.45f;

    [ViewVariables]
    public EntityUid? LightEntity;

    [ViewVariables]
    public EntityUid? BuildUpStream;

    [ViewVariables]
    public float ShownDanger;

    [ViewVariables]
    public float PulsePhase;
}
