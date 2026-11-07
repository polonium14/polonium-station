using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Drunk;
using Content.Shared.Examine;
using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.Forensics.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Medical;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Random.Helpers;
using Content.Shared.StatusEffectNew;
using Content.Shared.UserInterface;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Breathalyzer;

public sealed partial class BreathalyzerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private IngestionSystem _ingestion = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCuffableSystem _cuffable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ForensicsSystem _forensics = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private VomitSystem _vomit = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BreathalyzerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BreathalyzerComponent, UseInHandEvent>(OnUseInHand, before: [typeof(ActivatableUISystem)]);
        SubscribeLocalEvent<BreathalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<BreathalyzerComponent, BreathalyzerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<BreathalyzerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BreathalyzerComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<BreathalyzerComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<BreathalyzerComponent, BreathalyzerPrintMessage>(OnPrint);
        SubscribeLocalEvent<CleansForensicsComponent, CleanForensicsDoAfterEvent>(OnCleaned);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<BreathalyzerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.State != BreathalyzerState.Analyzing || _timing.CurTime < comp.AnalysisEnd)
                continue;

            comp.State = BreathalyzerState.Result;
            comp.LastResult = comp.PendingResult;
            comp.LastSubjectName = comp.PendingSubjectName;
            comp.LastMeasuredRoundTime = _ticker.RoundDuration();
            Dirty(uid, comp);

            var level = GetLevel(comp, comp.PendingResult);
            UpdateScreen((uid, comp));
            _audio.PlayPvs(level?.Sound, uid);
        }
    }

    public BreathalyzerLevel? GetLevel(BreathalyzerComponent comp, float perMille)
    {
        BreathalyzerLevel? result = null;
        foreach (var level in comp.Levels)
        {
            if (perMille < level.Threshold)
                break;

            result = level;
        }

        return result;
    }

    private void OnMapInit(Entity<BreathalyzerComponent> ent, ref MapInitEvent args)
    {
        UpdateScreen(ent);
    }

    private void OnPowerCellChanged(Entity<BreathalyzerComponent> ent, ref PowerCellChangedEvent args)
    {
        UpdateScreen(ent);

        if (args.Ejected)
            _ui.CloseUi(ent.Owner, BreathalyzerUiKey.Key);
    }

    private void OnUiOpenAttempt(Entity<BreathalyzerComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || _powerCell.TryGetBatteryFromSlot(ent.Owner, out _))
            return;

        if (!args.Silent)
            _popup.PopupEntity(Loc.GetString("power-cell-no-battery"), ent, args.User);

        args.Cancel();
    }

    private void OnUseInHand(Entity<BreathalyzerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TrySelfTest(ent, args.User);
    }

    private void OnAfterInteract(Entity<BreathalyzerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (target == args.User)
        {
            args.Handled = true;
            TrySelfTest(ent, args.User);
            return;
        }

        if (!HasComp<BloodstreamComponent>(target) || !CanForceTest(target))
            return;

        args.Handled = true;
        TryStartTest(ent, args.User, target);
    }

    private void TrySelfTest(Entity<BreathalyzerComponent> ent, EntityUid user)
    {
        if (!HasComp<BloodstreamComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("breathalyzer-cannot-blow", ("device", ent.Owner)), ent, user);
            return;
        }

        TryStartTest(ent, user, user);
    }

    private bool CanForceTest(EntityUid target)
    {
        if (!TryComp<CuffableComponent>(target, out var cuffable) || _hands.GetHandCount(target) == 0)
            return true;

        return _cuffable.IsCuffed((target, cuffable));
    }

    private void TryStartTest(Entity<BreathalyzerComponent> ent, EntityUid user, EntityUid subject)
    {
        if (ent.Comp.State is BreathalyzerState.Blowing or BreathalyzerState.Analyzing)
        {
            _popup.PopupEntity(Loc.GetString("breathalyzer-busy", ("device", ent.Owner)), ent, user);
            return;
        }

        if (ent.Comp.MouthpieceDirty)
        {
            _popup.PopupEntity(Loc.GetString("breathalyzer-dirty"), ent, user);
            return;
        }

        if (!_powerCell.HasCharge(ent.Owner, ent.Comp.ChargePerTest, user, predicted: true))
            return;

        if (!_ingestion.HasMouthAvailable(user, subject))
            return;

        var forced = subject != user;
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.BlowDuration, new BreathalyzerDoAfterEvent(), ent, forced ? subject : null, ent)
        {
            BreakOnDamage = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            BreakOnMove = forced,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        ent.Comp.State = BreathalyzerState.Blowing;
        Dirty(ent);
        UpdateScreen(ent);

        if (forced)
        {
            _popup.PopupEntity(Loc.GetString("breathalyzer-force-user", ("target", subject), ("device", ent.Owner)),
                Loc.GetString("breathalyzer-force-others", ("user", user), ("target", subject), ("device", ent.Owner)),
                user,
                user);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("breathalyzer-blow-self", ("device", ent.Owner)),
                Loc.GetString("breathalyzer-blow-others", ("user", user), ("device", ent.Owner)),
                user,
                user);
        }

        _audio.PlayPredicted(ent.Comp.StartSound, ent, user);
        ent.Comp.BlowStream = _audio.PlayPredicted(ent.Comp.BlowSound, ent, user)?.Entity;
    }

    private void OnDoAfter(Entity<BreathalyzerComponent> ent, ref BreathalyzerDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.BlowStream = _audio.Stop(ent.Comp.BlowStream);

        if (args.Cancelled || !_powerCell.TryUseCharge(ent.Owner, ent.Comp.ChargePerTest, args.User, predicted: true))
        {
            ResetState(ent);
            return;
        }

        var subject = args.Target ?? args.User;
        var reading = Measure(ent.Comp, subject);

        if (reading >= ent.Comp.VomitThreshold
            && SharedRandomExtensions.PredictedProb(_timing, ent.Comp.VomitChance, GetNetEntity(ent), GetNetEntity(subject)))
        {
            VomitIntoMouthpiece(ent, args.User, subject);
            return;
        }

        ent.Comp.PendingResult = reading;
        ent.Comp.PendingSubjectName = Identity.Name(subject, EntityManager);
        ent.Comp.State = BreathalyzerState.Analyzing;
        ent.Comp.AnalysisEnd = _timing.CurTime + ent.Comp.AnalysisDuration;
        Dirty(ent);
        UpdateScreen(ent);

        _audio.PlayPredicted(ent.Comp.AnalyzeSound, ent, args.User);
        _ui.OpenUi(ent.Owner, BreathalyzerUiKey.Key, args.User, predicted: true);

        var shown = reading.ToString("0.00");
        _adminLog.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(args.User):user} tested {ToPrettyString(subject):subject} with {ToPrettyString(ent):device}, reading {shown:reading} per mille");
    }

    private void ResetState(Entity<BreathalyzerComponent> ent)
    {
        ent.Comp.State = ent.Comp.LastResult == null ? BreathalyzerState.Idle : BreathalyzerState.Result;
        Dirty(ent);
        UpdateScreen(ent);
    }

    private void VomitIntoMouthpiece(Entity<BreathalyzerComponent> ent, EntityUid user, EntityUid subject)
    {
        ent.Comp.MouthpieceDirty = true;
        ResetState(ent);
        _popup.PopupEntity(Loc.GetString("breathalyzer-vomit"), ent, user, PopupType.MediumCaution);

        _adminLog.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(subject):subject} vomited into {ToPrettyString(ent):device} during a test by {ToPrettyString(user):user}");

        if (_net.IsClient)
            return;

        _forensics.TransferDna(ent, subject);
        _vomit.Vomit(subject);
    }

    private void OnCleaned(Entity<CleansForensicsComponent> ent, ref CleanForensicsDoAfterEvent args)
    {
        if (args.Cancelled
            || args.Args.Target is not { } target
            || !TryComp<BreathalyzerComponent>(target, out var breathalyzer)
            || !breathalyzer.MouthpieceDirty)
            return;

        breathalyzer.MouthpieceDirty = false;
        Dirty(target, breathalyzer);
        UpdateScreen((target, breathalyzer));
        _popup.PopupEntity(Loc.GetString("breathalyzer-cleaned"), target, args.User);
    }

    private void OnPrint(Entity<BreathalyzerComponent> ent, ref BreathalyzerPrintMessage args)
    {
        if (_net.IsClient || ent.Comp.State != BreathalyzerState.Result || ent.Comp.LastResult is not { } result)
            return;

        if (_timing.CurTime < ent.Comp.PrintReadyAt)
        {
            _popup.PopupEntity(Loc.GetString("forensic-scanner-printer-not-ready"), ent, args.Actor);
            return;
        }

        if (!_powerCell.TryUseCharge(ent.Owner, ent.Comp.ChargePerPrint, args.Actor))
            return;

        var level = GetLevel(ent.Comp, result);
        var time = ent.Comp.LastMeasuredRoundTime;
        var printed = SpawnAtPosition(ent.Comp.ReportPaper, Transform(ent).Coordinates);
        _hands.PickupOrDrop(args.Actor, printed, checkActionBlocker: false);

        if (TryComp<PaperComponent>(printed, out var paper))
        {
            _paper.SetContent((printed, paper),
                Loc.GetString("breathalyzer-report",
                    ("subject", ent.Comp.LastSubjectName ?? Loc.GetString("breathalyzer-report-unknown")),
                    ("result", result.ToString("0.00")),
                    ("level", level == null ? string.Empty : Loc.GetString(level.Name)),
                    ("time", $"{(int) time.TotalHours:00}:{time.Minutes:00}")));
        }

        _audio.PlayPvs(ent.Comp.PrintSound, ent);
        ent.Comp.PrintReadyAt = _timing.CurTime + ent.Comp.PrintCooldown;
        Dirty(ent);
    }

    private void OnExamined(Entity<BreathalyzerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.MouthpieceDirty)
            args.PushMarkup(Loc.GetString("breathalyzer-examine-dirty"));

        switch (ent.Comp.State)
        {
            case BreathalyzerState.Blowing:
            case BreathalyzerState.Analyzing:
                args.PushMarkup(Loc.GetString("breathalyzer-examine-busy"));
                break;
            case BreathalyzerState.Result when ent.Comp.LastResult is { } result:
                var level = GetLevel(ent.Comp, result);
                args.PushMarkup(Loc.GetString("breathalyzer-examine-result",
                    ("result", result.ToString("0.00")),
                    ("level", level == null ? string.Empty : Loc.GetString(level.Name)),
                    ("color", level?.Color.ToHex() ?? Color.White.ToHex())));
                break;
            default:
                args.PushMarkup(Loc.GetString("breathalyzer-examine-idle"));
                break;
        }
    }

    private float Measure(BreathalyzerComponent comp, EntityUid user)
    {
        if (!TryComp<BloodstreamComponent>(user, out var bloodstream))
            return 0f;

        var units = GetQuantity(comp, user, bloodstream.BloodSolutionName)
                    + GetQuantity(comp, user, bloodstream.MetabolitesSolutionName);

        return units * comp.PerMillePerUnit + GetDrunkenness(comp, user);
    }

    private float GetDrunkenness(BreathalyzerComponent comp, EntityUid user)
    {
        if (!_status.TryGetMaxTime<DrunkStatusEffectComponent>(user, out var time))
            return 0f;

        if (time.EndEffectTime is not { } end)
            return comp.ScaleMax;

        var seconds = (float) (end - _timing.CurTime).TotalSeconds;
        return MathF.Max(0f, seconds) * comp.PerMillePerDrunkSecond;
    }

    private float GetQuantity(BreathalyzerComponent comp, EntityUid user, string solutionName)
    {
        if (!_solution.TryGetSolution(user, solutionName, out _, out var solution))
            return 0f;

        return solution.GetTotalPrototypeQuantity(comp.Reagents).Float();
    }

    private void UpdateScreen(Entity<BreathalyzerComponent> ent)
    {
        _appearance.SetData(ent, BreathalyzerVisuals.Dirty, ent.Comp.MouthpieceDirty);

        if (!_powerCell.TryGetBatteryFromSlot(ent.Owner, out _))
        {
            _appearance.SetData(ent, BreathalyzerVisuals.Screen, ent.Comp.OffScreen);
            return;
        }

        var screen = ent.Comp.State switch
        {
            BreathalyzerState.Blowing => ent.Comp.BlowScreen,
            BreathalyzerState.Analyzing => ent.Comp.AnalyzeScreen,
            BreathalyzerState.Result when ent.Comp.LastResult is { } result => GetLevel(ent.Comp, result)?.Screen ?? ent.Comp.IdleScreen,
            _ => ent.Comp.IdleScreen,
        };

        _appearance.SetData(ent, BreathalyzerVisuals.Screen, screen);
    }
}
