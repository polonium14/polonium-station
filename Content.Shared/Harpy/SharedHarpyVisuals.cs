// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared.Harpy;

[Serializable, NetSerializable]
public enum HarpyVisualLayers : byte
{
    Singing,
}

[Serializable, NetSerializable]
public enum SingingVisualLayer : byte
{
    False,
    True,
}
