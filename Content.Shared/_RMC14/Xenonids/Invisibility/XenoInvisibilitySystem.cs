using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Xenonids.Charge;
using Content.Shared._RMC14.Xenonids.Crest;
using Content.Shared._RMC14.Xenonids.Fling;
using Content.Shared._RMC14.Xenonids.Headbutt;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared._RMC14.Xenonids.Lunge;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Punch;
using Content.Shared._RMC14.Xenonids.Screech;
using Content.Shared._RMC14.Xenonids.Spit;
using Content.Shared._RMC14.Xenonids.Stab;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Invisibility;

public sealed class XenoInvisibilitySystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly XenoPlasmaSystem _plasma = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoInvisibilityComponent, XenoInvisibilityActionEvent>(OnInvisibility);

        SubscribeLocalEvent<XenoInvisibilityComponent, XenoTailStabActionEvent>(OnAttackAction);
        SubscribeLocalEvent<XenoInvisibilityComponent, XenoLeapActionEvent>(OnAttackAction);
        SubscribeLocalEvent<XenoInvisibilityComponent, XenoSpitActionEvent>(OnAttackAction);
        SubscribeLocalEvent<XenoInvisibilityComponent, XenoCrestActionEvent>(OnAttackAction);
        SubscribeLocalEvent<XenoInvisibilityComponent, XenoHeadbuttActionEvent>(OnAttackAction);
        SubscribeLocalEvent<XenoInvisibilityComponent, XenoChargeActionEvent>(OnAttackAction);
        SubscribeLocalEvent<XenoInvisibilityComponent, XenoPunchActionEvent>(OnAttackAction);
        SubscribeLocalEvent<XenoInvisibilityComponent, XenoLungeActionEvent>(OnAttackAction);
        SubscribeLocalEvent<XenoInvisibilityComponent, XenoFlingActionEvent>(OnAttackAction);
        SubscribeLocalEvent<XenoInvisibilityComponent, XenoScreechActionEvent>(OnAttackAction);
        SubscribeLocalEvent<XenoInvisibilityComponent, XenoCorrosiveAcidEvent>(OnAttackAction);

        SubscribeLocalEvent<MeleeWeaponComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnInvisibility(Entity<XenoInvisibilityComponent> xeno, ref XenoInvisibilityActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        args.Handled = true;

        if (xeno.Comp.Active)
        {
            SetActive(xeno, false);
            _actions.SetToggled((EntityUid)args.Action, false);
            return;
        }

        if (!TryComp<XenoPlasmaComponent>(xeno, out var plasma) || plasma.Plasma <= FixedPoint2.Zero)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-not-enough-plasma"), xeno, xeno, PopupType.MediumCaution);
            return;
        }

        SetActive(xeno, true);
        _actions.SetToggled((EntityUid)args.Action, true);
    }

    private void OnAttackAction<T>(Entity<XenoInvisibilityComponent> xeno, ref T args) where T : notnull
    {
        if (_timing.ApplyingState)
            return;

        Deactivate(xeno);
    }

    private void OnMeleeHit(Entity<MeleeWeaponComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        if (!TryComp<XenoInvisibilityComponent>(args.User, out var invis) || !invis.Active)
            return;

        Deactivate((args.User, invis));
    }

    private void SetActive(Entity<XenoInvisibilityComponent> xeno, bool active)
    {
        if (xeno.Comp.Active == active)
            return;

        xeno.Comp.Active = active;
        Dirty(xeno);

        var stealth = EnsureComp<StealthComponent>(xeno);
        _stealth.SetEnabled(xeno, active, stealth);
        if (active)
            _stealth.SetVisibility(xeno, xeno.Comp.CloakVisibility, stealth);
        else
            _stealth.SetVisibility(xeno, stealth.MaxVisibility, stealth);
    }

    public void Deactivate(Entity<XenoInvisibilityComponent> xeno)
    {
        if (!xeno.Comp.Active)
            return;

        SetActive(xeno, false);
        SetActionToggled(xeno.Owner, false);
    }

    private void SetActionToggled(EntityUid xeno, bool toggled)
    {
        foreach (var action in _actions.GetActions(xeno))
        {
            if (!TryComp<InstantActionComponent>(action, out var instant))
                continue;

            if (instant.Event is XenoInvisibilityActionEvent)
            {
                _actions.SetToggled(action.Owner, toggled);
                return;
            }
        }
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<XenoInvisibilityComponent, XenoPlasmaComponent>();
        while (query.MoveNext(out var uid, out var invis, out var plasma))
        {
            if (!invis.Active || invis.MaxCloakDuration <= TimeSpan.Zero || plasma.MaxPlasma <= FixedPoint2.Zero)
                continue;

            var drainPerSecond = plasma.MaxPlasma / (float)invis.MaxCloakDuration.TotalSeconds;
            var drain = FixedPoint2.New(drainPerSecond * frameTime);
            if (drain <= FixedPoint2.Zero)
                continue;

            _plasma.RemovePlasma((uid, plasma), drain);

            if (plasma.Plasma <= FixedPoint2.Zero)
                Deactivate((uid, invis));
        }
    }
}
