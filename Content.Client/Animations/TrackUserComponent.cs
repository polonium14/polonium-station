// SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 github-actions[bot] <41898282+github-actions[bot]@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Client.Animations;

/// <summary>
/// Entities with this component tracks the user's world position every frame.
/// </summary>
[RegisterComponent]
public sealed partial class TrackUserComponent : Component
{
    public EntityUid? User;

    /// <summary>
    /// Offset in the direction of the entity's rotation.
    /// </summary>
    public Vector2 Offset = Vector2.Zero;

    /// <summary>
    /// Offset from the user's position.
    /// </summary>
    public Vector2 OriginOffset = Vector2.Zero;
}
