// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.BluespaceStrike;

/// <summary>
/// Client-side falling projectile visual for an incoming bluespace strike.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BluespaceStrikeIncomingComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan FallDuration = TimeSpan.FromSeconds(BluespaceStrikeComponent.FallDurationSeconds);
 
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ImpactAt;

    /// <summary>
    /// How far above the impact point the sprite starts in tiles
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StartOffsetY = 14f;
}
