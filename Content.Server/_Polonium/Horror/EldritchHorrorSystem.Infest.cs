using Content.Shared._Polonium.Horror;
using Content.Shared._Polonium.Horror.Components;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Body;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Medical.HealthAnalyzer;
using Content.Shared.MedicalScanner;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Polymorph;
using Content.Shared.Popups;

namespace Content.Server._Polonium.Horror;

public sealed partial class EldritchHorrorSystem
{
    private void InitializeInfest()
    {
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorInfestActionEvent>(OnInfestAction);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorInfestDoAfterEvent>(OnInfestDoAfter);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorLeaveHostActionEvent>(OnLeaveAction);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorLeaveHostDoAfterEvent>(OnLeaveDoAfter);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorScanHostActionEvent>(OnScanHost);

        SubscribeLocalEvent<EldritchHorrorInfestedComponent, ComponentInit>(OnInfestedInit);
        SubscribeLocalEvent<EldritchHorrorInfestedComponent, OrganRemovedFromEvent>(OnOrganRemoved);
        SubscribeLocalEvent<EldritchHorrorInfestedComponent, PolymorphedEvent>(OnPolymorphed);
        SubscribeLocalEvent<HorrorRemovalStepComponent, SurgeryStepEvent>(OnRemovalStep);
    }

    private void OnInfestedInit(Entity<EldritchHorrorInfestedComponent> ent, ref ComponentInit args)
    {
        ent.Comp.HorrorContainer = _container.EnsureContainer<Robust.Shared.Containers.ContainerSlot>(ent, EldritchHorrorInfestedComponent.HorrorContainerId);
        ent.Comp.CaptiveMindContainer = _container.EnsureContainer<Robust.Shared.Containers.ContainerSlot>(ent, EldritchHorrorInfestedComponent.CaptiveMindContainerId);
    }

