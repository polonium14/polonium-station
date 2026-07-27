using System.Numerics;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Punch;

public sealed partial class XenoPunchSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoPunchComponent, XenoPunchActionEvent>(OnXenoPunchAction);
    }

    private void OnXenoPunchAction(Entity<XenoPunchComponent> xeno, ref XenoPunchActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        if (!_interaction.InRangeUnobstructed(args.Performer, args.Target, xeno.Comp.Range))
            return;

        if (!HasComp<MobStateComponent>(args.Target))
            return;

        if (HasComp<XenoComponent>(args.Target) || HasComp<XenoFriendlyComponent>(args.Target))
            return;

        var attempt = new XenoPunchAttemptEvent();
        RaiseLocalEvent(xeno, ref attempt);
        if (attempt.Cancelled)
            return;

        args.Handled = true;

        PlayPunchAnimation(xeno, args.Target);

        if (_net.IsClient)
            return;

        _audio.PlayPvs(xeno.Comp.Sound, xeno);
        _damageable.TryChangeDamage(args.Target, xeno.Comp.Damage, origin: xeno);

        var origin = _transform.GetWorldPosition(xeno);
        var target = _transform.GetWorldPosition(args.Target);
        var direction = target - origin;
        if (direction != default)
            _throwing.TryThrow(args.Target, direction.Normalized() * xeno.Comp.ThrowRange, xeno.Comp.ThrowSpeed, xeno);

        _movementMod.TryUpdateMovementSpeedModDuration(
            args.Target,
            MovementModStatusSystem.TaserSlowdown,
            xeno.Comp.SlowDuration,
            xeno.Comp.SlowMultiplier);
    }

    private void PlayPunchAnimation(EntityUid user, EntityUid target)
    {
        if (!TryComp(user, out TransformComponent? userXform) || userXform.MapID == MapId.Nullspace)
            return;

        var targetMap = _transform.GetMapCoordinates(target);
        if (targetMap.MapId != userXform.MapID)
            return;

        var invMatrix = _transform.GetInvWorldMatrix(userXform);
        var localPos = Vector2.Transform(targetMap.Position, invMatrix);
        if (localPos.LengthSquared() <= 0f)
            return;

        localPos = userXform.LocalRotation.RotateVec(localPos);
        _melee.DoLunge(user, user, Angle.Zero, localPos, null);
    }
}
