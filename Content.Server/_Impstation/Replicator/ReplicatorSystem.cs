// SPDX-FileCopyrightText: 2025 beck <163376292+widgetbeck@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Damian Zieliński <zientasek.pl@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Polonium.Replicator; // POLONIUM
using Content.Shared.Ghost;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Impstation.SpawnedFromTracker;
using Content.Shared.Mind.Components;

namespace Content.Server._Impstation.Replicator;

public sealed partial class ReplicatorSystem : SharedReplicatorSystem
{
    [Dependency] private ReplicatorQueenSystem _queen = default!; // POLONIUM

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<ReplicatorComponent, GhostRoleSpawnerUsedEvent>(OnGhostRoleSpawnerUsed);
    }

    private void OnMindAdded(Entity<ReplicatorComponent> ent, ref MindAddedMessage args)
    {
        _queen.RefreshNestAction(ent); // POLONIUM
    }

    private void OnGhostRoleSpawnerUsed(Entity<ReplicatorComponent> ent, ref GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp<SpawnedFromTrackerComponent>(args.Spawner, out var tracker)
            || !TryComp<ReplicatorNestComponent>(tracker.SpawnedFrom, out var nestComp))
            return;

        nestComp.SpawnedMinions.Add(ent);
        nestComp.UnclaimedSpawners.Remove(args.Spawner);
        ent.Comp.MyNest = tracker.SpawnedFrom;
    }
}
