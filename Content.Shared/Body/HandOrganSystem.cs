// SPDX-FileCopyrightText: 2026 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Shared.Body;

public sealed partial class HandOrganSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandOrganComponent, OrganGotInsertedEvent>(OnGotInserted);
        SubscribeLocalEvent<HandOrganComponent, OrganGotRemovedEvent>(OnGotRemoved);
    }

    private void OnGotInserted(Entity<HandOrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        // AddHand/RemoveHand mutate the networked HandsComponent and hand containers. Doing
        // that while the client applies a server state corrupts NetComponents mid-iteration
        // in ResetPredictedEntities; the server state already carries the resulting hands.
        if (_timing.ApplyingState)
            return;

        _hands.AddHand(args.Target, ent.Comp.HandID, ent.Comp.Data);
    }

    private void OnGotRemoved(Entity<HandOrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        // prevent a recursive double-delete bug
        if (LifeStage(args.Target) >= EntityLifeStage.Terminating)
            return;

        _hands.RemoveHand(args.Target, ent.Comp.HandID);
    }
}
