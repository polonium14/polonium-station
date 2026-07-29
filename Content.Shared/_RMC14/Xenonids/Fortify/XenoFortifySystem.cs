using Content.Shared._RMC14.Xenonids.Crest;
using Content.Shared._RMC14.Xenonids.Headbutt;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared._RMC14.Xenonids.Sweep;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using static Content.Shared.Physics.CollisionGroup;

namespace Content.Shared._RMC14.Xenonids.Fortify;

public sealed partial class XenoFortifySystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoFortifyComponent, XenoFortifyActionEvent>(OnXenoFortifyAction);

        SubscribeLocalEvent<XenoFortifyComponent, DamageModifyEvent>(OnXenoFortifyDamageModify);
        SubscribeLocalEvent<XenoFortifyComponent, KnockDownAttemptEvent>(OnXenoFortifyKnockDownAttempt);

        SubscribeLocalEvent<XenoFortifyComponent, ChangeDirectionAttemptEvent>(OnXenoFortifyCancel);
        SubscribeLocalEvent<XenoFortifyComponent, UpdateCanMoveEvent>(OnXenoFortifyCancel);

        SubscribeLocalEvent<XenoFortifyComponent, AttackAttemptEvent>(OnXenoFortifyAttack);
        SubscribeLocalEvent<XenoFortifyComponent, XenoHeadbuttAttemptEvent>(OnXenoFortifyHeadbuttAttempt);
        SubscribeLocalEvent<XenoFortifyComponent, XenoRestAttemptEvent>(OnXenoFortifyRestAttempt);
        SubscribeLocalEvent<XenoFortifyComponent, XenoTailSweepAttemptEvent>(OnXenoFortifyTailSweepAttempt);
        SubscribeLocalEvent<XenoFortifyComponent, XenoToggleCrestAttemptEvent>(OnXenoFortifyToggleCrestAttempt);
        SubscribeLocalEvent<XenoFortifyComponent, MobStateChangedEvent>(OnXenoFortifyMobStateChanged);
        SubscribeLocalEvent<XenoFortifyComponent, RefreshMovementSpeedModifiersEvent>(OnXenoFortifyRefreshSpeed);
    }

    private void OnXenoFortifyAction(Entity<XenoFortifyComponent> xeno, ref XenoFortifyActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        var attempt = new XenoFortifyAttemptEvent();
        RaiseLocalEvent(xeno, ref attempt);
        if (attempt.Cancelled)
            return;

        args.Handled = true;
        _audio.PlayPredicted(xeno.Comp.FortifySound, xeno, xeno);

        if (xeno.Comp.Fortified)
            Unfortify(xeno);
        else
            Fortify(xeno);

        _actions.SetToggled((EntityUid)args.Action, xeno.Comp.Fortified);
    }

    private void OnXenoFortifyDamageModify(Entity<XenoFortifyComponent> xeno, ref DamageModifyEvent args)
    {
        if (xeno.Comp.Fortified)
            args.Damage *= xeno.Comp.DamageModifier;
    }

    private void OnXenoFortifyKnockDownAttempt(Entity<XenoFortifyComponent> xeno, ref KnockDownAttemptEvent args)
    {
        if (xeno.Comp.Fortified)
            args.Cancelled = true;
    }

    private void OnXenoFortifyCancel<T>(Entity<XenoFortifyComponent> xeno, ref T args)
        where T : CancellableEntityEventArgs
    {
        if (xeno.Comp.Fortified && !xeno.Comp.CanMoveFortified)
            args.Cancel();
    }

    private void OnXenoFortifyAttack(Entity<XenoFortifyComponent> xeno, ref AttackAttemptEvent args)
    {
        if (!xeno.Comp.Fortified || args.Target is not { } target)
            return;

        if (HasComp<MobStateComponent>(target))
            args.Cancel();
    }

    private void OnXenoFortifyHeadbuttAttempt(Entity<XenoFortifyComponent> xeno, ref XenoHeadbuttAttemptEvent args)
    {
        if (xeno.Comp.CanHeadbuttFortified || !xeno.Comp.Fortified)
            return;

        _popup.PopupClient(Loc.GetString("cm-xeno-fortify-cant-headbutt"), xeno, xeno);
        args.Cancelled = true;
    }

    private void OnXenoFortifyRestAttempt(Entity<XenoFortifyComponent> xeno, ref XenoRestAttemptEvent args)
    {
        if (!xeno.Comp.Fortified)
            return;

        _popup.PopupClient(Loc.GetString("cm-xeno-fortify-cant-rest"), xeno, xeno);
        args.Cancelled = true;
    }

    private void OnXenoFortifyTailSweepAttempt(Entity<XenoFortifyComponent> xeno, ref XenoTailSweepAttemptEvent args)
    {
        if (!xeno.Comp.Fortified)
            return;

        _popup.PopupClient(Loc.GetString("cm-xeno-fortify-cant-tail-sweep"), xeno, xeno);
        args.Cancelled = true;
    }

    private void OnXenoFortifyToggleCrestAttempt(Entity<XenoFortifyComponent> xeno, ref XenoToggleCrestAttemptEvent args)
    {
        if (!xeno.Comp.Fortified)
            return;

        _popup.PopupClient(Loc.GetString("cm-xeno-fortify-cant-toggle-crest"), xeno, xeno);
        args.Cancelled = true;
    }

    private void OnXenoFortifyMobStateChanged(Entity<XenoFortifyComponent> xeno, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Critical or MobState.Dead)
            Unfortify(xeno);
    }

    private void OnXenoFortifyRefreshSpeed(Entity<XenoFortifyComponent> xeno, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!xeno.Comp.CanMoveFortified || !xeno.Comp.Fortified)
            return;

        var modifier = xeno.Comp.MoveSpeedModifier.Float();
        args.ModifySpeed(modifier, modifier);
    }

    private void Fortify(Entity<XenoFortifyComponent> xeno)
    {
        xeno.Comp.Fortified = true;

        if (!xeno.Comp.CanMoveFortified)
        {
            _fixtures.TryCreateFixture(xeno, xeno.Comp.Shape, XenoFortifyComponent.FixtureId, hard: true, collisionLayer: (int)WallLayer);
            _transform.AnchorEntity((xeno, Transform(xeno)));
        }
        else
        {
            _speed.RefreshMovementSpeedModifiers(xeno.Owner);
        }

        FortifyUpdated(xeno);
    }

    private void Unfortify(Entity<XenoFortifyComponent> xeno)
    {
        if (!xeno.Comp.Fortified)
            return;

        xeno.Comp.Fortified = false;

        if (!xeno.Comp.CanMoveFortified)
        {
            _fixtures.DestroyFixture(xeno, XenoFortifyComponent.FixtureId);
            _transform.Unanchor(xeno, Transform(xeno));
            _physics.TrySetBodyType(xeno, BodyType.KinematicController);
        }
        else
        {
            _speed.RefreshMovementSpeedModifiers(xeno.Owner);
        }

        FortifyUpdated(xeno);
    }

    private void FortifyUpdated(Entity<XenoFortifyComponent> xeno)
    {
        _actionBlocker.UpdateCanMove(xeno);
        _appearance.SetData(xeno, XenoVisualLayers.Fortify, xeno.Comp.Fortified);
        Dirty(xeno);

        var ev = new XenoFortifiedEvent(xeno.Comp.Fortified);
        RaiseLocalEvent(xeno, ref ev);
    }
}
