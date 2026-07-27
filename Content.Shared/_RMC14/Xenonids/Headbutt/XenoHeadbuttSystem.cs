using System.Numerics;
using Content.Shared._RMC14.Xenonids.Crest;
using Content.Shared._RMC14.Xenonids.Fortify;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Headbutt;

public sealed class XenoHeadbuttSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ThrownItemSystem _thrownItem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<ThrownItemComponent> _thrownItemQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _thrownItemQuery = GetEntityQuery<ThrownItemComponent>();

        SubscribeLocalEvent<XenoHeadbuttComponent, XenoHeadbuttActionEvent>(OnXenoHeadbuttAction);
        SubscribeLocalEvent<XenoHeadbuttComponent, ThrowDoHitEvent>(OnXenoHeadbuttHit);
    }

    private void OnXenoHeadbuttAction(Entity<XenoHeadbuttComponent> xeno, ref XenoHeadbuttActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        if (TryComp(xeno, out XenoCrestComponent? crest) && crest.Lowered &&
            !_interaction.InRangeUnobstructed(xeno.Owner, args.Target))
        {
            _popup.PopupClient(Loc.GetString("rmc-xeno-headbutt-too-far"), xeno, xeno, PopupType.SmallCaution);
            return;
        }

        var attempt = new XenoHeadbuttAttemptEvent();
        RaiseLocalEvent(xeno, ref attempt);
        if (attempt.Cancelled)
            return;

        args.Handled = true;

        var origin = _transform.GetMapCoordinates(xeno);
        var target = _transform.GetMapCoordinates(args.Target);
        if (origin.MapId != target.MapId)
            return;

        var diff = target.Position - origin.Position;
        if (diff == Vector2.Zero)
            return;

        diff = diff.Normalized() * xeno.Comp.Range;

        xeno.Comp.Charge = diff;
        Dirty(xeno);

        if (_physicsQuery.TryComp(xeno, out var physics))
            _physics.SetLinearVelocity(xeno, Vector2.Zero, body: physics);

        _throwing.TryThrow(xeno, diff, xeno.Comp.ThrowSpeed, xeno, doSpin: false, recoil: false, pushbackRatio: 0);
    }

    private void OnXenoHeadbuttHit(Entity<XenoHeadbuttComponent> xeno, ref ThrowDoHitEvent args)
    {
        var targetId = args.Target;
        if (targetId == xeno.Owner)
            return;

        if (!HasComp<MobStateComponent>(targetId))
            return;

        if (HasComp<XenoComponent>(targetId) || HasComp<XenoFriendlyComponent>(targetId))
            return;

        if (_physicsQuery.TryGetComponent(xeno, out var physics) &&
            _thrownItemQuery.TryGetComponent(xeno, out var thrown))
        {
            _thrownItem.LandComponent(xeno, thrown, physics, true);
            _thrownItem.StopThrow(xeno, thrown);
        }

        if (_timing.IsFirstTimePredicted)
            xeno.Comp.Charge = null;

        if (_net.IsClient)
            return;

        _audio.PlayPvs(xeno.Comp.Sound, xeno);

        var damage = new DamageSpecifier(xeno.Comp.Damage);
        if (TryComp(xeno, out XenoCrestComponent? crest) && crest.Lowered)
            damage += xeno.Comp.CrestedDamageReduction;

        if (damage.GetTotal() > FixedPoint2.Zero)
            _damageable.TryChangeDamage(targetId, damage, origin: xeno);

        var knockRange = xeno.Comp.ThrowForce;
        if ((TryComp(xeno, out XenoCrestComponent? crest2) && crest2.Lowered) ||
            (TryComp(xeno, out XenoFortifyComponent? fort) && fort.Fortified))
        {
            knockRange += xeno.Comp.CrestFortifiedThrowAdd;
        }

        var origin = _transform.GetWorldPosition(xeno);
        var targetPos = _transform.GetWorldPosition(targetId);
        var direction = targetPos - origin;
        if (direction != default)
            _throwing.TryThrow(targetId, direction.Normalized() * knockRange, xeno.Comp.ThrowSpeed, xeno);

        StopHeadbutt(xeno);
    }

    private void StopHeadbutt(EntityUid xeno)
    {
        if (!_physicsQuery.TryGetComponent(xeno, out var physics))
            return;

        _physics.SetLinearVelocity(xeno, Vector2.Zero, body: physics);
        _physics.SetBodyStatus(xeno, physics, BodyStatus.OnGround);
    }
}
