// SPDX-FileCopyrightText: 2025 Steve <marlumpy@gmail.com>
// SPDX-FileCopyrightText: 2025 marc-pelletier <113944176+marc-pelletier@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <maciej.walendziuk@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.RPD;

/// <summary>
/// Marks an RCD as a rapid pipe dispenser. Sits alongside RCDComponent, which still drives the
/// actual construction; this component carries everything that is RPD-only.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(RPDSystem))]
public sealed partial class RPDComponent : Component
{
    /// <summary>
    /// The pipe colour picked in the RPD menu, applied to anything the RPD builds.
    /// "default" leaves the entity's own colour alone.
    /// </summary>
    [DataField, AutoNetworkedField]
    public (string Key, Color? Color) PipeColor { get; set; } = ("default", null);

    /// <summary>
    /// Last eye rotation reported by the client holding this RPD.
    /// </summary>
    /// <remarks>
    /// Eye rotation is not networked, but the server needs it to work out which pipe layer the
    /// player aimed at. The client pushes it here via <see cref="RPDEyeRotationEvent"/> whenever it
    /// changes. Not a permanent solution.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public float? LastKnownEyeRotation { get; set; } = null;
}
