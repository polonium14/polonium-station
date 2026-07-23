// SPDX-FileCopyrightText: 2021 20kdc <asdd2808@gmail.com>
// SPDX-FileCopyrightText: 2021 Vera Aguilera Puerto <gradientvera@outlook.com>
// SPDX-FileCopyrightText: 2022 Jezithyr <Jezithyr@gmail.com>
// SPDX-FileCopyrightText: 2022 mirrorcult <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2022 wrexbe <81056464+wrexbe@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Jezithyr <jezithyr@gmail.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 ShadowCommander <10494922+ShadowCommander@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <comedian_vs_clown@hotmail.com>
// SPDX-FileCopyrightText: 2024 0x6273 <0x40@keemail.me>
// SPDX-FileCopyrightText: 2024 Krunklehorn <42424291+Krunklehorn@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Kyle Tyo <36606155+VerinSenpai@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Pointing;
using Robust.Shared.Timing;

namespace Content.Shared.Body.Systems;

public sealed partial class BrainSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mindSystem = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BrainComponent, OrganGotInsertedEvent>((uid, _, args) => HandleMind(args.Target, uid));
        SubscribeLocalEvent<BrainComponent, OrganGotRemovedEvent>((uid, _, args) => HandleMind(uid, args.Target));
        SubscribeLocalEvent<BrainComponent, PointAttemptEvent>(OnPointAttempt);
    }

    private void HandleMind(EntityUid newEntity, EntityUid oldEntity)
    {
        // EnsureComp of networked MindContainer/GhostOnMove during client state application
        // corrupts NetComponents mid-iteration in ResetPredictedEntities. Mind transfer is
        // server-authoritative and replicates on its own.
        if (_timing.ApplyingState)
            return;

        if (TerminatingOrDeleted(newEntity) || TerminatingOrDeleted(oldEntity))
            return;

        EnsureComp<MindContainerComponent>(newEntity);
        EnsureComp<MindContainerComponent>(oldEntity);

        var ghostOnMove = EnsureComp<GhostOnMoveComponent>(newEntity);
        ghostOnMove.MustBeDead = HasComp<MobStateComponent>(newEntity); // Don't ghost living players out of their bodies.

        if (!_mindSystem.TryGetMind(oldEntity, out var mindId, out var mind))
            return;

        _mindSystem.TransferTo(mindId, newEntity, mind: mind);
    }

    private void OnPointAttempt(Entity<BrainComponent> ent, ref PointAttemptEvent args)
    {
        args.Cancel();
    }
}
