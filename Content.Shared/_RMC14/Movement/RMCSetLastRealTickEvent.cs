// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Movement;

[Serializable, NetSerializable]
public sealed class RMCSetLastRealTickEvent(GameTick tick) : EntityEventArgs
{
    public readonly GameTick Tick = tick;
}
