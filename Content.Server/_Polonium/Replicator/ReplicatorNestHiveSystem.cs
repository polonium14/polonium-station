using Content.Server.Materials;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.Lathe;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Replicator;

public sealed partial class ReplicatorNestHiveSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ReplicatorHiveSystem _hive = default!;
    [Dependency] private MaterialStorageSystem _materialStorage = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedReplicatorNestSystem _nest = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorHiveComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<ReplicatorHiveComponent, ReplicatorNestLevelUpEvent>(OnLevelUp);
        SubscribeLocalEvent<ReplicatorHiveComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<ReplicatorHiveComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<ReplicatorHiveComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ReplicatorHiveComponent, LatheGetResultEvent>(OnLatheResult);
        SubscribeLocalEvent<ReplicatorHiveComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);

        SubscribeLocalEvent<ReplicatorNestPrintComponent, EntGotInsertedIntoContainerMessage>(OnPrintInserted);
        SubscribeLocalEvent<ReplicatorNestPrintComponent, PullStartedMessage>(OnPrintPulled);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ReplicatorRebuildingComponent, ReplicatorComponent>();
        while (query.MoveNext(out var uid, out var rebuilding, out var replicator))
        {
            // Not in the nest's container until the fall ends.
            if (HasComp<ReplicatorNestFallingComponent>(uid))
                continue;

            if (!IsInside(uid, rebuilding.Nest))
            {
                RemCompDeferred(uid, rebuilding);
                _popup.PopupEntity(Loc.GetString("replicator-rebuild-interrupted"), uid, uid, PopupType.MediumCaution);
                continue;
            }

            if (_timing.CurTime >= rebuilding.FinishTime)
                FinishRebuild((uid, replicator), rebuilding);
        }
    }

    private void OnGetVerbs(Entity<ReplicatorHiveComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract
            || !TryComp<ReplicatorComponent>(args.User, out var replicator)
            || replicator.MyNest != ent.Owner
            || HasComp<ReplicatorRebuildingComponent>(args.User)
            || !TryComp<ReplicatorNestComponent>(ent, out var nest))
            return;

        var user = args.User;
        var currentTier = replicator.UpgradeStage + 1;

        foreach (var option in ent.Comp.Rebuilds)
        {
            var verb = new Verb
            {
                Text = Loc.GetString(option.Name, ("cost", ReplicatorHiveSystem.ToSheets(option.Cost))),
                Icon = option.Icon,
                Priority = -option.Tier,
                Act = () => TryStartRebuild((ent.Owner, ent.Comp, nest), user, option),
            };

            if (GetRebuildBlocker(ent, nest, option, currentTier) is { } blocker)
            {
                verb.Disabled = true;
                verb.Message = blocker;
            }

            args.Verbs.Add(verb);
        }
    }

    private void OnLevelUp(Entity<ReplicatorHiveComponent> ent, ref ReplicatorNestLevelUpEvent args)
    {
        if (!TryComp<ReplicatorNestComponent>(ent, out var nest))
            return;

        var unlocked = new List<string>();
        foreach (var option in ent.Comp.Rebuilds)
        {
            if (option.MinNestLevel == args.Level)
                unlocked.Add(Loc.GetString(option.Name, ("cost", ReplicatorHiveSystem.ToSheets(option.Cost))));
        }

        if (unlocked.Count == 0)
            return;

        var message = Loc.GetString("replicator-hive-unlocked", ("level", args.Level), ("classes", string.Join(", ", unlocked)));
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
            ("level", nest.CurrentLevel)));
    }

    private string? GetRebuildBlocker(Entity<ReplicatorHiveComponent> hive, ReplicatorNestComponent nest,
        ReplicatorRebuildOption option, int currentTier)
    {
        if (option.Tier == currentTier)
            return Loc.GetString("replicator-rebuild-fail-same");

        if (nest.CurrentLevel < option.MinNestLevel)
            return Loc.GetString("replicator-rebuild-fail-level", ("level", option.MinNestLevel));

        if (!_hive.CanAfford(hive, option.Cost))
            return Loc.GetString("replicator-construction-fail-budget", ("cost", ReplicatorHiveSystem.ToSheets(option.Cost)));

        return null;
    }

    private void TryStartRebuild(Entity<ReplicatorHiveComponent, ReplicatorNestComponent> hive, EntityUid user,
        ReplicatorRebuildOption option)
    {
        if (!TryComp<ReplicatorComponent>(user, out var replicator)
            || replicator.MyNest != hive.Owner
            || HasComp<ReplicatorRebuildingComponent>(user)
            || !_mobState.IsAlive(user))
            return;

        if (GetRebuildBlocker(hive, hive.Comp2, option, replicator.UpgradeStage + 1) is { } blocker)
        {
            _popup.PopupEntity(blocker, user, user, PopupType.SmallCaution);
            return;
        }

        if (!_hive.TrySpend(hive, option.Cost))
            return;

        var rebuilding = EnsureComp<ReplicatorRebuildingComponent>(user);
        rebuilding.Nest = hive;
        rebuilding.Tier = option.Tier;
        rebuilding.FinishTime = _timing.CurTime + hive.Comp1.RebuildTime;

        _nest.StartDive((hive.Owner, hive.Comp2), user);
        _popup.PopupEntity(Loc.GetString("replicator-rebuild-started"), user, user, PopupType.Medium);
    }

    private void FinishRebuild(Entity<ReplicatorComponent> ent, ReplicatorRebuildingComponent rebuilding)
    {
        var nest = rebuilding.Nest;
        var tier = rebuilding.Tier;
        RemComp(ent, rebuilding);

        _container.TryRemoveFromContainer(ent.Owner, force: true);
        var rebuilt = _nest.UpgradeReplicator(ent, tier, Transform(nest).Coordinates);
        _nest.GiveTierLoadout(rebuilt, tier);
        QueueDel(ent);

        _popup.PopupEntity(Loc.GetString("replicator-rebuild-finished"), rebuilt, rebuilt, PopupType.Medium);
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

    private bool IsInside(EntityUid uid, EntityUid nest)
    {
        return !TerminatingOrDeleted(nest)
               && _container.TryGetContainingContainer(uid, out var container)
               && container.Owner == nest;
    }
}
