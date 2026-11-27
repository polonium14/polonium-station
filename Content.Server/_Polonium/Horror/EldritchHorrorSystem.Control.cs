using Content.Shared._Polonium.Horror;
using Content.Shared._Polonium.Horror.Components;
using Content.Shared.BloodCult;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Polonium.Horror;

public sealed partial class EldritchHorrorSystem
{
    [Dependency] private IRobustRandom _random = default!;

    private void InitializeControl()
    {
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorTakeControlActionEvent>(OnTakeControlAction);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorBondDoAfterEvent>(OnBondDoAfter);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorReleaseControlActionEvent>(OnReleaseControlAction);

        SubscribeLocalEvent<EldritchHorrorInfestedComponent, HorrorControlAttemptEvent>(OnControlAttempt);
        SubscribeLocalEvent<EldritchHorrorInfestedComponent, DamageDealtEvent>(OnHostDamaged);
        SubscribeLocalEvent<EldritchHorrorInfestedComponent, MobStateChangedEvent>(OnHostStateChanged);

        SubscribeLocalEvent<HorrorCaptiveMindComponent, HorrorResistControlActionEvent>(OnResistAction);
    }

    private void OnTakeControlAction(Entity<EldritchHorrorComponent> ent, ref HorrorTakeControlActionEvent args)
    {
        if (args.Handled || !TryGetHost(ent, out var host))
            return;

        if (!CanControl(ent, host, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason, ("target", Identity.Entity(host, EntityManager))), ent, ent, PopupType.SmallCaution);
            return;
        }

        var time = ent.Comp.BondTime;
        foreach (var upgrade in GetUpgrades(ent))
            time -= upgrade.BondTimeReduction;

        var doAfter = new DoAfterArgs(EntityManager, ent, time, new HorrorBondDoAfterEvent(), ent, host)
        {
            // pressing the action again stops bonding
            CancelDuplicate = true,
            BreakOnMove = false,
            RequireCanInteract = false,
            Hidden = true,
        };

