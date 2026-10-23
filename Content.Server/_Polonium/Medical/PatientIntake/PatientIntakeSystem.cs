// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Body.Systems;
using Content.Server.Cargo.Systems;
using Content.Server.Fax;
using Content.Server.GameTicking;
using Content.Server.Medical.Components;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared._Polonium.Medical.PatientIntake;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.DeviceLinking;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Implants;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Random.Helpers;
using Content.Shared.Roles.Jobs;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Medical.PatientIntake;

public sealed partial class PatientIntakeSystem : SharedPatientIntakeSystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private CargoSystem _cargo = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityStorageSystem _storage = default!;
    [Dependency] private FaxSystem _fax = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private InternalsSystem _internals = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private PatientIntakeCheckSystem _check = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedSubdermalImplantSystem _implants = default!;
    [Dependency] private StationSpawningSystem _spawning = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private TraumaSystem _trauma = default!;
    [Dependency] private WoundSystem _wound = default!;

    /// <summary>
    /// How often a machine that is due for a case rechecks the spawn conditions.
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PatientIntakeMachineComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PatientIntakeMachineComponent, EntityTerminatingEvent>(OnMachineTerminating);
        SubscribeLocalEvent<PatientIntakeMachineComponent, StorageAfterCloseEvent>(OnAfterClose);
        SubscribeLocalEvent<PatientIntakeMachineComponent, GetVerbsEvent<AlternativeVerb>>(OnGetToggleVerb);
        SubscribeLocalEvent<PatientIntakeMachineComponent, GetVerbsEvent<Verb>>(OnGetMarkMissingVerb);
        SubscribeLocalEvent<PatientIntakeMachineComponent, ExaminedEvent>(OnMachineExamined);

        SubscribeLocalEvent<PatientIntakePatientComponent, MindAddedMessage>(OnPatientMindAdded);
        SubscribeLocalEvent<PatientIntakePatientComponent, BeingGibbedEvent>(OnPatientGibbed);
        SubscribeLocalEvent<PatientIntakePatientComponent, EntityTerminatingEvent>(OnPatientTerminating);
        SubscribeLocalEvent<PatientIntakePatientComponent, ExaminedEvent>(OnPatientExamined);

        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
    }

    private void OnRoundEndText(RoundEndTextAppendEvent args)
    {
        var query = EntityQueryEnumerator<PatientIntakeMachineComponent>();
        var header = false;
        while (query.MoveNext(out var uid, out var machine))
        {
            if (machine.CasesClosed == 0)
                continue;

            if (!header)
            {
                args.AddLine(Loc.GetString("patient-intake-round-end-header"));
                header = true;
            }

            args.AddLine(Loc.GetString(machine.TotalNet < 0 ? "patient-intake-round-end-line-loss" : "patient-intake-round-end-line",
                ("cases", machine.CasesClosed), ("amount", Math.Abs(machine.TotalNet))));
        }
    }

    private void OnMapInit(Entity<PatientIntakeMachineComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.UntilNextCase = TimeSpan.FromMinutes(_cfg.GetCVar(CCVars.PatientIntakeFirstDelay));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enabled = _cfg.GetCVar(CCVars.PatientIntakeEnabled);
        var query = EntityQueryEnumerator<PatientIntakeMachineComponent, EntityStorageComponent>();
        while (query.MoveNext(out var uid, out var machine, out var storage))
        {
            var ent = (uid, machine);
            _appearance.SetData(uid, PatientIntakeVisuals.Status, GetStatus(ent, storage, enabled));
            if (!enabled)
                continue;

            if (machine.Case is { } activeCase)
            {
                if (activeCase.ToSpawn > 0 && _timing.CurTime >= activeCase.NextPop && IsReadyForPatient(storage))
                    PopPatient(ent, activeCase, storage);

                BeepIfWaiting((uid, machine), storage, frameTime);
                continue;
            }

            if (!IsRunning(ent))
                continue;

            machine.UntilNextCase -= TimeSpan.FromSeconds(frameTime);
            if (machine.UntilNextCase > TimeSpan.Zero)
                continue;

            var medics = IsReadyForPatient(storage) ? CountMedics(ent) : 0;
            if (medics > 0)
                StartCase(ent, storage, medics);
            else
                machine.UntilNextCase = RetryDelay;
        }
    }

    private PatientIntakeStatus GetStatus(Entity<PatientIntakeMachineComponent> ent, EntityStorageComponent storage, bool enabled)
    {
        if (!_power.IsPowered(ent.Owner))
            return PatientIntakeStatus.Off;

        if (ent.Comp.Case != null)
            return IsPatientWaiting(storage) ? PatientIntakeStatus.PatientWaiting : PatientIntakeStatus.Busy;

        return enabled && ent.Comp.Enabled ? PatientIntakeStatus.Ready : PatientIntakeStatus.Off;
    }

    private bool IsPatientWaiting(EntityStorageComponent storage)
    {
        return !storage.Open && storage.Contents.ContainedEntities.Any(HasComp<PatientIntakePatientComponent>);
    }

    private void BeepIfWaiting(Entity<PatientIntakeMachineComponent> ent, EntityStorageComponent storage, float frameTime)
    {
        ent.Comp.UntilWaitingSound -= TimeSpan.FromSeconds(frameTime);
        if (ent.Comp.UntilWaitingSound > TimeSpan.Zero)
            return;

        ent.Comp.UntilWaitingSound = ent.Comp.WaitingSoundDelay;
        if (IsPatientWaiting(storage))
            _audio.PlayPvs(ent.Comp.WaitingSound, ent);
    }

    public bool IsRunning(Entity<PatientIntakeMachineComponent> ent)
    {
        return ent.Comp.Enabled && _power.IsPowered(ent.Owner);
    }

    private bool IsReadyForPatient(EntityStorageComponent storage)
    {
        return !storage.Open && !storage.Contents.ContainedEntities.Any(HasComp<BodyComponent>);
    }

    /// <summary>
    /// Living, connected members of the machine's department on the same station.
    /// </summary>
    private int CountMedics(Entity<PatientIntakeMachineComponent> ent)
    {
        var station = _station.GetOwningStation(ent);
        var count = 0;
        foreach (var mind in _mind.GetAliveHumans())
        {
            if (mind.Comp.UserId is not { } user
                || !_player.TryGetSessionById(user, out _)
                || !_jobs.MindIsInDepartment(mind, ent.Comp.Department)
                || mind.Comp.OwnedEntity is not { } body
                || _station.GetOwningStation(body) != station)
                continue;

            count++;
        }

        return count;
    }

    /// <summary>
    /// Rolls a case kind the current staff can handle.
    /// </summary>
    public void StartCase(Entity<PatientIntakeMachineComponent> ent, EntityStorageComponent storage, int medics)
    {
        var weights = _proto.EnumeratePrototypes<PatientIntakeCasePrototype>()
            .Where(c => c.Weight > 0 && c.MinMedics <= medics)
            .ToDictionary(c => c, c => c.Weight);

        if (weights.Count == 0)
        {
            ent.Comp.UntilNextCase = RetryDelay;
            return;
        }

        StartCase(ent, storage, _random.Pick(weights));
    }

    public void StartCase(Entity<PatientIntakeMachineComponent> ent, EntityStorageComponent storage, PatientIntakeCasePrototype caseProto)
    {
        var patients = _random.Next(caseProto.PatientsMin, caseProto.PatientsMax + 1);
        ent.Comp.CaseCount++;
        ent.Comp.Case = new PatientIntakeCase
        {
            Proto = caseProto.ID,
            Payout = _random.Next(caseProto.PayMin, caseProto.PayMax + 1),
            Window = caseProto.WindowMin + (caseProto.WindowMax - caseProto.WindowMin) * _random.NextDouble(),
            PatientsTotal = patients,
            ToSpawn = patients,
        };

        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(ent):machine} started patient intake case #{ent.Comp.CaseCount} ({caseProto.ID}, {patients} patients, payout {ent.Comp.Case.Payout})");

        PopPatient(ent, ent.Comp.Case, storage);
    }

    private void PopPatient(Entity<PatientIntakeMachineComponent> ent, PatientIntakeCase activeCase, EntityStorageComponent storage)
    {
        var caseProto = _proto.Index(activeCase.Proto);
        var patient = SpawnPatient(ent, caseProto, Transform(ent).Coordinates);
        _container.Insert(patient, storage.Contents, force: true);
        SetFrozen(patient, true);

        activeCase.Patients.Add(patient);
        activeCase.ToSpawn--;
        activeCase.NextPop = _timing.CurTime + ent.Comp.PopDelay;

        var caseFile = BuildCaseFile(ent, activeCase, caseProto, patient);
        if (!TrySendToFax(ent, caseFile.Name, caseFile.Content))
        {
            // No fax: the file rides in the tray with the patient.
            var paper = SpawnPaper(ent, caseFile.Name, caseFile.Content);
            _container.Insert(paper, storage.Contents, force: true);
        }

        _radio.SendRadioMessage(ent, Loc.GetString("patient-intake-radio-arrived",
            ("case", Loc.GetString(caseProto.Name)),
            ("name", Name(patient)),
            ("patient", activeCase.PatientsTotal - activeCase.ToSpawn),
            ("total", activeCase.PatientsTotal)), ent.Comp.Channel, ent);
    }

    #region Freezing

    /// <summary>
    /// Pauses the patient and every organ in it, so nothing deteriorates before the tray is opened.
    /// </summary>
    private void SetFrozen(EntityUid patient, bool frozen)
    {
        if (TryComp<PatientIntakePatientComponent>(patient, out var comp))
            comp.Frozen = frozen;

        SetPausedRecursive(patient, frozen);
    }

    private void SetPausedRecursive(EntityUid uid, bool paused)
    {
        _metaData.SetEntityPaused(uid, paused);
        var children = Transform(uid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            SetPausedRecursive(child, paused);
        }
    }

    #endregion

    #region Storage

    protected override void OnOpened(Entity<PatientIntakeMachineComponent> ent)
    {
        if (ent.Comp.Case is not { } activeCase)
            return;

        // The tray contents are already ejected onto the tray here.
        foreach (var patient in activeCase.Patients)
        {
            if (!TryComp<PatientIntakePatientComponent>(patient, out var comp) || !comp.Frozen)
                continue;

            SetFrozen(patient, false);
            comp.ClockStart = _timing.CurTime;
            activeCase.ClockStart ??= _timing.CurTime;
        }
    }

    private void OnAfterClose(Entity<PatientIntakeMachineComponent> ent, ref StorageAfterCloseEvent args)
    {
        if (ent.Comp.Case is not { } activeCase || !TryComp<EntityStorageComponent>(ent, out var storage))
            return;

        var contents = storage.Contents.ContainedEntities.ToList();
        var returned = contents.Where(e => IsCasePatient(ent, e)).ToList();
        if (returned.Count == 0)
            return;

        foreach (var patient in returned)
        {
            JudgePatient(ent, activeCase, patient);
        }

        _adminLogger.Add(LogType.EntityDelete, LogImpact.Medium,
            $"{ToPrettyString(ent):machine} took back case #{ent.Comp.CaseCount} and deleted {string.Join(", ", contents.Select(e => ToPrettyString(e)))}");

        foreach (var entity in contents)
        {
            RemComp<PatientIntakePatientComponent>(entity);
            QueueDel(entity);
        }

        TryFinishCase(ent);
    }

    #endregion

    #region Patient events

    private void OnPatientMindAdded(Entity<PatientIntakePatientComponent> ent, ref MindAddedMessage args)
    {
        FailPatient(ent, "patient-intake-receipt-mind");
    }

    private void OnPatientGibbed(Entity<PatientIntakePatientComponent> ent, ref BeingGibbedEvent args)
    {
        FailPatient(ent, "patient-intake-receipt-gibbed");
    }

    private void OnPatientTerminating(Entity<PatientIntakePatientComponent> ent, ref EntityTerminatingEvent args)
    {
        FailPatient(ent, "patient-intake-receipt-destroyed");
    }

    /// <summary>
    /// Ends a patient's part of the case with a debit and turns them into an ordinary body.
    /// </summary>
    private void FailPatient(Entity<PatientIntakePatientComponent> ent, LocId reason)
    {
        var machine = ent.Comp.Machine;
        RemCompDeferred<PatientIntakePatientComponent>(ent);
        if (ent.Comp.Frozen)
            SetFrozen(ent, false);

        // Map teardown at round end deletes everything: the case is void, not failed.
        if (TerminatingOrDeleted(machine) || TerminatingOrDeleted(Transform(machine).MapUid))
            return;

        if (!TryComp<PatientIntakeMachineComponent>(machine, out var machineComp)
            || machineComp.Case is not { } activeCase
            || !activeCase.Patients.Remove(ent))
            return;

        var debit = Debit(activeCase);
        activeCase.Net -= debit;
        activeCase.Receipt.Add(Loc.GetString(reason, ("name", Name(ent)), ("amount", debit)));
        TryFinishCase((machine, machineComp));
    }

    private void OnPatientExamined(Entity<PatientIntakePatientComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange
            || _hands.GetActiveItem(args.Examiner) is not { } held
            || !HasComp<HealthAnalyzerComponent>(held))
            return;

        var failures = GetFailures(ent);
        using (args.PushGroup(nameof(PatientIntakeSystem)))
        {
            if (failures.Count == 0)
            {
                args.PushMarkup(Loc.GetString("patient-intake-examine-fit"));
                return;
            }

            args.PushMarkup(Loc.GetString("patient-intake-examine-unfit"));
            foreach (var failure in failures)
            {
                args.PushMarkup(Loc.GetString("patient-intake-examine-line", ("line", FailureText(failure))));
            }
        }
    }

    #endregion

    #region Machine interaction

    private void OnGetToggleVerb(Entity<PatientIntakeMachineComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var enabling = !ent.Comp.Enabled;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(enabling ? "patient-intake-verb-on" : "patient-intake-verb-off"),
            Act = () => TryToggle(ent, user, enabling),
        });
    }

    public void TryToggle(Entity<PatientIntakeMachineComponent> ent, EntityUid user, bool enabling)
    {
        if (enabling && !_access.FindAccessTags(user).Contains(ent.Comp.EnableAccess))
        {
            _popup.PopupEntity(Loc.GetString("patient-intake-popup-access"), ent, user);
            return;
        }

        ent.Comp.Enabled = enabling;
        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):user} switched {ToPrettyString(ent):machine} {(enabling ? "on" : "off")}");
        _popup.PopupEntity(Loc.GetString(enabling ? "patient-intake-popup-on" : "patient-intake-popup-off"), ent, user);
    }

    private void OnGetMarkMissingVerb(Entity<PatientIntakeMachineComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Case is null)
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("patient-intake-verb-missing"),
            Act = () => TryMarkMissing(ent, user),
        });
    }

    public void TryMarkMissing(Entity<PatientIntakeMachineComponent> ent, EntityUid user)
    {
        if (ent.Comp.Case is not { } activeCase)
            return;

        var tags = _access.FindAccessTags(user);
        if (!ent.Comp.MarkMissingAccess.Any(tags.Contains))
        {
            _popup.PopupEntity(Loc.GetString("patient-intake-popup-missing-access"), ent, user);
            return;
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):user} marked patient intake case #{ent.Comp.CaseCount} on {ToPrettyString(ent):machine} as missing");
        MarkMissing(ent, activeCase);
    }

    /// <summary>
    /// Debits the case, leaves the patients as ordinary bodies and frees the machine.
    /// </summary>
    public void MarkMissing(Entity<PatientIntakeMachineComponent> ent, PatientIntakeCase activeCase)
    {
        foreach (var patient in activeCase.Patients)
        {
            ReleasePatient(patient);
        }

        activeCase.Patients.Clear();
        activeCase.ToSpawn = 0;

        var debit = (int) (activeCase.Payout * _proto.Index(activeCase.Proto).DebitFraction);
        activeCase.Net -= debit;
        activeCase.Receipt.Add(Loc.GetString("patient-intake-receipt-missing", ("amount", debit)));
        TryFinishCase(ent);
    }

    private void ReleasePatient(EntityUid patient)
    {
        if (TryComp<PatientIntakePatientComponent>(patient, out var comp) && comp.Frozen)
            SetFrozen(patient, false);

        RemComp<PatientIntakePatientComponent>(patient);
    }

    private void OnMachineTerminating(Entity<PatientIntakeMachineComponent> ent, ref EntityTerminatingEvent args)
    {
        // Case voided: no pay, no penalty, the bodies stay - except a hard order already past
        // its deadline, which would otherwise be dodged by smashing the machine.
        if (ent.Comp.Case is not { } activeCase)
            return;

        var debit = 0;
        if (_proto.Index(activeCase.Proto).DebitOnTimeout && !TerminatingOrDeleted(Transform(ent).MapUid))
        {
            foreach (var patient in activeCase.Patients)
            {
                if (TryComp<PatientIntakePatientComponent>(patient, out var comp)
                    && comp.ClockStart is { } start
                    && _timing.CurTime - start > activeCase.Window)
                    debit += Debit(activeCase);
            }
        }

        foreach (var patient in activeCase.Patients)
        {
            ReleasePatient(patient);
        }

        ent.Comp.Case = null;
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(ent):machine} destroyed during patient intake case #{ent.Comp.CaseCount}, debit {debit}");

        if (debit > 0)
            ChargeAccount(ent, -debit);
        ent.Comp.TotalNet -= debit;
    }

    private void OnMachineExamined(Entity<PatientIntakeMachineComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(PatientIntakeSystem)))
        {
            args.PushMarkup(Loc.GetString(IsRunning(ent) ? "patient-intake-examine-running" : "patient-intake-examine-stopped"));

            if (ent.Comp.Case is not { } activeCase)
                return;

            args.PushMarkup(Loc.GetString("patient-intake-examine-case",
                ("case", Loc.GetString(_proto.Index(activeCase.Proto).Name))));

            if (activeCase.ClockStart is not { } start)
            {
                args.PushMarkup(Loc.GetString("patient-intake-examine-clock-waiting"));
                return;
            }

            var left = start + activeCase.Window - _timing.CurTime;
            args.PushMarkup(left > TimeSpan.Zero
                ? Loc.GetString("patient-intake-examine-time", ("minutes", (int) Math.Ceiling(left.TotalMinutes)))
                : Loc.GetString("patient-intake-examine-overdue"));
        }
    }

    #endregion
}
