using Content.Shared._RMC14.Xenonids.Fortify;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared._RMC14.Xenonids.Sweep;
using Content.Shared.Actions;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Crest;

public sealed partial class XenoCrestSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoCrestComponent, XenoCrestActionEvent>(OnXenoCrestAction);
        SubscribeLocalEvent<XenoCrestComponent, RefreshMovementSpeedModifiersEvent>(OnXenoCrestRefreshMovementSpeed);
        SubscribeLocalEvent<XenoCrestComponent, DamageModifyEvent>(OnXenoCrestDamageModify);
        SubscribeLocalEvent<XenoCrestComponent, KnockDownAttemptEvent>(OnXenoCrestKnockDownAttempt);

        SubscribeLocalEvent<XenoCrestComponent, XenoFortifyAttemptEvent>(OnXenoCrestFortifyAttempt);
        SubscribeLocalEvent<XenoCrestComponent, XenoTailSweepAttemptEvent>(OnXenoCrestTailSweepAttempt);
        SubscribeLocalEvent<XenoCrestComponent, XenoRestAttemptEvent>(OnXenoCrestRestAttempt);
    }

    private void OnXenoCrestAction(Entity<XenoCrestComponent> xeno, ref XenoCrestActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        var attempt = new XenoToggleCrestAttemptEvent();
        RaiseLocalEvent(xeno, ref attempt);
        if (attempt.Cancelled)
            return;

        args.Handled = true;
        SetCrest(xeno, !xeno.Comp.Lowered);
        _actions.SetToggled((EntityUid)args.Action, xeno.Comp.Lowered);
    }

    private void OnXenoCrestRefreshMovementSpeed(Entity<XenoCrestComponent> xeno, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (xeno.Comp.Lowered)
            args.ModifySpeed(xeno.Comp.SpeedMultiplier, xeno.Comp.SpeedMultiplier);
    }

    private void OnXenoCrestDamageModify(Entity<XenoCrestComponent> xeno, ref DamageModifyEvent args)
    {
        if (xeno.Comp.Lowered)
            args.Damage *= xeno.Comp.DamageModifier;
    }

    private void OnXenoCrestKnockDownAttempt(Entity<XenoCrestComponent> xeno, ref KnockDownAttemptEvent args)
    {
        if (xeno.Comp.Lowered)
            args.Cancelled = true;
    }

    private void OnXenoCrestFortifyAttempt(Entity<XenoCrestComponent> xeno, ref XenoFortifyAttemptEvent args)
    {
        if (!xeno.Comp.Lowered)
            return;

        _popup.PopupClient(Loc.GetString("cm-xeno-toggle-crest-cant-fortify"), xeno, xeno);
        args.Cancelled = true;
    }

    private void OnXenoCrestTailSweepAttempt(Entity<XenoCrestComponent> xeno, ref XenoTailSweepAttemptEvent args)
    {
        if (!xeno.Comp.Lowered)
            return;

        _popup.PopupClient(Loc.GetString("cm-xeno-toggle-crest-cant-tail-sweep"), xeno, xeno);
        args.Cancelled = true;
    }

    private void OnXenoCrestRestAttempt(Entity<XenoCrestComponent> xeno, ref XenoRestAttemptEvent args)
    {
        if (!xeno.Comp.Lowered)
            return;

        _popup.PopupClient(Loc.GetString("cm-xeno-toggle-crest-cant-rest"), xeno, xeno);
        args.Cancelled = true;
    }

    public void SetCrest(Entity<XenoCrestComponent> xeno, bool lowered)
    {
        if (xeno.Comp.Lowered == lowered)
            return;

        xeno.Comp.Lowered = lowered;
        Dirty(xeno);

        _movementSpeed.RefreshMovementSpeedModifiers(xeno.Owner);
        _appearance.SetData(xeno, XenoVisualLayers.Crest, lowered);
    }
}
