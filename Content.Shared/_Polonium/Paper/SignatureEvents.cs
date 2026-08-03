// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Paper;

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
