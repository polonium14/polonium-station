// SPDX-FileCopyrightText: 2026 coderabbitai[bot] <136622811+coderabbitai[bot]@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared.Paper;

/// <summary>
///     Sent from the server to a single client to tell it to open the stamp
///     placement UI for the given paper, letting the player position and rotate
///     the stamp before committing it. Mirrors the signature placement flow, but
///     stamps place at their natural size (no scale).
/// </summary>
[Serializable, NetSerializable]
public sealed class PaperStampRequestEvent : EntityEventArgs
{
    public readonly NetEntity Paper;
    public readonly NetEntity Stamp;

    /// <summary>
    /// Creates an event requesting stamp placement on a paper entity.
    /// </summary>
    /// <param name="paper">The paper entity receiving the stamp.</param>
    /// <param name="stamp">The stamp entity to place.</param>
    public PaperStampRequestEvent(NetEntity paper, NetEntity stamp)
    {
        Paper = paper;
        Stamp = stamp;
    }
}
