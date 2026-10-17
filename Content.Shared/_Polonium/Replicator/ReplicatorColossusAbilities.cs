using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Replicator;

[RegisterComponent]
public sealed partial class ReplicatorColossusPulseComponent : Component
{
    [DataField]
    public float Radius = 4.5f;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2.5);

    [DataField]
    public FixedPoint2 DamageThreshold = 40;

    /// <summary>
    /// How many rounds of the shared minigun pool the pulse burns.
    /// </summary>
    [DataField]
    public int AmmoDrain;

    [DataField]
    public EntProtoId? ChargeEffect;

    [DataField]
    public EntProtoId? Effect;

    [DataField]
    public EntProtoId? ArrivalEffect;

    [DataField]
    public SoundSpecifier? ChargeSound;

    [DataField]
    public SoundSpecifier? Sound;

    [ViewVariables]
    public EntityUid? ChargeEffectEntity;
}

[RegisterComponent]
public sealed partial class ReplicatorColossusSweepComponent : Component
{
    [DataField]
    public float Radius = 6.5f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// How many rounds of the shared minigun pool the sweep burns.
    /// </summary>
    [DataField]
    public int AmmoDrain;

    [DataField(required: true)]
    public EntProtoId Wall;

    [DataField(required: true)]
    public EntProtoId ReinforcedWall;

    [DataField]
    public float ReinforcedThreshold = 300;

    [DataField(required: true)]
    public ProtoId<ContentTileDefinition> Tile;

    [DataField]
    public EntProtoId? Effect;

    [DataField]
    public EntProtoId? ConvertEffect;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public SoundSpecifier? ConvertSound;

    [DataField]
    public TimeSpan ConvertSoundInterval = TimeSpan.FromSeconds(0.15);

    [ViewVariables]
    public TimeSpan NextConvertSound;

    [ViewVariables]
    public List<ReplicatorColossusPendingConversion> Pending = new();
}

public record struct ReplicatorColossusPendingConversion(TimeSpan Time, EntityUid Grid, Vector2i Indices);

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ReplicatorColossusSweepEffectComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartTime;

    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1.5);

    [DataField, AutoNetworkedField]
    public Angle StartAngle;
}

public sealed partial class ReplicatorColossusPulseActionEvent : InstantActionEvent;

public sealed partial class ReplicatorColossusSweepActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class ReplicatorColossusPulseDoAfterEvent : SimpleDoAfterEvent;
