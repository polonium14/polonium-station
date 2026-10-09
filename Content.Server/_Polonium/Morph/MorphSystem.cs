using Content.Server.Administration.Logs;
using Content.Shared._Polonium.Morph;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Devour.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Polymorph.Components;
using Content.Shared.Polymorph.Systems;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Polonium.Morph;

public sealed partial class MorphSystem : SharedMorphSystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobThresholdSystem _threshold = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChameleonProjectorSystem _chameleon = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MorphComponent, MorphDisguiseActionEvent>(OnDisguiseAction);
        SubscribeLocalEvent<MorphComponent, MorphRevealActionEvent>(OnRevealAction);
        SubscribeLocalEvent<MorphComponent, MorphSpitActionEvent>(OnSpitAction);
        SubscribeLocalEvent<MorphComponent, MorphStomachSelectMessage>(OnStomachSelect);
        SubscribeLocalEvent<MorphComponent, MorphDigestDoAfterEvent>(OnDigestDoAfter);
        SubscribeLocalEvent<MorphComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MorphComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnDisguiseAction(Entity<MorphComponent> ent, ref MorphDisguiseActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanChangeForm(ent))
            return;

        var target = args.Target;
        if (ent.Comp.Projector is not { } projector
            || !TryComp<ChameleonProjectorComponent>(projector, out var proj))
            return;

        if (_container.IsEntityInContainer(ent)
            || _container.IsEntityInContainer(target)
            || _chameleon.IsInvalid(proj, target))
        {
            _popup.PopupEntity(Loc.GetString("morph-disguise-invalid"), target, ent);
            return;
        }

        args.Handled = true;

        _popup.PopupEntity(
            Loc.GetString("morph-disguise-self", ("target", target)),
            Loc.GetString("morph-disguise-others", ("morph", ent.Owner), ("target", target)),
            ent,
            ent,
            PopupType.MediumCaution);

        _chameleon.Disguise((projector, proj), ent, target);
        ent.Comp.NextDisguise = Timing.CurTime + ent.Comp.DisguiseCooldown;
        Dirty(ent);
    }

    private void OnRevealAction(Entity<MorphComponent> ent, ref MorphRevealActionEvent args)
    {
        if (args.Handled)
            return;

        if (!ent.Comp.Disguised)
        {
            _popup.PopupEntity(Loc.GetString("morph-reveal-already"), ent, ent);
            return;
        }

        if (!CanChangeForm(ent))
            return;

        args.Handled = true;

        _chameleon.TryReveal(ent.Owner);
        _popup.PopupEntity(
            Loc.GetString("morph-reveal-self"),
            Loc.GetString("morph-reveal-others", ("morph", ent.Owner)),
            ent,
            ent,
            PopupType.MediumCaution);
    }

    private bool CanChangeForm(Entity<MorphComponent> ent)
    {
        if (Timing.CurTime >= ent.Comp.NextDisguise)
            return true;

        _popup.PopupEntity(Loc.GetString("morph-disguise-cooldown"), ent, ent);
        return false;
    }

    private void OnSpitAction(Entity<MorphComponent> ent, ref MorphSpitActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.PreparedThrow is not { } item || !IsInStomach(ent, item))
        {
            ent.Comp.PreparedThrow = null;
            Dirty(ent);
            _popup.PopupEntity(Loc.GetString("morph-spit-nothing"), ent, ent);
            return;
        }

        args.Handled = true;

        _container.TryRemoveFromContainer(item);
        _throwing.TryThrow(item, args.Target, ent.Comp.ThrowSpeed, ent);
        _popup.PopupEntity(Loc.GetString("morph-spit-throw", ("morph", ent.Owner), ("item", item)), ent, PopupType.MediumCaution);
        _audio.PlayPvs(ent.Comp.SpitSound, ent);

        ent.Comp.PreparedThrow = null;
        Dirty(ent);
    }

    private void OnStomachSelect(Entity<MorphComponent> ent, ref MorphStomachSelectMessage args)
    {
        if (!TryGetEntity(args.Target, out var target) || !IsInStomach(ent, target.Value))
            return;

        switch (args.Action)
        {
            case MorphStomachAction.Drop:
                if (ent.Comp.PreparedThrow == target)
                    ent.Comp.PreparedThrow = null;

                _container.TryRemoveFromContainer(target.Value);
                _popup.PopupEntity(Loc.GetString("morph-spit-drop", ("morph", ent.Owner), ("item", target.Value)), ent, PopupType.MediumCaution);
                _audio.PlayPvs(ent.Comp.SpitSound, ent);
                break;

            case MorphStomachAction.PrepareThrow:
                ent.Comp.PreparedThrow = target;
                _popup.PopupEntity(Loc.GetString("morph-throw-prepared", ("item", target.Value)), ent, ent);
                break;

            case MorphStomachAction.Digest:
                StartDigest(ent, target.Value);
                break;
        }

        Dirty(ent);
    }

    private void StartDigest(Entity<MorphComponent> ent, EntityUid target)
    {
        if (HasComp<MobStateComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("morph-digest-start", ("target", target)), ent, ent);
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, ent.Comp.DigestTime, new MorphDigestDoAfterEvent(), ent, target: target)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
            });
            return;
        }

        if (_whitelist.IsWhitelistPass(ent.Comp.DigestBlacklist, target))
        {
            _popup.PopupEntity(Loc.GetString("morph-digest-fail", ("target", target)), ent, ent);
            return;
        }

        if (ent.Comp.PreparedThrow == target)
            ent.Comp.PreparedThrow = null;

        _popup.PopupEntity(Loc.GetString("morph-digest-item", ("target", target)), ent, ent);
        _audio.PlayPvs(ent.Comp.DigestItemSound, ent);
        QueueDel(target);
    }

    private void OnDigestDoAfter(Entity<MorphComponent> ent, ref MorphDigestDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target || !IsInStomach(ent, target))
            return;

        args.Handled = true;

        if (ent.Comp.PreparedThrow == target)
        {
            ent.Comp.PreparedThrow = null;
            Dirty(ent);
        }

        // out of the stomach first, otherwise the equipment would just drop back into it
        _container.TryRemoveFromContainer(target);
        if (_inventory.TryGetContainerSlotEnumerator(target, out var enumerator))
        {
            while (enumerator.NextItem(out _, out var slot))
            {
                _inventory.TryUnequip(target, target, slot.Name, true, true);
            }
        }
        _hands.DropAll(target, checkActionBlocker: false, doDropInteraction: false);

        if (_threshold.TryGetDeadThreshold(target, out var threshold))
            _damageable.HealEvenly(ent.Owner, -(threshold.Value * ent.Comp.DigestHealFraction));

        _adminLog.Add(LogType.Gib, LogImpact.High, $"{ToPrettyString(ent):player} digested {ToPrettyString(target):target} as a morph");

        _popup.PopupEntity(Loc.GetString("morph-digest-mob", ("target", target)), ent, ent, PopupType.Medium);
        _audio.PlayPvs(ent.Comp.DigestMobSound, ent);

        if (ent.Comp.DigestRemains is { } remains)
            Spawn(remains, Transform(ent).Coordinates);

        QueueDel(target);
    }

    private void OnMobStateChanged(Entity<MorphComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        _ui.CloseUi(ent.Owner, MorphStomachUiKey.Key);

        if (ent.Comp.Disguised)
        {
            _popup.PopupEntity(Loc.GetString("morph-death-disguised", ("morph", ent.Owner)), ent, PopupType.LargeCaution);
            _chameleon.TryReveal(ent.Owner);
        }

        if (_container.TryGetContainer(ent, DevourerComponent.StomachContainerId, out var stomach))
            _container.EmptyContainer(stomach);

        ent.Comp.PreparedThrow = null;
        Dirty(ent);
    }

    private void OnTerminating(Entity<MorphComponent> ent, ref EntityTerminatingEvent args)
    {
        // reveal before children get deleted, otherwise the projector deletes the already deleting disguise
        if (ent.Comp.Disguised)
            _chameleon.TryReveal(ent.Owner);
    }

    private bool IsInStomach(EntityUid morph, EntityUid target)
    {
        return _container.TryGetContainingContainer(target, out var container)
            && container.Owner == morph
            && container.ID == DevourerComponent.StomachContainerId;
    }
}
