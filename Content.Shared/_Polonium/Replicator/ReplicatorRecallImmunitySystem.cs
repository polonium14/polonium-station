using Content.Shared.Alert;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Replicator;

public sealed partial class ReplicatorRecallImmunitySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private AlertsSystem _alerts = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorRecallImmuneComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<ReplicatorRecallImmuneComponent>();
        while (query.MoveNext(out var uid, out var immune))
        {
            if (_timing.CurTime >= immune.EndTime)
                RemCompDeferred(uid, immune);
        }
    }

    public bool IsImmune(EntityUid uid)
    {
        return TryComp<ReplicatorRecallImmuneComponent>(uid, out var immune) && _timing.CurTime < immune.EndTime;
    }

    public void Apply(EntityUid uid, TimeSpan duration)
    {
        if (_net.IsClient || duration <= TimeSpan.Zero)
            return;

        var immune = EnsureComp<ReplicatorRecallImmuneComponent>(uid);
        var now = _timing.CurTime;
        immune.EndTime = now + duration;
        Dirty(uid, immune);

        _alerts.ShowAlert(uid, immune.Alert, cooldown: (now, immune.EndTime), autoRemove: true, showCooldown: true);
    }

    private void OnShutdown(Entity<ReplicatorRecallImmuneComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
    }
}
