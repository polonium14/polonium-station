using System.Linq;
using Content.Shared._Polonium.Horror;
using Content.Shared._Polonium.Horror.Components;
using Content.Server.Objectives.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Random;

namespace Content.Server._Polonium.Horror;

public sealed partial class EldritchHorrorSystem
{
    [Dependency] private SharedJobSystem _job = default!;
    [Dependency] private CounterConditionSystem _counterCondition = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private void InitializeSouls()
    {
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorSeekSoulActionEvent>(OnSeekSoul);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorPickSoulMessage>(OnPickSoul);
        SubscribeLocalEvent<EldritchHorrorComponent, BoundUIClosedEvent>(OnSoulUiClosed);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorConsumeSoulActionEvent>(OnConsumeSoul);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorConsumeSoulDoAfterEvent>(OnConsumeSoulDoAfter);
    }

    private void OnSeekSoul(Entity<EldritchHorrorComponent> ent, ref HorrorSeekSoulActionEvent args)
    {
        if (args.Handled)
            return;

        var candidates = GetSoulCandidates(ent);
        if (candidates.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("horror-seek-soul-none"), ent, ent, PopupType.SmallCaution);
            return;
        }

        _random.Shuffle(candidates);
        ent.Comp.SoulCandidates = candidates.Take(ent.Comp.SoulChoices).ToList();

        var offered = new List<HorrorSoulCandidate>();
        foreach (var mindId in ent.Comp.SoulCandidates)
        {
            var mind = Comp<MindComponent>(mindId);
            string? job = _job.MindTryGetJobName(mindId, out var jobName) ? jobName : null;
            offered.Add(new HorrorSoulCandidate(GetNetEntity(mindId),
                mind.CharacterName ?? Name(mind.OwnedEntity!.Value),
                job));
        }

