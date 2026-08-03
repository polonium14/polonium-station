// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Paper;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Paper;

/// <summary>
/// Raised on the pen when trying to sign a paper.
/// If it's cancelled the signature isn't made.
/// </summary>
[ByRefEvent]
public record struct SignAttemptEvent(Entity<PaperComponent> Paper, EntityUid User, EntityUid Pen, bool Cancelled = false);

/// <summary>
/// Sent from the server to a single client to tell it to open the signature
/// placement UI for the given paper, letting the player position and scale
/// their signature before committing it.
/// </summary>
[Serializable, NetSerializable]
public sealed class PaperSignRequestEvent : EntityEventArgs
{
    public readonly NetEntity Paper;
    public readonly NetEntity Pen;

    public PaperSignRequestEvent(NetEntity paper, NetEntity pen)
    {
        Paper = paper;
        Pen = pen;
    }
}
