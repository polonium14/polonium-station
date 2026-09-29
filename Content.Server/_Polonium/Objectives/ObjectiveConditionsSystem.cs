using Content.Server.Objectives.Systems;
using Content.Server.Station.Components;
using Content.Shared.AlertLevel;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Station;
using Content.Shared.Whitelist;

namespace Content.Server._Polonium.Objectives;

public sealed partial class ObjectiveConditionsSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private NumberObjectiveSystem _number = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private TargetObjectiveSystem _target = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TargetWhitelistConditionComponent, ObjectiveGetProgressEvent>(OnTargetWhitelistProgress);
        SubscribeLocalEvent<MindWhitelistCountConditionComponent, ObjectiveGetProgressEvent>(OnMindCountProgress);
        SubscribeLocalEvent<EntityCountConditionComponent, ObjectiveGetProgressEvent>(OnEntityCountProgress);
        SubscribeLocalEvent<AlertLevelReachedConditionComponent, ObjectiveGetProgressEvent>(OnAlertLevelProgress);
        SubscribeLocalEvent<AlertLevelReachedConditionComponent, ObjectiveAssignedEvent>(OnAlertAssigned);
        SubscribeLocalEvent<WearingWhitelistConditionComponent, ObjectiveGetProgressEvent>(OnWearingProgress);

        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
    }

    private void OnTargetWhitelistProgress(Entity<TargetWhitelistConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = _target.GetTarget(ent, out var target)
                        && TryComp<MindComponent>(target, out var mind)
                        && BodyPasses(mind, ent.Comp.Whitelist, ent.Comp.RequireAlive)
            ? 1f
            : 0f;
    }

    private void OnMindCountProgress(Entity<MindWhitelistCountConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var count = 0;
        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var mindId, out var mind))
        {
            if (mindId != args.MindId && BodyPasses(mind, ent.Comp.Whitelist, ent.Comp.RequireAlive))
                count++;
        }

        args.Progress = GetProgress(ent, count);
    }

    private void OnEntityCountProgress(Entity<EntityCountConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var count = 0;
        var type = EntityManager.ComponentFactory.GetRegistration(ent.Comp.Component).Type;
        var query = EntityManager.AllEntityQueryEnumerator(type);
        while (query.MoveNext(out var uid, out _))
        {
            if (ent.Comp.StationOnly && _station.GetOwningStation(uid) == null)
                continue;

            if (_whitelist.IsWhitelistPassOrNull(ent.Comp.Whitelist, uid))
                count++;
        }

        args.Progress = GetProgress(ent, count);
    }

    private void OnAlertLevelProgress(Entity<AlertLevelReachedConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = ent.Comp.Reached ? 1f : 0f;
    }

    private void OnWearingProgress(Entity<WearingWhitelistConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = IsWearing(args.Mind, ent.Comp) ? 1f : 0f;
    }

    private void OnAlertAssigned(Entity<AlertLevelReachedConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        ent.Comp.Station = StationFor(args.Mind);
    }

    private void OnAlertLevelChanged(ref AlertLevelChangedEvent args)
    {
        var query = AllEntityQuery<AlertLevelReachedConditionComponent>();
        while (query.MoveNext(out var comp))
        {
            if (comp.Station == args.Station && comp.Levels.Contains(args.AlertLevel))
                comp.Reached = true;
        }
    }

    private EntityUid? StationFor(MindComponent mind)
    {
        if (mind.OwnedEntity is { } body
            && _station.GetOwningStation(body) is { } owned
            && HasComp<AlertLevelComponent>(owned))
            return owned;

        var query = EntityQueryEnumerator<StationEventEligibleComponent, AlertLevelComponent>();

        return query.MoveNext(out var uid, out _, out _) ? uid : null;
    }

    private bool BodyPasses(MindComponent mind, EntityWhitelist whitelist, bool requireAlive)
    {
        if (mind.OwnedEntity is not { } body)
            return false;

        if (requireAlive && _mind.IsCharacterDeadIc(mind))
            return false;

        return _whitelist.IsWhitelistPass(whitelist, body);
    }

    private bool IsWearing(MindComponent mind, WearingWhitelistConditionComponent comp)
    {
        if (mind.OwnedEntity is not { } body || _mind.IsCharacterDeadIc(mind))
            return false;

        foreach (var slot in comp.Slots)
        {
            if (!_inventory.TryGetSlotEntity(body, slot, out var item)
                || !_whitelist.IsWhitelistPass(comp.Whitelist, item.Value))
                return false;
        }

        return true;
    }

    private float GetProgress(EntityUid objective, int count)
    {
        var target = _number.GetTarget(objective);
        return target <= 0 ? 1f : MathF.Min(count / (float) target, 1f);
    }
}
