// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Vector2 = System.Numerics.Vector2;

namespace Content.Shared.Explosion;

/// <summary>
/// Broadcast by the server when an explosion occurs so clients can render a screen-space distortion shockwave ring synced to server time
/// </summary>
[Serializable, NetSerializable]
public sealed class ExplosionShockwaveEvent : EntityEventArgs
{
    public MapId MapId { get; }
    public Vector2 EpicenterWorld { get; }
    public double ServerStartSeconds { get; }
    public float MaxRadiusTiles { get; }
    public float DurationSeconds { get; }
    public float Intensity { get; }

    /// <summary>
    /// Whether to render a blinding white flash before the distortion ring
    /// </summary>
    public bool Flash { get; }

    public ExplosionShockwaveEvent(
        MapId mapId,
        Vector2 epicenterWorld,
        double serverStartSeconds,
        float maxRadiusTiles,
        float durationSeconds,
        float intensity,
        bool flash = false)
    {
        MapId = mapId;
        EpicenterWorld = epicenterWorld;
        ServerStartSeconds = serverStartSeconds;
        MaxRadiusTiles = maxRadiusTiles;
        DurationSeconds = durationSeconds;
        Intensity = intensity;
        Flash = flash;
    }
}
