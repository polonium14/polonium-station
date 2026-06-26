// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.NPC.HTN;
using Content.Shared.NPC.Systems;

namespace Content.Client.NPC.Systems;

public sealed class NPCSystem : SharedNPCSystem
{
    public override bool IsNpc(EntityUid uid)
    {
        return HasComp<HTNComponent>(uid);
    }
}
