// SPDX-FileCopyrightText: 2025 beck <163376292+widgetbeck@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ALooseGoose <ALooseGoosey@gmail.com>
// SPDX-FileCopyrightText: 2026 Damian Zieliński <zientasek.pl@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Impstation.SpawnedFromTracker;
using Content.Shared._Polonium.Replicator; // POLONIUM
using Content.Shared.Actions;
using Content.Shared.Construction.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Impstation.Replicator;

public abstract partial class SharedReplicatorNestSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StepTriggerSystem _stepTrigger = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ReplicatorHiveSystem _hive = default!; // POLONIUM
    [Dependency] private ReplicatorSheetHandSystem _sheetHand = default!; // POLONIUM

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorNestComponent, StepTriggeredOffEvent>(OnStepTriggered);

        SubscribeLocalEvent<ReplicatorComponent, ReplicatorUpgrade2ActionEvent>(OnUpgrade2);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorUpgrade3ActionEvent>(OnUpgrade3);
    }

    private void OnStepTriggered(Entity<ReplicatorNestComponent> ent, ref StepTriggeredOffEvent args)
    {
        if (HasComp<ReplicatorNestFallingComponent>(args.Tripper))
            return;

        // POLONIUM
        if (!_hive.CanDigest(args.Tripper))
            return;

        var isReplicator = HasComp<ReplicatorComponent>(args.Tripper);

        if (TryComp<MobStateComponent>(args.Tripper, out var mobState) && isReplicator && _mobState.IsDead(args.Tripper))
        {
            StartFalling(ent, args.Tripper);
            return;
        }

        if (mobState != null && _mobState.IsAlive(args.Tripper))
            return;

        StartFalling(ent, args.Tripper);
    }

    // POLONIUM
    public void StartDive(Entity<ReplicatorNestComponent> ent, EntityUid diver)
    {
        StartFalling(ent, diver, handlePoints: false);
    }

    private void StartFalling(Entity<ReplicatorNestComponent> ent, EntityUid tripper, bool playSound = true, bool handlePoints = true) // POLONIUM: handlePoints
    {
        if (handlePoints) // POLONIUM
            HandlePoints(ent, tripper);

        if (TryComp<PullableComponent>(tripper, out var pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(tripper, pullable);

        var fall = EnsureComp<ReplicatorNestFallingComponent>(tripper);
        fall.FallingTarget = ent;
        fall.NextDeletionTime = _timing.CurTime + fall.DeletionTime;
        _stun.TryKnockdown(tripper, fall.DeletionTime, false);

        if (playSound)
            _audio.PlayPvs(ent.Comp.FallingSound, tripper);
    }

    private void HandlePoints(Entity<ReplicatorNestComponent> ent, EntityUid tripper)
    {
        if (_whitelist.IsWhitelistPass(ent.Comp.Blacklist, tripper))
            return;

        // POLONIUM: replisteel only refunds the budget, or printed sheets could be fed back to grow the nest
        TryComp<ReplicatorHiveComponent>(ent, out var hive);
        if (hive != null && _hive.GetStoredBudget((ent, hive), tripper) is > 0 and var refund)
        {
            _hive.AddBudget((ent, hive), refund);
            return;
        }

        var pointsBefore = ent.Comp.TotalPoints;
        // POLONIUM end

        ent.Comp.TotalPoints++;
        ent.Comp.SpawningProgress++;

        if (TryComp<ItemComponent>(tripper, out var itemComp))
        {
            if (_item.GetSizePrototype(itemComp.Size) == _item.GetSizePrototype("Large"))
                ent.Comp.TotalPoints++;
            else if (_item.GetSizePrototype(itemComp.Size) == _item.GetSizePrototype("Huge"))
                ent.Comp.TotalPoints += 2;
            else if (_item.GetSizePrototype(itemComp.Size) >= _item.GetSizePrototype("Ginormous"))
                ent.Comp.TotalPoints += 3;

            ent.Comp.SpawningProgress++;
        }
        else if (TryComp<AnchorableComponent>(tripper, out _))
        {
            ent.Comp.TotalPoints += 3;
            ent.Comp.SpawningProgress += 3;
        }
        else if (HasComp<ReplicatorComponent>(tripper))
        {
            ent.Comp.SpawningProgress += ent.Comp.SpawnNewAt / 4;
        }
        else if (HasComp<MobStateComponent>(tripper))
        {
            if (HasComp<HumanoidProfileComponent>(tripper))
            {
                ent.Comp.TotalPoints += ent.Comp.BonusPointsHumanoid * ent.Comp.CurrentLevel;
                ent.Comp.SpawningProgress += ent.Comp.SpawnNewAt;
            }
            else
            {
                ent.Comp.TotalPoints += ent.Comp.BonusPointsAlive * ent.Comp.CurrentLevel;
                ent.Comp.SpawningProgress += ent.Comp.SpawnNewAt / 4;
            }
        }

        // POLONIUM
        if (hive != null)
        {
            var points = ent.Comp.TotalPoints - pointsBefore;
            if (HasComp<ReplicatorComponent>(tripper))
                points += hive.DeadReplicatorPoints;

            _hive.AddPoints((ent, hive), points);
        }

        if (ent.Comp.TotalPoints >= ent.Comp.NextUpgradeAt)
        {
            ent.Comp.CurrentLevel++;

            var growthMessage = $"replicator-nest-level{ent.Comp.CurrentLevel}";
            if (Loc.TryGetString(growthMessage, out var localizedMsg))
                _popup.PopupEntity(localizedMsg, ent);
            else
                _popup.PopupEntity(Loc.GetString("replicator-nest-levelup"), ent);

            if (ent.Comp.CurrentLevel <= ent.Comp.EndgameLevel)
                Embiggen(ent);

            if (ent.Comp.CurrentLevel == ent.Comp.EndgameLevel && TryComp<StepTriggerComponent>(ent, out var stepTrigger))
                _stepTrigger.SetIgnoreWeightless((ent, stepTrigger), true);

            ent.Comp.NextUpgradeAt += ent.Comp.CurrentLevel >= ent.Comp.EndgameLevel
                ? ent.Comp.UpgradeAt * ent.Comp.EndgameLevel
                : ent.Comp.UpgradeAt * ent.Comp.CurrentLevel;

            // POLONIUM: replaces UpgradeAll
            var levelUp = new ReplicatorNestLevelUpEvent(ent.Comp.CurrentLevel);
            RaiseLocalEvent(ent, ref levelUp);
        }

        // POLONIUM: removed SpawnNew

        Dirty(ent);
    }

    private void SpawnNew(Entity<ReplicatorNestComponent> ent)
    {
        if (_net.IsClient)
            return;

        var spawner = Spawn(ent.Comp.ToSpawn, Transform(ent).Coordinates);

        var tracker = EnsureComp<SpawnedFromTrackerComponent>(spawner);
        tracker.SpawnedFrom = ent;

        ent.Comp.UnclaimedSpawners.Add(spawner);
    }

    public void UpgradeAll(Entity<ReplicatorNestComponent> ent)
    {
        if (_net.IsClient || !_timing.IsFirstTimePredicted)
            return;

        foreach (var replicator in ent.Comp.SpawnedMinions)
        {
            if (!TryComp<ReplicatorComponent>(replicator, out var comp))
                continue;

            if (comp.UpgradeStage >= ent.Comp.MaxUpgradeStage || comp.TargetUpgradeStage >= ent.Comp.MaxUpgradeStage)
                continue;

            if (!TryComp<MindContainerComponent>(replicator, out var mindContainer) || mindContainer.Mind == null)
                continue;

            comp.TargetUpgradeStage++;

            var targetAction = comp.TargetUpgradeStage == 1 ? comp.Level2Action : comp.Level3Action;
            _actions.AddAction(replicator, targetAction);
        }
    }

    public void OnUpgrade2(Entity<ReplicatorComponent> ent, ref ReplicatorUpgrade2ActionEvent args)
    {
        if (_net.IsClient || !_timing.IsFirstTimePredicted)
            return;

        var oldUid = ent.Owner;
        var upgradedUid = UpgradeReplicator(ent, 2);

        QueueDel(oldUid);
        QueueDel(args.Action);

        if (!Exists(upgradedUid))
            return;

        GiveTierLoadout(upgradedUid, 2); // POLONIUM
    }

    public void OnUpgrade3(Entity<ReplicatorComponent> ent, ref ReplicatorUpgrade3ActionEvent args)
    {
        if (_net.IsClient || !_timing.IsFirstTimePredicted)
            return;

        var oldUid = ent.Owner;
        var upgradedUid = UpgradeReplicator(ent, 3);

        QueueDel(ent);
        QueueDel(args.Action);

        if (!Exists(upgradedUid))
            return;

        GiveTierLoadout(upgradedUid, 3); // POLONIUM
    }

    // POLONIUM
    public void GiveTierLoadout(EntityUid uid, int tier)
    {
        switch (tier)
        {
            case 2:
                var replicatorOmnitool = Spawn("OmnitoolUnremoveable");
                var replicatorWelder = Spawn("WelderExperimentalUnremoveable");
                _handsSystem.AddHand(uid, "Middle Tool Slot", HandLocation.Middle);
                _handsSystem.AddHand(uid, "Right Tool Slot", HandLocation.Right);
                _handsSystem.TrySetActiveHand(uid, "Right Tool Slot");
                _handsSystem.TryPickupAnyHand(uid, replicatorOmnitool);
                _handsSystem.TrySetActiveHand(uid, "Middle Tool Slot");
                _handsSystem.TryPickupAnyHand(uid, replicatorWelder);
                break;
            case 3:
                var replicatorArm = Spawn("ReplicatorT3Weapon");
                _handsSystem.AddHand(uid, "Arm", HandLocation.Middle);
                _handsSystem.TryPickupAnyHand(uid, replicatorArm);
                break;
        }
    }

    public EntityUid UpgradeReplicator(Entity<ReplicatorComponent> ent, int desiredLevel, EntityCoordinates? coordinates = null) // POLONIUM: coordinates
    {
        var oldUid = ent.Owner;
        var xform = Transform(oldUid);

        // POLONIUM
        var nextStage = desiredLevel switch
        {
            1 => ent.Comp.Level1Id,
            2 => ent.Comp.Level2Id,
            _ => ent.Comp.Level3Id,
        };

        var upgraded = Spawn(nextStage, coordinates ?? xform.Coordinates); // POLONIUM

        var upgradedComp = EnsureComp<ReplicatorComponent>(upgraded);
        upgradedComp.RelatedReplicators = ent.Comp.RelatedReplicators;
        upgradedComp.TargetUpgradeStage = ent.Comp.TargetUpgradeStage;

        // POLONIUM
        upgradedComp.MyNest = ent.Comp.MyNest;
        upgradedComp.Queen |= ent.Comp.Queen;
        upgradedComp.HasSpawnedNest = ent.Comp.HasSpawnedNest;

        if (ent.Comp.MyNest is { } myNest && TryComp<ReplicatorNestComponent>(myNest, out var nestComp)) // POLONIUM
        {
            nestComp.SpawnedMinions.Remove(oldUid);
            nestComp.SpawnedMinions.Add(upgraded);
        }

        _sheetHand.TransferSheets(oldUid, upgraded); // POLONIUM

        if (_mind.TryGetMind(oldUid, out var mind, out _))
            _mind.TransferTo(mind, upgraded);

        return upgraded;
    }

    protected void Embiggen(Entity<ReplicatorNestComponent> ent)
    {
        var ev = new ReplicatorNestEmbiggenedEvent(ent);
        RaiseLocalEvent(ent, ref ev);
    }
}

public sealed partial class ReplicatorSpawnNestActionEvent : InstantActionEvent;

public sealed partial class ReplicatorUpgrade2ActionEvent : InstantActionEvent;

public sealed partial class ReplicatorUpgrade3ActionEvent : InstantActionEvent;

// POLONIUM
[ByRefEvent]
public readonly record struct ReplicatorNestLevelUpEvent(int Level);

[ByRefEvent]
public sealed partial class ReplicatorNestEmbiggenedEvent(Entity<ReplicatorNestComponent> ent) : EntityEventArgs
{
    public Entity<ReplicatorNestComponent> Ent { get; set; } = ent;
}
