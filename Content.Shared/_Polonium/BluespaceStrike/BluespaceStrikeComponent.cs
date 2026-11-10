// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.BluespaceStrike;

/// <summary>
/// A bluespace artillery shell that falls onto its position and explodes at <see cref="DetonateAt"/>.
/// The explosion itself is configured by the <c>Explosive</c> component on the same entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BluespaceStrikeComponent : Component
{
    public const float MinDelaySeconds = 1f;
    public const float MaxDelaySeconds = 30f;
    public const float MaxRadius = 100f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan DetonateAt;

    /// <summary>
    /// How long before <see cref="DetonateAt"/> the shell becomes visible and starts falling.
    /// </summary>
    [DataField]
    public TimeSpan FallDuration = TimeSpan.FromSeconds(0.9);

    /// <summary>
    /// How far above the impact point the shell starts falling, in tiles.
    /// </summary>
    [DataField]
    public float StartOffsetY = 14f;

    /// <summary>
    /// Radius of the full-strength zone in tiles, chosen by the admin.
    /// </summary>
    [DataField]
    public float Radius;

    [DataField]
    public bool Falling;

    [DataField]
    public EntProtoId MarkerPrototype = "BluespaceStrikeMarker";

    [DataField]
    public SoundSpecifier AirRaidSound = new SoundPathSpecifier("/Audio/_Polonium/Effects/ABS/airraid.ogg");

    [DataField]
    public List<EntityUid> Markers = new();
}