        args.Handled = true;
        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            _popup.PopupEntity(Loc.GetString("horror-bond-cancel"), ent, ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("horror-bond-start"), ent, ent);
    }

    /// <summary>
    /// Whether the horror can take the host's body: it must be alive, and a mindshield or a cult keeps the mind safe.
    /// </summary>
    public bool CanControl(Entity<EldritchHorrorComponent> ent, EntityUid host, out string reason)
    {
        reason = string.Empty;
        if (ent.Comp.Controlling)
        {
            reason = "horror-control-already";
            return false;
        }

        if (_mobState.IsDead(host))
        {
            reason = "horror-control-dead";
            return false;
        }

        var ev = new HorrorControlAttemptEvent(ent);
        RaiseLocalEvent(host, ref ev);
        if (ev.Cancelled)
        {
            reason = ev.Reason ?? "horror-control-blocked";
            return false;
        }

        return true;
    }

    private void OnControlAttempt(Entity<EldritchHorrorInfestedComponent> ent, ref HorrorControlAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (HasComp<MindShieldComponent>(ent))
        {
            args.Cancelled = true;
            args.Reason = "horror-control-mindshield";
        }
        else if (HasComp<BloodCultistComponent>(ent))
        {
            args.Cancelled = true;
            args.Reason = "horror-control-cult";
        }
    }

    private void OnBondDoAfter(Entity<EldritchHorrorComponent> ent, ref HorrorBondDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || ent.Comp.Host is not { } host)
            return;

        if (!CanControl(ent, host, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason, ("target", Identity.Entity(host, EntityManager))), ent, ent, PopupType.SmallCaution);
            return;
        }

        TakeControl(ent, host);
        args.Handled = true;
    }

    /// <summary>
    /// Moves the horror's mind into the host's body and locks the host's mind in a dummy inside it.
    /// </summary>
    public void TakeControl(Entity<EldritchHorrorComponent> ent, EntityUid host)
    {
        if (!TryComp<EldritchHorrorInfestedComponent>(host, out var infested)
            || !_mind.TryGetMind(ent, out var horrorMind, out _))
            return;

        var captive = Spawn(ent.Comp.CaptiveMindPrototype, MapCoordinates.Nullspace);
        if (!_container.Insert(captive, infested.CaptiveMindContainer))
        {
            Del(captive);
            return;
        }

        var captiveComp = EnsureComp<HorrorCaptiveMindComponent>(captive);
        captiveComp.Host = host;
        Dirty(captive, captiveComp);

        if (_mind.TryGetMind(host, out var hostMind, out _))
        {
            infested.HostMind = hostMind;
            _mind.TransferTo(hostMind, captive);
            _metaData.SetEntityName(captive, Loc.GetString("horror-captive-mind-name", ("name", Name(host))));
        }

        ent.Comp.Controlling = true;
        Dirty(ent);
        _mind.TransferTo(horrorMind, host);

        _popup.PopupEntity(Loc.GetString("horror-control-done"), host, host, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("horror-control-done-host"), captive, captive, PopupType.LargeCaution);

        var message = $"{ToPrettyString(ent)} took control of {ToPrettyString(host)}";
        _adminLog.Add(LogType.Mind, LogImpact.High, $"{ToPrettyString(ent):horror} took control of {ToPrettyString(host):host}");
        _chat.SendAdminAlert(message);

        RefreshActions(ent);
        UpdateSoulLocator(ent);
    }

    private void OnReleaseControlAction(Entity<EldritchHorrorComponent> ent, ref HorrorReleaseControlActionEvent args)
    {
        if (args.Handled || !ent.Comp.Controlling)
            return;

        ReleaseControl(ent);
        if (ent.Comp.Host is { } host)
            _popup.PopupEntity(Loc.GetString("horror-control-released"), ent, ent);
        args.Handled = true;
    }

    /// <summary>
    /// Gives the body back to its owner and puts the horror's mind back into the horror.
    /// </summary>
    public void ReleaseControl(Entity<EldritchHorrorComponent> ent)
    {
        if (!ent.Comp.Controlling || ent.Comp.Host is not { } host)
            return;

        ent.Comp.Controlling = false;
        Dirty(ent);

        if (_mind.TryGetMind(host, out var horrorMind, out _))
            _mind.TransferTo(horrorMind, ent);

        if (TryComp<EldritchHorrorInfestedComponent>(host, out var infested))
        {
            if (infested.HostMind is { } hostMind && !TerminatingOrDeleted(hostMind))
                _mind.TransferTo(hostMind, host);

            infested.HostMind = null;
            if (infested.CaptiveMindContainer.ContainedEntity is { } captive)
                QueueDel(captive);
        }

        _adminLog.Add(LogType.Mind, LogImpact.Medium, $"{ToPrettyString(ent):horror} released control of {ToPrettyString(host):host}");
        RefreshActions(ent);
        UpdateSoulLocator(ent);
    }

    /// <summary>
    /// A hard hit makes the host's brain snap back and take the body.
    /// </summary>
    private void OnHostDamaged(Entity<EldritchHorrorInfestedComponent> ent, ref DamageDealtEvent args)
    {
        // the damage lands after this event, so the total gets checked on the next update
        if (args.Damage.AnyPositive())
            ent.Comp.HitPending = true;
    }

    private void UpdateHitHosts()
    {
        var query = EntityQueryEnumerator<EldritchHorrorInfestedComponent>();
        while (query.MoveNext(out var uid, out var infested))
        {
            if (!infested.HitPending)
                continue;

            infested.HitPending = false;
            if (infested.Horror is not { } horror
                || !TryComp<EldritchHorrorComponent>(horror, out var comp)
                || !comp.Controlling
                || !TryComp<DamageableComponent>(uid, out var damageable)
                || !_damageable.TryGetDamageGreaterThan((uid, damageable), comp.ControlBreakDamage - FixedPoint2.Epsilon, out _))
                continue;

            _popup.PopupEntity(Loc.GetString("horror-control-hit-horror"), uid, uid, PopupType.MediumCaution);
            ReleaseControl((horror, comp));
            _popup.PopupEntity(Loc.GetString("horror-control-hit-host"), uid, uid, PopupType.MediumCaution);
        }
    }

    private void OnHostStateChanged(Entity<EldritchHorrorInfestedComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead
            || ent.Comp.Horror is not { } horror
            || !TryComp<EldritchHorrorComponent>(horror, out var comp))
            return;

        ReleaseControl((horror, comp));
    }

    private void OnResistAction(Entity<HorrorCaptiveMindComponent> ent, ref HorrorResistControlActionEvent args)
    {
        if (args.Handled || ent.Comp.Host is not { } host)
            return;

        args.Handled = true;
        if (ent.Comp.ResistEnd != null)
        {
            _popup.PopupEntity(Loc.GetString("horror-resist-already"), ent, ent, PopupType.SmallCaution);
            return;
        }

        var time = _random.Next(ent.Comp.ResistMin, ent.Comp.ResistMax);
        if (TryComp<EldritchHorrorInfestedComponent>(host, out var infested)
            && infested.Horror is { } horror
            && TryComp<EldritchHorrorComponent>(horror, out var comp))
        {
            foreach (var upgrade in GetUpgrades((horror, comp)))
            {
                if (upgrade.DeepControl)
                    time += _random.Next(ent.Comp.ResistMin, ent.Comp.ResistMax);
            }
        }

        ent.Comp.ResistEnd = _timing.CurTime + time;
        _popup.PopupEntity(Loc.GetString("horror-resist-start"), ent, ent, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("horror-resist-start-horror"), host, host, PopupType.MediumCaution);
    }

    private void UpdateCaptiveMinds(TimeSpan now)
    {
        var query = EntityQueryEnumerator<HorrorCaptiveMindComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ResistEnd is not { } end || now < end)
                continue;

            comp.ResistEnd = null;
            if (comp.Host is not { } host
                || !TryComp<EldritchHorrorInfestedComponent>(host, out var infested)
                || infested.Horror is not { } horror
                || !TryComp<EldritchHorrorComponent>(horror, out var horrorComp)
                || !horrorComp.Controlling)
                continue;

            _popup.PopupEntity(Loc.GetString("horror-resist-done-horror"), host, host, PopupType.MediumCaution);
            ReleaseControl((horror, horrorComp));
            _popup.PopupEntity(Loc.GetString("horror-resist-done"), host, host, PopupType.Large);
        }
    }
}
