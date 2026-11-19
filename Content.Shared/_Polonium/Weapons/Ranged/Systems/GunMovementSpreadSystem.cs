using Content.Shared._Polonium.Weapons.Ranged.Components;
using Content.Shared._Polonium.Weapons.Ranged.Events;
using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Sprinting;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Weapons.Ranged.Systems;

/// <summary>
/// Adds a spread penalty to guns with <see cref="GunMovementSpreadComponent"/> while their holder moves.
/// </summary>
public sealed partial class GunMovementSpreadSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float StillSpeed = 0.1f;

    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunMovementSpreadComponent, GetGunSpreadEvent>(OnGetSpread);

        Subs.CVar(_cfg, CCVars.GunMovementSpread, v => _enabled = v, true);
    }

    private void OnGetSpread(Entity<GunMovementSpreadComponent> ent, ref GetGunSpreadEvent args)
    {
        if (!_enabled || args.User is not { } user)
            return;

        args.Spread += GetPenalty(ent.Comp, user);
    }

    private Angle GetPenalty(GunMovementSpreadComponent comp, EntityUid user)
    {
        var speed = TryComp<PhysicsComponent>(user, out var physics) ? physics.LinearVelocity.Length() : 0f;

        var walkSpeed = MovementSpeedModifierComponent.DefaultBaseWalkSpeed;
        var runSpeed = MovementSpeedModifierComponent.DefaultBaseSprintSpeed;
        var baseWalkSpeed = walkSpeed;
        var baseRunSpeed = runSpeed;
        if (TryComp<MovementSpeedModifierComponent>(user, out var move))
        {
            walkSpeed = move.CurrentWalkSpeed;
            runSpeed = move.CurrentSprintSpeed;
            baseWalkSpeed = move.BaseWalkSpeed;
            baseRunSpeed = move.BaseSprintSpeed;
        }

        var penalty = 0.0;
        TryComp<SprinterComponent>(user, out var sprinter);

        if (speed > StillSpeed)
        {
            if (sprinter is { IsSprinting: true })
                penalty = comp.Sprint.Theta * Math.Clamp(speed / Math.Max(runSpeed, StillSpeed), 0f, 1f);
            else
                penalty = GetMovePenalty(comp, speed, walkSpeed, runSpeed, comp.Run.Theta);

            penalty = Math.Max(penalty, GetMovePenalty(comp, speed, baseWalkSpeed, baseRunSpeed, comp.Sprint.Theta));
        }

        if (sprinter is { IsSprinting: false } && comp.SprintRecovery > TimeSpan.Zero)
        {
            var sinceSprint = _timing.CurTime - sprinter.LastSprint;
            if (sinceSprint < comp.SprintRecovery)
                penalty = Math.Max(penalty, comp.Sprint.Theta * (1 - sinceSprint / comp.SprintRecovery));
        }

        return new Angle(penalty);
    }

    private static double GetMovePenalty(GunMovementSpreadComponent comp, float speed, float walkSpeed, float runSpeed, double limit)
    {
        if (speed <= walkSpeed || runSpeed <= walkSpeed)
            return comp.Walk.Theta * Math.Clamp(speed / Math.Max(walkSpeed, StillSpeed), 0f, 1f);

        return Math.Min(limit, double.Lerp(comp.Walk.Theta, comp.Run.Theta, (speed - walkSpeed) / (runSpeed - walkSpeed)));
    }
}