    private void OnInfestAction(Entity<EldritchHorrorComponent> ent, ref HorrorInfestActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanInfest(ent, args.Target, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason, ("target", Identity.Entity(args.Target, EntityManager))), ent, ent, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.InfestTime, new HorrorInfestDoAfterEvent(), ent, args.Target)
        {
            DistanceThreshold = 1.5f,
            BreakOnDamage = true,
            BreakOnMove = true,
            AttemptFrequency = AttemptFrequency.StartAndEnd,
            Hidden = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("horror-infest-start", ("target", Identity.Entity(args.Target, EntityManager))), ent, ent);
        args.Handled = true;
    }

    /// <summary>
    /// Whether the horror can crawl into the target: a living-looking head with a brain and a mind behind it,
    /// or the body of the horror's current soul target.
    /// </summary>
    public bool CanInfest(Entity<EldritchHorrorComponent> ent, EntityUid target, out string reason)
    {
        reason = string.Empty;
        if (ent.Comp.Host != null)
        {
            reason = "horror-infest-already-host";
            return false;
        }

        if (target == ent.Owner || HasComp<EldritchHorrorComponent>(target) || !HasComp<MobStateComponent>(target))
        {
            reason = "horror-infest-invalid";
            return false;
        }

        if (HasComp<EldritchHorrorInfestedComponent>(target))
        {
            reason = "horror-infest-already-infested";
            return false;
        }

        if (!HasOrgan(target, HeadCategory))
        {
            reason = "horror-infest-no-head";
            return false;
        }

        if (!HasOrgan(target, BrainCategory))
        {
            reason = "horror-infest-no-brain";
            return false;
        }

        // only players: an empty or disconnected body can't host the horror, unless it's the soul it hunts
        var hasPlayer = _mind.TryGetMind(target, out _, out var mind)
                        && mind.UserId is { } userId
                        && _player.TryGetSessionById(userId, out _);
        if (!hasPlayer && !IsSoulTarget(ent, target))
        {
            reason = "horror-infest-no-mind";
            return false;
        }

        var ev = new HorrorInfestAttemptEvent(ent);
        RaiseLocalEvent(target, ref ev);
        if (ev.Cancelled)
        {
            reason = ev.Reason ?? "horror-infest-invalid";
            return false;
        }

        return true;
    }

    private bool HasOrgan(EntityUid body, Robust.Shared.Prototypes.ProtoId<OrganCategoryPrototype> category)
    {
        foreach (var organ in _body.EnumerateOrgans<OrganComponent>(body))
        {
            if (organ.Comp1.Category == category)
                return true;
        }

        return false;
    }

    private void OnInfestDoAfter(Entity<EldritchHorrorComponent> ent, ref HorrorInfestDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (!CanInfest(ent, target, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason, ("target", Identity.Entity(target, EntityManager))), ent, ent, PopupType.SmallCaution);
            return;
        }

        Infest(ent, target);
        args.Handled = true;
    }

    /// <summary>
    /// Puts the horror into the target's head, without any checks.
    /// </summary>
    public bool Infest(Entity<EldritchHorrorComponent> ent, EntityUid target)
    {
        var infested = EnsureComp<EldritchHorrorInfestedComponent>(target);
        if (!_container.Insert(ent.Owner, infested.HorrorContainer))
        {
            RemCompDeferred<EldritchHorrorInfestedComponent>(target);
            return false;
        }

        infested.Horror = ent;
        ent.Comp.Host = target;
        Dirty(target, infested);
        Dirty(ent);

        var revealEv = new HorrorRevealEvent();
        RaiseLocalEvent(ent, ref revealEv);

        _popup.PopupEntity(Loc.GetString("horror-infest-done-horror", ("target", Identity.Entity(target, EntityManager))), target, ent);
        _popup.PopupEntity(Loc.GetString("horror-infest-done-host"), target, target, PopupType.MediumCaution);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ent):horror} infested {ToPrettyString(target):host}");

        RefreshActions(ent);
        UpdateChemicalsUi(ent);
        return true;
    }

    private void OnLeaveAction(Entity<EldritchHorrorComponent> ent, ref HorrorLeaveHostActionEvent args)
    {
        if (args.Handled || !TryGetHost(ent, out var host))
            return;

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.LeaveTime, new HorrorLeaveHostDoAfterEvent(), ent, host)
        {
            // pressing the action again stops crawling out
            CancelDuplicate = true,
            BreakOnMove = false,
            RequireCanInteract = false,
            Hidden = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            _popup.PopupEntity(Loc.GetString("horror-leave-cancel"), ent, ent);
            args.Handled = true;
            return;
        }

        _popup.PopupEntity(Loc.GetString("horror-leave-start", ("target", Identity.Entity(host, EntityManager))), ent, ent);
        if (!_mobState.IsDead(host))
            _popup.PopupEntity(Loc.GetString("horror-leave-start-host"), host, host, PopupType.MediumCaution);

        args.Handled = true;
    }

    private void OnLeaveDoAfter(Entity<EldritchHorrorComponent> ent, ref HorrorLeaveHostDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || ent.Comp.Controlling || ent.Comp.Host is not { } host)
            return;

        if (!TryLeaveHost(ent))
            return;

        _popup.PopupEntity(Loc.GetString("horror-leave-done", ("target", Identity.Entity(host, EntityManager))), ent, ent);
        _popup.PopupEntity(Loc.GetString("horror-leave-done-host"), host, host, PopupType.MediumCaution);
        args.Handled = true;
    }

    /// <summary>
    /// Drops the horror out of its host next to it. Hands control back first.
    /// </summary>
    public bool TryLeaveHost(Entity<EldritchHorrorComponent> ent)
    {
        if (ent.Comp.Host is not { } host)
            return false;

        ReleaseControl(ent);

        if (TryComp<EldritchHorrorInfestedComponent>(host, out var infested) && _container.ContainsEntity(host, ent))
            _container.Remove(ent.Owner, infested.HorrorContainer);

        _alerts.ClearAlert(host, ent.Comp.SoulLocatorAlert);
        ent.Comp.Host = null;
        Dirty(ent);
        RemCompDeferred<EldritchHorrorInfestedComponent>(host);

        _ui.CloseUi(ent.Owner, HorrorChemicalsUiKey.Key);
        _ui.CloseUi(ent.Owner, HealthAnalyzerUiKey.Key);

        var ev = new HorrorLeftHostEvent(host);
        RaiseLocalEvent(ent, ref ev);

        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(ent):horror} left {ToPrettyString(host):host}");
        RefreshActions(ent);
        return true;
    }

    private void OnOrganRemoved(Entity<EldritchHorrorInfestedComponent> ent, ref OrganRemovedFromEvent args)
    {
        if (ent.Comp.Horror is not { } horror
            || !TryComp<EldritchHorrorComponent>(horror, out var comp)
            || !TryComp<OrganComponent>(args.Organ, out var organ)
            || organ.Category != HeadCategory && organ.Category != BrainCategory)
            return;

        TryLeaveHost((horror, comp));
    }

    private void OnPolymorphed(Entity<EldritchHorrorInfestedComponent> ent, ref PolymorphedEvent args)
    {
        if (ent.Comp.Horror is not { } horror || !TryComp<EldritchHorrorComponent>(horror, out var comp))
            return;

        var horrorEnt = (horror, comp);
        TryLeaveHost(horrorEnt);
        if (CanInfest(horrorEnt, args.NewEntity, out _))
            Infest(horrorEnt, args.NewEntity);
    }

    /// <summary>
    /// The last step of the removal surgery pulls the horror out of the brain.
    /// </summary>
    private void OnRemovalStep(Entity<HorrorRemovalStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryComp<EldritchHorrorInfestedComponent>(args.Body, out var infested)
            || infested.Horror is not { } horror
            || !TryComp<EldritchHorrorComponent>(horror, out var comp))
            return;

        if (!TryLeaveHost((horror, comp)))
            return;

        _popup.PopupEntity(Loc.GetString("horror-surgery-removed", ("target", Identity.Entity(args.Body, EntityManager))), args.Body, args.User);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(args.User):user} surgically removed {ToPrettyString(horror):horror} from {ToPrettyString(args.Body):host}");
    }
}

/// <summary>
/// Raised on a horror when it gets into a host and should drop its disguises.
/// </summary>
[ByRefEvent]
public record struct HorrorRevealEvent;

/// <summary>
/// Raised on a horror after it left its host, whatever the reason.
/// </summary>
[ByRefEvent]
public record struct HorrorLeftHostEvent(EntityUid Host);
