using System.Numerics;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Screech;

public sealed partial class XenoScreechSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private XenoPlasmaSystem _plasma = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<EntityUid> _nearby = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoScreechComponent, XenoScreechActionEvent>(OnScreech);
    }

    private void OnScreech(Entity<XenoScreechComponent> xeno, ref XenoScreechActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        if (!_plasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;

        // wave originates at the queen - ScreechShockWave reads this entity's world pos
        PredictedSpawnAttachedTo(xeno.Comp.Effect, new EntityCoordinates(xeno.Owner, Vector2.Zero));
        _audio.PlayPredicted(xeno.Comp.Sound, xeno, xeno);

        if (_net.IsClient)
            return;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(Transform(xeno).Coordinates, xeno.Comp.Range, _nearby);

        foreach (var ent in _nearby)
        {
            if (ent == xeno.Owner)
                continue;

            if (!HasComp<MobStateComponent>(ent) || HasComp<XenoComponent>(ent) || HasComp<XenoFriendlyComponent>(ent))
                continue;

            _stun.TryUpdateParalyzeDuration(ent, xeno.Comp.StunTime);
        }
    }
}
