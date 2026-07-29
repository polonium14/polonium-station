using System.Numerics;
using Content.Shared.Coordinates;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Stab;

public sealed partial class XenoTailStabSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoTailStabComponent, XenoTailStabActionEvent>(OnTailStab);
    }

    private void OnTailStab(Entity<XenoTailStabComponent> xeno, ref XenoTailStabActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        if (!_interaction.InRangeUnobstructed(args.Performer, args.Target, xeno.Comp.Range))
            return;

        args.Handled = true;

        PlayTailAnimation(xeno, args.Target, xeno.Comp);
        PredictedSpawnAttachedTo(xeno.Comp.HitAnimationId, args.Target.ToCoordinates());

        if (_net.IsClient)
            return;

        _damageable.TryChangeDamage(args.Target, xeno.Comp.TailDamage, origin: args.Performer);
        _audio.PlayPvs(xeno.Comp.Sound, xeno);
    }

    private void PlayTailAnimation(EntityUid user, EntityUid target, XenoTailStabComponent stab)
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

        // shorten a bit so the thrust doesnt overshoot the hit
        const float bufferLength = 0.2f;
        var visualLength = stab.Range - bufferLength;
        if (visualLength > 0f && localPos.Length() > visualLength)
            localPos = localPos.Normalized() * visualLength;

        _melee.DoLunge(user, user, Angle.Zero, localPos, stab.TailAnimationId);
    }
}
