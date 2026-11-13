using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Power.Generation.FissionGenerator;

/// <summary>
/// Molten reactor core that flows out of a melted reactor and slowly cools down.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class CoriumComponent : Component
{
    [DataField, AutoNetworkedField]
    public CoriumStage Stage = CoriumStage.Molten;

    [DataField]
    public Dictionary<CoriumStage, CoriumStageData> Stages = new();

    /// <summary>
    /// Played on a mob each time the corium burns it.
    /// </summary>
    [DataField]
    public SoundSpecifier? BurnSound;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextStage;
}

[DataDefinition]
public sealed partial class CoriumStageData
{
    /// <summary>
    /// How long the corium stays in this stage. The last stage lasts forever.
    /// </summary>
    [DataField]
    public TimeSpan Duration;

    /// <summary>
    /// Base of the smoothed sprite states. The glowing layer uses the same base with a <c>_glow</c> suffix.
    /// </summary>
    [DataField(required: true)]
    public string State = string.Empty;

    /// <summary>
    /// Radiation intensity of the corium tile.
    /// </summary>
    [DataField]
    public float Radiation;

    /// <summary>
    /// Energy of the corium's glow.
    /// </summary>
    [DataField]
    public float LightEnergy;

    /// <summary>
    /// Radius of the corium's glow.
    /// </summary>
    [DataField]
    public float LightRadius;

    /// <summary>
    /// Colour of the corium's glow.
    /// </summary>
    [DataField]
    public Color LightColor = Color.White;

    /// <summary>
    /// Damage dealt every second to mobs standing on the corium.
    /// </summary>
    [DataField]
    public DamageSpecifier? Damage;

    /// <summary>
    /// Whether the corium's ambient sound plays in this stage.
    /// </summary>
    [DataField]
    public bool Bubbling;

    /// <summary>
    /// Whether the corium takes damage and can be broken apart in this stage.
    /// </summary>
    [DataField]
    public bool Breakable;
}

[Serializable, NetSerializable]
public enum CoriumStage : byte
{
    Molten,
    Crust,
    Solid,
}
