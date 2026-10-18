using Content.Server.Destructible;
using Content.Server.Materials;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Impstation.SpawnedFromTracker;
using Content.Shared._Polonium.Lathe;
using Content.Shared._Polonium.Replicator;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.Examine;
using Content.Shared.Lathe;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Replicator;

public sealed partial class ReplicatorNestHiveSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private ReplicatorHiveSystem _hive = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MaterialStorageSystem _materialStorage = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private readonly HashSet<Entity<ReplicatorComponent>> _replicators = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorHiveComponent, ReplicatorNestLevelUpEvent>(OnLevelUp);
        SubscribeLocalEvent<ReplicatorHiveComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<ReplicatorHiveComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<ReplicatorHiveComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ReplicatorHiveComponent, LatheQueueAttemptEvent>(OnQueueAttempt);
        SubscribeLocalEvent<ReplicatorHiveComponent, LatheGetResultEvent>(OnLatheResult);
        SubscribeLocalEvent<ReplicatorHiveComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);

        SubscribeLocalEvent<ReplicatorNestPrintComponent, EntGotInsertedIntoContainerMessage>(OnPrintInserted);
        SubscribeLocalEvent<ReplicatorNestPrintComponent, PullStartedMessage>(OnPrintPulled);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ReplicatorHiveComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hive, out var xform))
        {
            if (now >= hive.NextQueenCheck)
            {
                hive.NextQueenCheck = now + hive.QueenCheckInterval;
                UpdateQueenRespawn((uid, hive), now);
            }

            if (now < hive.NextHeal)
                continue;

            hive.NextHeal = now + hive.HealInterval;

            _replicators.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, hive.HealRange, _replicators);
            foreach (var replicator in _replicators)
            {
                if (replicator.Comp.MyNest == uid
                    && !HasComp<ReplicatorColossusComponent>(replicator)
                    && _mobState.IsAlive(replicator))
                {
                    _damageable.TryChangeDamage(replicator.Owner, hive.Heal, true, false);
                }
            }
        }
    }

    private void UpdateQueenRespawn(Entity<ReplicatorHiveComponent> ent, TimeSpan now)
    {
        if (!TryComp<ReplicatorNestComponent>(ent, out var nest))
            return;

        if (!IsAbandoned(ent.Owner, nest))
        {
            ent.Comp.QueenDueAt = null;
            return;
        }

        ent.Comp.QueenDueAt ??= now + ent.Comp.QueenRespawnDelay;
        if (now < ent.Comp.QueenDueAt)
            return;

        ent.Comp.QueenDueAt = null;

        var spawner = Spawn(ent.Comp.QueenSpawner, Transform(ent).Coordinates);
        EnsureComp<SpawnedFromTrackerComponent>(spawner).SpawnedFrom = ent;
        nest.UnclaimedSpawners.Add(spawner);
    }

    private bool IsAbandoned(EntityUid nestUid, ReplicatorNestComponent nest)
    {
        foreach (var spawner in nest.UnclaimedSpawners)
        {
            if (!TerminatingOrDeleted(spawner))
                return false;
        }

        var query = EntityQueryEnumerator<ReplicatorComponent>();
        while (query.MoveNext(out var uid, out var replicator))
        {
            if (replicator.MyNest == nestUid && !TerminatingOrDeleted(uid) && !_mobState.IsDead(uid))
                return false;
        }

        return true;
    }

    private void OnLevelUp(Entity<ReplicatorHiveComponent> ent, ref ReplicatorNestLevelUpEvent args)
    {
        if (!TryComp<ReplicatorNestComponent>(ent, out var nest))
            return;

        if (args.Level <= nest.EndgameLevel && TryComp<DestructibleComponent>(ent, out var destructible))
        {
            foreach (var threshold in destructible.Thresholds)
            {
                if (threshold.Trigger is DamageTrigger trigger)
                    trigger.Damage *= ent.Comp.HealthGrowth;
            }
        }

        var unlocked = new List<string>();
        foreach (var structure in _prototype.EnumeratePrototypes<ReplicatorStructurePrototype>())
        {
            if (structure.MinNestLevel == args.Level)
                unlocked.Add(_prototype.Index(structure.Entity).Name);
        }

        if (unlocked.Count == 0)
            return;

        var message = Loc.GetString("replicator-hive-unlocked", ("level", args.Level), ("structures", string.Join(", ", unlocked)));
        foreach (var minion in nest.SpawnedMinions)
        {
            if (!TerminatingOrDeleted(minion) && _mobState.IsAlive(minion))
                _popup.PopupEntity(message, minion, minion, PopupType.Medium);
        }
    }

    private void OnDestroyed(Entity<ReplicatorHiveComponent> ent, ref DestructionEventArgs args)
    {
        var spill = (int)(_hive.GetBudget(ent) * ent.Comp.SpillFraction);
        if (spill > 0)
            _materialStorage.SpawnMultipleFromMaterial(spill, ent.Comp.Material, Transform(ent).Coordinates);
    }

    private void OnUiOpenAttempt(Entity<ReplicatorHiveComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<ReplicatorComponent>(args.User))
            args.Cancel();
    }

    private void OnExamined(Entity<ReplicatorHiveComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<ReplicatorComponent>(args.Examiner) || !TryComp<ReplicatorNestComponent>(ent, out var nest))
            return;

        args.PushMarkup(Loc.GetString("replicator-hive-examine",
            ("budget", ReplicatorHiveSystem.ToSheets(_hive.GetBudget(ent))),
            ("reserve", ReplicatorHiveSystem.ToSheets(ent.Comp.WithdrawReserve)),
            ("level", nest.CurrentLevel)));
    }

    private void OnQueueAttempt(Entity<ReplicatorHiveComponent> ent, ref LatheQueueAttemptEvent args)
    {
        if (!args.Recipe.Materials.TryGetValue(ent.Comp.Material, out var perItem)
            || _hive.GetBudget(ent) - perItem * args.Quantity >= ent.Comp.WithdrawReserve)
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("replicator-hive-withdraw-reserve",
                ("reserve", ReplicatorHiveSystem.ToSheets(ent.Comp.WithdrawReserve))),
            ent, args.Actor, PopupType.SmallCaution);
    }

    // Printed sheets spawn on top of the nest and would fall straight back in.
    private void OnLatheResult(Entity<ReplicatorHiveComponent> ent, ref LatheGetResultEvent args)
    {
        EnsureComp<ReplicatorNestPrintComponent>(args.ResultItem);
    }

    private void OnStepTriggerAttempt(Entity<ReplicatorHiveComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (HasComp<ReplicatorNestPrintComponent>(args.Tripper))
            args.Cancelled = true;
    }

    private void OnPrintInserted(Entity<ReplicatorNestPrintComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        RemCompDeferred(ent, ent.Comp);
    }

    private void OnPrintPulled(Entity<ReplicatorNestPrintComponent> ent, ref PullStartedMessage args)
    {
        RemCompDeferred(ent, ent.Comp);
    }
}
