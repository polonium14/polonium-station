using Content.Server.Database;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Electrocution;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Content.Shared.Tools.Components;
using Content.Shared.Wall;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Network;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// Keeps a run survivable. The trainee cannot take the hull apart, and damage that would end
/// the lesson early is turned into something the mentor can comment on instead.
/// </summary>
public sealed partial class TutorialSystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    private const string ShockDamage = "Shock";

    private void OnWallUsing(EntityUid uid, WallComponent component, InteractUsingEvent args)
    {
        TryBlockHullUsing(uid, args);
    }

    private void OnWindowUsing(EntityUid uid, WallMountComponent component, InteractUsingEvent args)
    {
        TryBlockHullUsing(uid, args);
    }

    private void OnSealedUsing(Entity<TutorialSealedComponent> ent, ref InteractUsingEvent args)
    {
        args.Handled = true;
    }

    private void TryBlockHullUsing(EntityUid uid, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<TutorialSessionComponent>(args.User, out var session))
            return;

        if (!IsHullStructure(uid))
            return;

        // a cable coil clicks the wall to occupy that tile, it is not taking the wall apart
        if (!HasComp<ToolComponent>(args.Used))
            return;

        if (!TryBlockStructureAttack(args.User, session, uid))
            return;

        args.Handled = true;
    }

    private void OnPlayerDamage(Entity<TutorialSessionComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!args.Damage.AnyPositive())
            return;

        // the jolt, the stun and the popup all still happen, only the burn is left out
        if (IsShockOnly(args.Damage))
        {
            args.Cancelled = true;
            return;
        }

        if (!_thresholds.TryGetThresholdForState(ent.Owner, MobState.Critical, out var critAt) || critAt is null)
            return;

        if (!TryComp<DamageableComponent>(ent.Owner, out var dmg))
            return;

        if (_damageable.GetPositiveDamage((ent.Owner, dmg)).GetTotal() + args.Damage.GetTotal() < critAt.Value)
            return;

        args.Cancelled = true;
    }

    private static bool IsShockOnly(DamageSpecifier damage)
    {
        var any = false;
        foreach (var (type, amount) in damage.DamageDict)
        {
            if (amount <= 0)
                continue;

            if (type != ShockDamage)
                return false;

            any = true;
        }

        return any;
    }

    private void OnTraineeShocked(Entity<TutorialSessionComponent> ent, ref ElectrocutedEvent args)
    {
        if (!ent.Comp.RequireInsulatedGloves || _timing.CurTime < ent.Comp.NextShockQuip)
            return;

        ent.Comp.NextShockQuip = _timing.CurTime + ShockQuipCooldown;
        _mentor.Enqueue(ent.Owner, new LocId[] { "tutorial-holopad-r16-shocked" });
    }

    private void OnPlayerMobState(Entity<TutorialSessionComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Alive or MobState.Invalid)
            return;

        _damageable.ClearAllDamage(ent.Owner);
        _mobState.ChangeMobState(ent.Owner, MobState.Alive);
        _standing.Stand(ent.Owner);

        // back to the start of the room. the navigation anchor is where the step wants them to go,
        // and landing there on a revive used to finish the step without them
        if (TryGetCurrentStep(ent.Comp, out _, out var step)
            && TryGetRoomMarker(step.ID, out var room)
            && !Reaches(step.Completion, room))
            _actions.Teleport(ent.Owner, room);

        _mentor.Enqueue(ent.Owner, new LocId[] { "tutorial-holopad-quip-death" });
    }
}
