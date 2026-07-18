// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._RMC14.Movement;
using Robust.Client.Timing;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Movement;

public sealed partial class RMCLagCompensationSystem : SharedRMCLagCompensationSystem
{
    [Dependency] private IClientGameTiming _timing = default!;

    public override GameTick GetLastRealTick(NetUserId? session)
    {
        return _timing.LastRealTick;
    }
}
