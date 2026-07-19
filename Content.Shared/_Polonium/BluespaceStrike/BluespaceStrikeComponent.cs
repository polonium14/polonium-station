// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.BluespaceStrike;

/// <summary>
/// Tracks a scheduled bluespace artillery strike until detonation.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BluespaceStrikeComponent : Component
{
    public const float MinDelaySeconds = 1f;
    public const float MaxDelaySeconds = 300f;
    public const float DefaultSlope = 5f;
    public const float DefaultMaxIntensity = 100f;
    public const float FallDurationSeconds = 0.9f;

    public const string ExplosionType = "BluespaceArtillery";
    public static readonly EntProtoId ControllerPrototype = "BluespaceStrikeController";
    public static readonly EntProtoId MarkerPrototype = "BluespaceStrikeMarker";
    public static readonly EntProtoId IncomingPrototype = "BluespaceStrikeIncoming";

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan DetonateAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan SpawnFallingAt;

    [DataField, AutoNetworkedField]
    public float Radius;

    [DataField, AutoNetworkedField]
    public float TotalIntensity;

    [DataField, AutoNetworkedField]
    public float IntensitySlope = DefaultSlope;

    [DataField, AutoNetworkedField]
    public float MaxIntensity = DefaultMaxIntensity;

    [ViewVariables]
    public MapCoordinates Epicenter;

    [DataField]
    public List<EntityUid> Markers = new();

    [DataField]
    public EntityUid? AudioStream;

    [DataField]
    public EntityUid? IncomingVisual;

    [DataField]
    public bool FallingSpawned;

    [DataField]
    public SoundSpecifier AirRaidSound = new SoundPathSpecifier("/Audio/_Polonium/Effects/ABS/airraid.ogg");
}