        _ui.SetUiState(ent.Owner, HorrorSoulUiKey.Key, new HorrorSoulBoundUserInterfaceState(offered));
        _ui.OpenUi(ent.Owner, HorrorSoulUiKey.Key, ent);
        args.Handled = true;
    }

    /// <summary>
    /// Living people with a soul left, other than the horror's master.
    /// </summary>
    private List<EntityUid> GetSoulCandidates(Entity<EldritchHorrorComponent> ent)
    {
        var list = new List<EntityUid>();
        var master = CompOrNull<HorrorServantComponent>(ent)?.Master;
        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var mindId, out var mind))
        {
            if (mind.OwnedEntity is not { } body
                || body == ent.Owner
                || mindId == ent.Comp.SoulTarget
                || HasComp<HorrorSoullessComponent>(mindId)
                || !HasComp<HumanoidProfileComponent>(body)
                || !HasComp<MobStateComponent>(body)
                || _mobState.IsDead(body)
                || master != null && mindId == master)
                continue;

            list.Add(mindId);
        }

        return list;
    }

    private void OnPickSoul(Entity<EldritchHorrorComponent> ent, ref HorrorPickSoulMessage args)
    {
        if (args.Actor != ent.Owner)
            return;

        var mind = GetEntity(args.Mind);
        if (!ent.Comp.SoulCandidates.Contains(mind))
            return;

        SetSoulTarget(ent, mind);
        _ui.CloseUi(ent.Owner, HorrorSoulUiKey.Key);
    }

    private void OnSoulUiClosed(Entity<EldritchHorrorComponent> ent, ref BoundUIClosedEvent args)
    {
        // closing the window without a pick still takes one of the offered souls, like in Aquila
        if (args.UiKey is not HorrorSoulUiKey || ent.Comp.SoulCandidates.Count == 0)
            return;

        SetSoulTarget(ent, _random.Pick(ent.Comp.SoulCandidates));
    }

    /// <summary>
    /// Makes the horror hunt the soul of the given mind and points the locator at it.
    /// </summary>
    public void SetSoulTarget(Entity<EldritchHorrorComponent> ent, EntityUid mind)
    {
        ent.Comp.SoulCandidates.Clear();
        ent.Comp.SoulTarget = mind;
        Dirty(ent);

        var name = CompOrNull<MindComponent>(mind)?.CharacterName ?? Loc.GetString("horror-seek-soul-unknown");
        var job = _job.MindTryGetJobName(mind, out var jobName) ? jobName : Loc.GetString("horror-seek-soul-unknown");
        _popup.PopupEntity(Loc.GetString("horror-seek-soul-picked", ("name", name), ("job", job)), ent, ent, PopupType.Medium);
        UpdateSoulLocator(ent);
    }

    /// <summary>
    /// Points the locator alert at the hunted soul's body. The alert sits where the horror's player is:
    /// on the horror, or on the host while the horror drives it.
    /// </summary>
    private void UpdateSoulLocator(Entity<EldritchHorrorComponent> ent)
    {
        var viewer = ent.Comp.Controlling && ent.Comp.Host is { } controlled ? controlled : ent.Owner;
        if (viewer != ent.Owner)
            _alerts.ClearAlert(ent.Owner, ent.Comp.SoulLocatorAlert);
        else if (ent.Comp.Host is { } host)
            _alerts.ClearAlert(host, ent.Comp.SoulLocatorAlert);

        if (ent.Comp.SoulTarget is not { } mindId)
        {
            _alerts.ClearAlert(viewer, ent.Comp.SoulLocatorAlert);
            return;
        }

        var distance = HorrorSoulDistance.Unknown;
        var arrow = ent.Comp.SoulArrow;
        if (TryComp<MindComponent>(mindId, out var mind)
            && mind.OwnedEntity is { } body
            && Transform(body).MapID == Transform(ent).MapID)
        {
            var vec = _xform.GetWorldPosition(body) - _xform.GetWorldPosition(ent);
            var length = vec.Length();
            arrow = vec.ToWorldAngle();
            if (length <= ent.Comp.SoulReachedDistance)
                distance = HorrorSoulDistance.Reached;
            else if (length <= ent.Comp.SoulCloseDistance)
                distance = HorrorSoulDistance.Close;
            else if (length <= ent.Comp.SoulMediumDistance)
                distance = HorrorSoulDistance.Medium;
            else
                distance = HorrorSoulDistance.Far;
        }

        if (distance != ent.Comp.SoulDistance || !arrow.EqualsApprox(ent.Comp.SoulArrow))
        {
            ent.Comp.SoulDistance = distance;
            ent.Comp.SoulArrow = arrow;
            Dirty(ent);
        }

        _alerts.ShowAlert(viewer, ent.Comp.SoulLocatorAlert);
    }

    private void OnConsumeSoul(Entity<EldritchHorrorComponent> ent, ref HorrorConsumeSoulActionEvent args)
    {
        if (args.Handled || !TryGetHost(ent, out var host))
            return;

        if (!CanConsumeSoul(ent, host, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), ent, ent, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.ConsumeTime, new HorrorConsumeSoulDoAfterEvent(), ent, host)
        {
            BreakOnMove = false,
            RequireCanInteract = false,
            CancelDuplicate = true,
        };

        args.Handled = true;
        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("horror-consume-start", ("target", IdentityOf(host))), ent, ent);
    }

    private bool CanConsumeSoul(Entity<EldritchHorrorComponent> ent, EntityUid host, out string reason)
    {
        reason = string.Empty;
        if (!_mind.TryGetMind(host, out var mind, out _) || HasComp<HorrorSoullessComponent>(mind))
        {
            reason = "horror-consume-no-soul";
            return false;
        }

        if (CompOrNull<HorrorServantComponent>(ent)?.Master == mind)
        {
            reason = "horror-consume-master";
            return false;
        }

        if (mind != ent.Comp.SoulTarget)
        {
            reason = "horror-consume-not-target";
            return false;
        }

        return true;
    }

    private void OnConsumeSoulDoAfter(Entity<EldritchHorrorComponent> ent, ref HorrorConsumeSoulDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || ent.Comp.Host is not { } host || !CanConsumeSoul(ent, host, out _))
            return;

        args.Handled = true;
        _mind.TryGetMind(host, out var mind, out _);
        EnsureComp<HorrorSoullessComponent>(mind);

        if (_mind.TryGetMind(ent, out var horrorMind, out var horrorMindComp))
        {
            foreach (var objective in _mind.EnumerateObjectives<HorrorDevourConditionComponent>((horrorMind, horrorMindComp)))
                _counterCondition.IncreaseCount(objective);
        }

        ent.Comp.ConsumedSouls++;
        ent.Comp.SoulPoints++;
        ent.Comp.SoulTarget = null;
        Dirty(ent);
        UpdateSoulLocator(ent);
        UpdateMutationsUi(ent);

        _popup.PopupEntity(Loc.GetString("horror-consume-done", ("target", IdentityOf(host))), ent, ent, PopupType.Large);
        _popup.PopupEntity(Loc.GetString("horror-consume-done-host"), host, host, PopupType.LargeCaution);
        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(ent):horror} devoured the soul of {ToPrettyString(host):host}");
    }
}
