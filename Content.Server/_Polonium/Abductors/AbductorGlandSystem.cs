using Content.Shared._Polonium.Abductors;
using Content.Shared.Body;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Abductors;

public sealed partial class AbductorGlandSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    [Dependency] private BodySystem _body = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AbductorGlandComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<AbductorGlandComponent, OrganGotRemovedEvent>(OnRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        var query = EntityQueryEnumerator<AbductorGlandComponent>();
        while (query.MoveNext(out var uid, out var gland))
        {
            if (gland.Body is not { } body || gland.Uses == 0 || now < gland.NextTrigger)
                continue;

            gland.NextTrigger = now + NextDelay(gland);
            if (_mobState.IsDead(body))
                continue;

            if (gland.Uses is { } uses)
                gland.Uses = uses - 1;

            _trigger.Trigger(uid, body, predicted: false);
        }
    }

    /// <summary>
    /// Wakes every gland in the body and collects what the Curator has to report about them.
    /// </summary>
    public List<ProtoId<AbductorCuratorLinePrototype>> WakeAll(EntityUid body)
    {
        var reports = new List<ProtoId<AbductorCuratorLinePrototype>>();
        foreach (var gland in _body.EnumerateOrgans<AbductorGlandComponent>(body))
        {
            var comp = gland.Comp2;
            reports.Add(comp.Report);
            if (comp.Awake)
                continue;

            comp.Awake = true;
            Start((gland.Owner, comp), body);
        }

        return reports;
    }

    private void OnInserted(Entity<AbductorGlandComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (ent.Comp.Awake)
            Start(ent, args.Target);
    }

    private void OnRemoved(Entity<AbductorGlandComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (ent.Comp.Body is not { } body)
            return;

        ent.Comp.Body = null;
        if (!TerminatingOrDeleted(body))
        {
            EntityManager.RemoveComponents(body, ent.Comp.AddedComponents);
            if (ent.Comp.BodyStatusEffect is { } effect)
                _status.TryRemoveStatusEffect(body, effect);
        }

        ent.Comp.AddedComponents = new ComponentRegistry();
    }

    private void Start(Entity<AbductorGlandComponent> ent, EntityUid body)
    {
        ent.Comp.Body = body;
        ent.Comp.NextTrigger = _timing.CurTime + NextDelay(ent.Comp);

        var missing = new ComponentRegistry();
        foreach (var (name, entry) in ent.Comp.BodyComponents)
        {
            if (!HasComp(body, entry.Component.GetType()))
                missing[name] = entry;
        }

        EntityManager.AddComponents(body, missing, removeExisting: false);
        ent.Comp.AddedComponents = missing;

        if (ent.Comp.BodyStatusEffect is { } effect)
            _status.TrySetStatusEffectDuration(body, effect);
    }

    private TimeSpan NextDelay(AbductorGlandComponent gland)
    {
        return _random.Next(gland.MinInterval, gland.MaxInterval);
    }
}
