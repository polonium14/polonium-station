using Content.Server.Chat.Systems;
using Content.Shared._Polonium.Polish;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Stunnable;

namespace Content.Server._Polonium.Polish;

/// <summary>
/// Krzyżowanie: przybicie ciągniętej ofiary, bolesne zejście z krzyża i blokada zwykłego przypinania.
/// </summary>
public sealed partial class CrucifixSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrucifixComponent, StrapAttemptEvent>(OnStrapAttempt);
        SubscribeLocalEvent<CrucifixComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        SubscribeLocalEvent<CrucifixComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<CrucifixComponent, CrucifyDoAfterEvent>(OnCrucifyDoAfter);
        SubscribeLocalEvent<CrucifixComponent, CrucifixFreeDoAfterEvent>(OnFreeDoAfter);
    }

    private void OnStrapAttempt(Entity<CrucifixComponent> ent, ref StrapAttemptEvent args)
    {
        // Na krzyż nie da się po prostu usiąść ani nikogo przeciągnąć - tylko przez przybicie.
        if (!ent.Comp.Bypass)
            args.Cancelled = true;
    }

    private void OnUnstrapAttempt(Entity<CrucifixComponent> ent, ref UnstrapAttemptEvent args)
    {
        if (args.User is not { } user)
            return;

        args.Cancelled = true;
        if (args.Popup)
            TryStartFreeing(ent, user, args.Buckle);
    }

    private void OnInteractHand(Entity<CrucifixComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !TryComp<StrapComponent>(ent, out var strap))
            return;

        var user = args.User;

        if (strap.BuckledEntities.Count > 0)
        {
            args.Handled = true;
            foreach (var buckled in strap.BuckledEntities)
            {
                TryStartFreeing(ent, user, buckled);
            }
            return;
        }

        if (!TryComp<PullerComponent>(user, out var puller) || puller.Pulling is not { } victim || !CanBeCrucified(victim))
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.CrucifyDelay, new CrucifyDoAfterEvent(), ent, target: victim, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnCrucifyDoAfter(Entity<CrucifixComponent> ent, ref CrucifyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } victim)
            return;

        if (!TryComp<StrapComponent>(ent, out var strap) || strap.BuckledEntities.Count > 0)
            return;

        if (!TryComp<PullerComponent>(args.User, out var puller) || puller.Pulling != victim || !CanBeCrucified(victim))
            return;

        args.Handled = true;

        if (TryComp<PullableComponent>(victim, out var pullable))
            _pulling.TryStopPull(victim, pullable, args.User);

        ent.Comp.Bypass = true;
        var buckled = _buckle.TryBuckle(victim, args.User, ent, popup: false);
        ent.Comp.Bypass = false;

        if (!buckled)
            return;

        _popup.PopupEntity(
            Loc.GetString("crucifix-crucified-victim", ("user", Identity.Entity(args.User, EntityManager))),
            Loc.GetString("crucifix-crucified-others", ("user", Identity.Entity(args.User, EntityManager)), ("victim", Identity.Entity(victim, EntityManager))),
            victim,
            victim,
            PopupType.LargeCaution);
        _chat.TryEmoteWithChat(victim, ent.Comp.ScreamEmote, ignoreActionBlocker: true);
        _damageable.TryChangeDamage(victim, ent.Comp.Damage, origin: args.User);
    }

    private void TryStartFreeing(Entity<CrucifixComponent> ent, EntityUid user, EntityUid victim)
    {
        var self = user == victim;
        var doAfter = new DoAfterArgs(EntityManager, user, self ? ent.Comp.FreeSelfDelay : ent.Comp.FreeOtherDelay, new CrucifixFreeDoAfterEvent(), ent, target: victim, used: ent)
        {
            BreakOnMove = !self,
            BreakOnDamage = false,
            CancelDuplicate = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        if (self)
        {
            _popup.PopupEntity(
                Loc.GetString("crucifix-free-self"),
                Loc.GetString("crucifix-free-self-others", ("victim", Identity.Entity(victim, EntityManager))),
                victim,
                victim,
                PopupType.MediumCaution);
            _damageable.TryChangeDamage(victim, ent.Comp.Damage);
        }
        else
        {
            _popup.PopupEntity(
                Loc.GetString("crucifix-free-other-victim", ("user", Identity.Entity(user, EntityManager))),
                Loc.GetString("crucifix-free-other-others", ("user", Identity.Entity(user, EntityManager)), ("victim", Identity.Entity(victim, EntityManager))),
                victim,
                victim,
                PopupType.MediumCaution);
        }
    }

    private void OnFreeDoAfter(Entity<CrucifixComponent> ent, ref CrucifixFreeDoAfterEvent args)
    {
        if (args.Handled || args.Target is not { } victim)
            return;

        if (!TryComp<BuckleComponent>(victim, out var buckle) || buckle.BuckledTo != ent.Owner)
            return;

        args.Handled = true;

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("crucifix-free-failed"), victim, args.User);
            return;
        }

        _damageable.TryChangeDamage(victim, ent.Comp.Damage);
        _buckle.Unbuckle((victim, buckle), args.User);
        _popup.PopupEntity(Loc.GetString("crucifix-fall", ("victim", Identity.Entity(victim, EntityManager)), ("crucifix", ent.Owner)), victim, PopupType.MediumCaution);
        _chat.TryEmoteWithChat(victim, ent.Comp.ScreamEmote, ignoreActionBlocker: true);
        _stun.TryAddParalyzeDuration(victim, ent.Comp.FallParalyze);
    }

    private bool CanBeCrucified(EntityUid victim)
    {
        return HasComp<MobStateComponent>(victim)
               && HasComp<BuckleComponent>(victim)
               && !HasComp<BorgChassisComponent>(victim);
    }
}
