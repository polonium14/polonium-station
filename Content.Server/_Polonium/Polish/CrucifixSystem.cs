using Content.Server.Buckle.Systems;
using Content.Server.Chat.Systems;
using Content.Shared._Polonium.Polish;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Stunnable;

namespace Content.Server._Polonium.Polish;

/// <summary>
/// Krzyżowanie: przybicie ciągniętej ofiary, bolesne zejście z krzyża i blokada zwykłego przypinania.
/// </summary>
public sealed partial class CrucifixSystem : SharedCrucifixSystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrucifixComponent, InteractHandEvent>(OnInteractHand, before: [typeof(BuckleSystem)]);
        SubscribeLocalEvent<CrucifixComponent, DragDropTargetEvent>(OnDragDropTarget);
        SubscribeLocalEvent<CrucifixComponent, CrucifyDoAfterEvent>(OnCrucifyDoAfter);
        SubscribeLocalEvent<CrucifixComponent, CrucifixFreeDoAfterEvent>(OnFreeDoAfter);
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

        if (!TryComp<PullerComponent>(user, out var puller) || puller.Pulling is not { } victim)
            return;

        args.Handled = TryStartCrucifying(ent, user, victim);
    }

    private void OnDragDropTarget(Entity<CrucifixComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || args.Dragged == args.User)
            return;

        args.Handled = TryStartCrucifying(ent, args.User, args.Dragged);
    }

    private bool TryStartCrucifying(Entity<CrucifixComponent> ent, EntityUid user, EntityUid victim)
    {
        if (!TryComp<StrapComponent>(ent, out var strap) || strap.BuckledEntities.Count > 0 || !CanBeCrucified(victim))
            return false;

        if (!IsHelpless(victim))
        {
            _popup.PopupEntity(Loc.GetString("crucifix-not-restrained", ("victim", Identity.Entity(victim, EntityManager))), victim, user);
            return true;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.CrucifyDelay, new CrucifyDoAfterEvent(), ent, target: victim, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };
        return _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnCrucifyDoAfter(Entity<CrucifixComponent> ent, ref CrucifyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } victim)
            return;

        if (!TryComp<StrapComponent>(ent, out var strap) || strap.BuckledEntities.Count > 0)
            return;

        if (!CanBeCrucified(victim) || !IsHelpless(victim))
            return;

        args.Handled = true;

        ent.Comp.Bypass = true;
        var buckled = _buckle.TryBuckle(victim, args.User, ent, popup: false);
        ent.Comp.Bypass = false;

        if (!buckled)
            return;

        if (TryComp<PullableComponent>(victim, out var pullable))
            _pulling.TryStopPull(victim, pullable, args.User);

        _popup.PopupEntity(
            Loc.GetString("crucifix-crucified-victim", ("user", Identity.Entity(args.User, EntityManager))),
            Loc.GetString("crucifix-crucified-others", ("user", Identity.Entity(args.User, EntityManager)), ("victim", Identity.Entity(victim, EntityManager))),
            victim,
            victim,
            PopupType.LargeCaution);
        Scream(ent, victim);
        _damageable.TryChangeDamage(victim, ent.Comp.Damage, origin: args.User);
    }

    protected override void TryStartFreeing(Entity<CrucifixComponent> ent, EntityUid user, EntityUid victim)
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
        Scream(ent, victim);
        _stun.TryAddParalyzeDuration(victim, ent.Comp.FallParalyze);
    }

    private void Scream(Entity<CrucifixComponent> ent, EntityUid victim)
    {
        if (_mobState.IsAlive(victim))
            _chat.TryEmoteWithChat(victim, ent.Comp.ScreamEmote, ignoreActionBlocker: true);
    }

    private bool CanBeCrucified(EntityUid victim)
    {
        return HasComp<MobStateComponent>(victim)
               && HasComp<BuckleComponent>(victim)
               && !HasComp<BorgChassisComponent>(victim);
    }

    private bool IsHelpless(EntityUid victim)
    {
        return !_mobState.IsAlive(victim)
               || TryComp<CuffableComponent>(victim, out var cuffable) && !cuffable.CanStillInteract;
    }
}
