// SPDX-FileCopyrightText: 2025 ALooseGoose <ALooseGoosey@gmail.com>
// SPDX-FileCopyrightText: 2025 beck <163376292+widgetbeck@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Damian Zieliński <zientasek.pl@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.GameTicking;
using Content.Server.Pinpointer;
using Content.Server._Polonium.Replicator; // POLONIUM
using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator; // POLONIUM
using Content.Shared.Destructible;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Content.Shared.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Server.Buckle.Systems;
using Content.Shared.Storage;
using Content.Shared.GameTicking;

namespace Content.Server._Impstation.Replicator;

public sealed partial class ReplicatorNestSystem : SharedReplicatorNestSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ReplicatorHiveSystem _hive = default!; // POLONIUM
    [Dependency] private ReplicatorQueenSystem _queen = default!; // POLONIUM
    [Dependency] private EntityStorageSystem _entStorage = default!; // POLONIUM

    private readonly List<EntityUid> _protectedInside = new(); // POLONIUM

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorNestComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ReplicatorNestComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<ReplicatorNestComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<ReplicatorNestFallingComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<ReplicatorNestComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ReplicatorNestFallingComponent>();
        while (query.MoveNext(out var uid, out var falling))
        {
            if (_timing.CurTime < falling.NextDeletionTime)
                continue;

            Digest(falling.FallingTarget, uid); // POLONIUM
        }
    }

    // POLONIUM
    private void Digest(Entity<ReplicatorNestComponent> nest, EntityUid uid)
    {
        if (TerminatingOrDeleted(nest.Owner))
        {
            RemCompDeferred<ReplicatorNestFallingComponent>(uid);
            return;
        }

        if (TryComp<EntityStorageComponent>(uid, out var entStorage))
        {
            _entStorage.EmptyContents(uid, entStorage);
        }

        if (TryComp<StorageComponent>(uid, out var container))
        {
            _containerSystem.EmptyContainer(container.Container, true);
        }

        HandlePoints(nest, uid);

        QueueDel(uid);
    }

    private void OnEntRemoved(Entity<ReplicatorNestComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        RemCompDeferred<StunnedComponent>(args.Entity);
    }

    private void OnMapInit(Entity<ReplicatorNestComponent> ent, ref MapInitEvent args)
    {
        if (!Transform(ent).Coordinates.IsValid(EntityManager))
            QueueDel(ent);

        ent.Comp.Hole = _containerSystem.EnsureContainer<Container>(ent, "hole");

        ent.Comp.NextSpawnAt = ent.Comp.SpawnNewAt;
        ent.Comp.NextUpgradeAt = ent.Comp.UpgradeAt;
    }

    private void OnStepTriggerAttempt(Entity<ReplicatorNestComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = true;
    }

    private void OnUpdateCanMove(Entity<ReplicatorNestFallingComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnDestruction(Entity<ReplicatorNestComponent> ent, ref DestructionEventArgs args)
    {
        HandleDestruction(ent);
    }

    private void HandleDestruction(Entity<ReplicatorNestComponent> ent)
    {
        if (ent.Comp.Hole != null)
        {
            foreach (var uid in _containerSystem.EmptyContainer(ent.Comp.Hole))
            {
                RemCompDeferred<StunnedComponent>(uid);
                _stun.TryKnockdown(uid, TimeSpan.FromSeconds(2), false);
            }
        }

        foreach (var spawner in ent.Comp.UnclaimedSpawners)
        {
            ent.Comp.UnclaimedSpawners.Remove(spawner);
            QueueDel(spawner);
        }

        var fallingQuery = EntityQueryEnumerator<ReplicatorNestFallingComponent>();
        while (fallingQuery.MoveNext(out var uid, out var falling))
        {
            if (falling.FallingTarget == ent)
                RemCompDeferred<ReplicatorNestFallingComponent>(uid);
        }

        foreach (var replicator in ent.Comp.SpawnedMinions)
        {
            if (!HasComp<ReplicatorComponent>(replicator))
                continue;

            if (!_mobState.IsAlive(replicator))
                continue;

            _popup.PopupEntity(Loc.GetString("replicator-nest-destroyed"), replicator, replicator);
        }

        _queen.UpdateHive(ent, nestLost: true); // POLONIUM: the queen and her nest action are picked there
    }

    private void OnRoundEndTextAppend(ref RoundEndTextAppendEvent args)
    {
        List<Entity<ReplicatorNestComponent>> nests = [];

        var query = AllEntityQuery<ReplicatorNestComponent>();
        while (query.MoveNext(out var uid, out var comp))
            nests.Add((uid, comp));

        if (nests.Count == 0)
            return;

        args.AddLine("");

        foreach (var ent in nests)
        {
            var location = "Unknown";
            var mapCoords = _transform.ToMapCoordinates(Transform(ent).Coordinates);
            if (_navMap.TryGetNearestBeacon(mapCoords, out var beacon, out _) && beacon != null && beacon.Value.Comp.Text != null)
                location = beacon.Value.Comp.Text;

            args.AddLine(Loc.GetString(
                "replicator-nest-end-of-round",
                ("location", location),
                ("level", ent.Comp.CurrentLevel),
                ("points", ent.Comp.TotalPoints),
                ("replicators", ent.Comp.SpawnedMinions.Count)));
        }

        args.AddLine("");
    }
}
