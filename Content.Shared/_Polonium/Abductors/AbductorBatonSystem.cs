using Content.Shared.Bed.Sleep;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Polonium.Abductors;

public sealed partial class AbductorBatonSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCuffableSystem _cuffs = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AbductorBatonComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<AbductorBatonComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<AbductorBatonComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<AbductorBatonComponent, AbductorCuffDoAfterEvent>(OnCuffDoAfter);
        SubscribeLocalEvent<AbductorBatonComponent, AbductorMarkDoAfterEvent>(OnMarkDoAfter);
    }

    /// <summary>
    /// Whether the target is in no state to resist: stunned, knocked down, asleep, cuffed or in critical condition.
    /// </summary>
    public bool IsSubdued(EntityUid target)
    {
        return HasComp<StunnedComponent>(target)
               || TryComp<KnockedDownComponent>(target, out var knocked) && knocked.AutoStand
               || HasComp<SleepingComponent>(target)
               || TryComp<CuffableComponent>(target, out var cuffable) && _cuffs.IsCuffed((target, cuffable))
               || _mobState.IsIncapacitated(target);
    }

    private void OnUseInHand(Entity<AbductorBatonComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!HasComp<AbductorComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("abductor-unfamiliar"), ent, args.User);
            return;
        }

        ent.Comp.Mode = ent.Comp.Mode switch
        {
            AbductorBatonMode.Stun => AbductorBatonMode.Sleep,
            AbductorBatonMode.Sleep => AbductorBatonMode.Cuff,
            AbductorBatonMode.Cuff => AbductorBatonMode.Mark,
            _ => AbductorBatonMode.Stun,
        };
        Dirty(ent);

        var prefix = HeldPrefix(ent.Comp.Mode);
        _appearance.SetData(ent, AbductorBatonVisuals.Mode, ent.Comp.Mode);
        _item.SetHeldPrefix(ent, prefix);
        _clothing.SetEquippedPrefix(ent, prefix);
        _audio.PlayPredicted(ent.Comp.ModeSound, ent, args.User);
        _popup.PopupEntity(Loc.GetString("abductor-baton-mode", ("mode", ModeName(ent.Comp.Mode))), ent, args.User);
    }

    private void OnExamined(Entity<AbductorBatonComponent> ent, ref ExaminedEvent args)
    {
        if (HasComp<AbductorComponent>(args.Examiner))
            args.PushMarkup(Loc.GetString("abductor-baton-examine", ("mode", ModeName(ent.Comp.Mode))));
    }

    private void OnMeleeHit(Entity<AbductorBatonComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        if (!HasComp<AbductorComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("abductor-unfamiliar"), ent, args.User);
            return;
        }

        var staminaTargets = 0;
        foreach (var target in args.HitEntities)
        {
            if (IsBatonTarget(target, args.User) && HasComp<StaminaComponent>(target))
                staminaTargets++;
        }

        foreach (var target in args.HitEntities)
        {
            if (!IsBatonTarget(target, args.User))
                continue;

            switch (ent.Comp.Mode)
            {
                case AbductorBatonMode.Stun:
                    if (HasComp<StaminaComponent>(target))
                        _stamina.TakeStaminaDamage(target, ent.Comp.StunStamina / staminaTargets, source: args.User, with: ent);
                    break;
                case AbductorBatonMode.Sleep:
                    TrySleep(ent, target, args.User);
                    break;
                case AbductorBatonMode.Cuff:
                    TryStartCuffing(ent, target, args.User);
                    break;
                case AbductorBatonMode.Mark:
                    TryStartMarking(ent, target, args.User);
                    break;
            }
        }
    }

    private bool IsBatonTarget(EntityUid target, EntityUid user)
    {
        return target != user && !HasComp<AbductorComponent>(target) && HasComp<MobStateComponent>(target);
    }

    private void TrySleep(Entity<AbductorBatonComponent> ent, EntityUid target, EntityUid user)
    {
        if (!IsSubdued(target))
        {
            _popup.PopupEntity(Loc.GetString("abductor-baton-sleep-needs-subdued"), target, user);
            return;
        }

        _status.TryUpdateStatusEffectDuration(target, SleepingSystem.StatusEffectForcedSleeping, ent.Comp.SleepDuration);
    }

    private void TryStartCuffing(Entity<AbductorBatonComponent> ent, EntityUid target, EntityUid user)
    {
        if (!TryComp<CuffableComponent>(target, out var cuffable) || _cuffs.IsCuffed((target, cuffable)))
        {
            _popup.PopupEntity(Loc.GetString("abductor-baton-cannot-cuff"), target, user);
            return;
        }

        StartDoAfter(ent, target, user, ent.Comp.CuffDelay, new AbductorCuffDoAfterEvent());
    }

    private void TryStartMarking(Entity<AbductorBatonComponent> ent, EntityUid target, EntityUid user)
    {
        if (!IsSubdued(target))
        {
            _popup.PopupEntity(Loc.GetString("abductor-baton-mark-needs-subdued"), target, user);
            return;
        }

        StartDoAfter(ent, target, user, ent.Comp.MarkDelay, new AbductorMarkDoAfterEvent());
    }

    private void StartDoAfter(Entity<AbductorBatonComponent> ent, EntityUid target, EntityUid user, TimeSpan delay, DoAfterEvent ev)
    {
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, delay, ev, ent, target, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnCuffDoAfter(Entity<AbductorBatonComponent> ent, ref AbductorCuffDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        var cuffs = SpawnAtPosition(ent.Comp.Cuffs, Transform(target).Coordinates);
        if (!_cuffs.TryAddNewCuffs(target, args.User, cuffs))
            QueueDel(cuffs);
    }

    private void OnMarkDoAfter(Entity<AbductorBatonComponent> ent, ref AbductorMarkDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        var ev = new AbductorSpecimenMarkedEvent(target);
        RaiseLocalEvent(args.User, ref ev);
    }

    private static string HeldPrefix(AbductorBatonMode mode)
    {
        return mode switch
        {
            AbductorBatonMode.Sleep => "sleep",
            AbductorBatonMode.Cuff => "cuffs",
            AbductorBatonMode.Mark => "damage",
            _ => "stun",
        };
    }

    private string ModeName(AbductorBatonMode mode)
    {
        return Loc.GetString(mode switch
        {
            AbductorBatonMode.Sleep => "abductor-baton-mode-sleep",
            AbductorBatonMode.Cuff => "abductor-baton-mode-cuff",
            AbductorBatonMode.Mark => "abductor-baton-mode-mark",
            _ => "abductor-baton-mode-stun",
        });
    }
}
