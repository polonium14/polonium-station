// SPDX-FileCopyrightText: 2026 coderabbitai[bot] <136622811+coderabbitai[bot]@users.noreply.github.com>
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

    /// <summary>
    /// Creates a request to open the signature-placement interface for a paper and pen.
    /// </summary>
    /// <param name="paper">The network entity representing the paper.</param>
    /// <param name="pen">The network entity representing the pen.</param>
    public PaperSignRequestEvent(NetEntity paper, NetEntity pen)
    {
        Paper = paper;
        Pen = pen;
    }
}
