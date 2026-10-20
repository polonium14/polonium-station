using System.Linq;
using System.Text;
using Content.Shared._Polonium.Medical.PatientIntake;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.Cargo.Components;
using Content.Shared.Database;
using Content.Shared.Fax.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Paper;
using Robust.Shared.Utility;

namespace Content.Server._Polonium.Medical.PatientIntake;

public sealed partial class PatientIntakeSystem
{
    [Dependency] private MobStateSystem _mobState = default!;

    /// <summary>
    /// Everything that would fail this patient right now, including unfinished requests.
    /// </summary>
    public List<IntakeFailure> GetFailures(Entity<PatientIntakePatientComponent> patient)
    {
        var failures = _check.Check(patient, patient.Comp.ExpectedParts);
        if (TryComp<BodyComponent>(patient, out var body) && body.Organs is { } organs)
        {
            foreach (var (category, original) in patient.Comp.Replace)
            {
                if (organs.Contains(original))
                    failures.Add(new(IntakeFailureKind.RequestNotDone, category.Id));
            }
        }

        return failures;
    }

    private int Debit(PatientIntakeCase activeCase)
    {
        return (int) (activeCase.Payout / (float) activeCase.PatientsTotal * _proto.Index(activeCase.Proto).DebitFraction);
    }

    private void JudgePatient(Entity<PatientIntakeMachineComponent> ent, PatientIntakeCase activeCase, EntityUid patient)
    {
        var comp = Comp<PatientIntakePatientComponent>(patient);
        var caseProto = _proto.Index(activeCase.Proto);
        activeCase.Patients.Remove(patient);

        var elapsed = comp.ClockStart is { } start ? _timing.CurTime - start : TimeSpan.Zero;
        var late = elapsed > activeCase.Window;
        var failures = GetFailures((patient, comp));
        var share = activeCase.Payout / (float) activeCase.PatientsTotal;
        var name = Name(patient);

        var lines = new List<string>();
        int amount;

        if (_mobState.IsDead(patient))
        {
            amount = -Debit(activeCase);
            lines.Add(Loc.GetString("patient-intake-receipt-dead", ("name", name), ("amount", -amount)));
        }
        else if (late && caseProto.DebitOnTimeout)
        {
            amount = -Debit(activeCase);
            lines.Add(Loc.GetString("patient-intake-receipt-late", ("name", name), ("amount", -amount)));
        }
        else if (failures.Count > 0)
        {
            amount = 0;
            lines.Add(Loc.GetString("patient-intake-receipt-fail", ("name", name)));
        }
        else
        {
            // Each deduction is worked out as its own amount so the receipt can itemise it.
            var basePay = (int) share;
            var timeCut = caseProto.DebitOnTimeout
                ? 0
                : (int) (share * Math.Clamp((float) (elapsed / activeCase.Window), 0f, 1f));

            var gearCut = 0;
            var gearPercent = 0;
            var missingGear = comp.Gear.Count(g => !IsInside(g, ent));
            if (missingGear > 0)
            {
                var cut = _random.NextFloat(ent.Comp.GearCutMin, ent.Comp.GearCutMax);
                gearPercent = (int) MathF.Round(cut * 100);
                gearCut = (int) ((basePay - timeCut) * cut);
            }

            amount = Math.Max(0, basePay - timeCut - gearCut);
            lines.Add(Loc.GetString(late ? "patient-intake-receipt-pass-late" : "patient-intake-receipt-pass",
                ("name", name), ("amount", amount)));
            lines.Add(TimeLine(comp, activeCase));
            lines.Add(Loc.GetString("patient-intake-receipt-base", ("amount", basePay)));
            if (timeCut > 0)
                lines.Add(Loc.GetString("patient-intake-receipt-time-cut", ("amount", timeCut)));
            if (gearCut > 0)
            {
                lines.Add(Loc.GetString("patient-intake-receipt-gear",
                    ("count", missingGear), ("amount", gearCut), ("percent", gearPercent)));
            }
        }

        // Failed returns get the time too; a pass already has it with the breakdown.
        if (lines.Count == 1)
            lines.Add(TimeLine(comp, activeCase));

        foreach (var failure in failures)
        {
            lines.Add(Loc.GetString("patient-intake-examine-line", ("line", FailureText(failure))));
        }

        activeCase.Net += amount;
        activeCase.Receipt.AddRange(lines);
    }

    private string TimeLine(PatientIntakePatientComponent comp, PatientIntakeCase activeCase)
    {
        if (comp.ClockStart is not { } start)
            return Loc.GetString("patient-intake-receipt-time-unopened");

        var elapsed = _timing.CurTime - start;
        return Loc.GetString("patient-intake-receipt-time",
            ("time", $"{(int) elapsed.TotalMinutes}:{elapsed.Seconds:D2}"),
            ("limit", (int) activeCase.Window.TotalMinutes));
    }

    /// <summary>
    /// True when the entity sits somewhere inside the machine: worn by the patient or loose in the tray.
    /// </summary>
    private bool IsInside(EntityUid entity, EntityUid machine)
    {
        if (!Exists(entity))
            return false;

        var current = entity;
        while (_container.TryGetContainingContainer(current, out var container))
        {
            if (container.Owner == machine)
                return true;

            current = container.Owner;
        }

        return false;
    }

    private void TryFinishCase(Entity<PatientIntakeMachineComponent> ent)
    {
        if (ent.Comp.Case is not { } activeCase || activeCase.Patients.Count > 0 || activeCase.ToSpawn > 0)
            return;

        ent.Comp.Case = null;
        ent.Comp.LastNet = activeCase.Net;
        ent.Comp.CasesClosed++;
        ent.Comp.TotalNet += activeCase.Net;
        ent.Comp.UntilNextCase = TimeSpan.FromMinutes(_random.NextFloat(
            _cfg.GetCVar(CCVars.PatientIntakeGapMin), _cfg.GetCVar(CCVars.PatientIntakeGapMax)));

        ChargeAccount(ent, activeCase.Net);
        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(ent):machine} closed patient intake case #{ent.Comp.CaseCount}, net {activeCase.Net} to {ent.Comp.Account}");

        var caseProto = _proto.Index(activeCase.Proto);
        var content = new StringBuilder();
        content.AppendLine(Loc.GetString("patient-intake-receipt-header", ("number", ent.Comp.CaseCount)));
        content.AppendLine(Loc.GetString("patient-intake-kind", ("case", Loc.GetString(caseProto.Name))));
        content.AppendLine();
        foreach (var line in activeCase.Receipt)
        {
            content.AppendLine(line);
        }

        content.AppendLine();
        content.AppendLine(Loc.GetString("patient-intake-receipt-total", ("amount", activeCase.Net)));

        var title = Loc.GetString("patient-intake-receipt-name", ("number", ent.Comp.CaseCount));
        if (!TrySendToFax(ent, title, content.ToString()))
            SpawnPaper(ent, title, content.ToString());

        _radio.SendRadioMessage(ent, Loc.GetString(activeCase.Net < 0 ? "patient-intake-radio-finished-debit" : "patient-intake-radio-finished",
            ("number", ent.Comp.CaseCount), ("amount", Math.Abs(activeCase.Net))), ent.Comp.Channel, ent);
    }

    private void ChargeAccount(EntityUid machine, int amount)
    {
        if (amount == 0
            || !TryComp<PatientIntakeMachineComponent>(machine, out var comp)
            || _station.GetOwningStation(machine) is not { } station
            || !TryComp<StationBankAccountComponent>(station, out var bank))
            return;

        _cargo.UpdateBankAccount((station, bank), amount, comp.Account);
    }

    public string FailureText(IntakeFailure failure)
    {
        var subject = failure.Subject is { } s ? PartName(s) : string.Empty;
        return Loc.GetString($"patient-intake-failure-{failure.Kind.ToString().ToLowerInvariant()}", ("part", subject));
    }

    /// <summary>
    /// Organ categories are prototype ids; show their localized name when there is one.
    /// </summary>
    private string PartName(string subject)
    {
        return Loc.TryGetString($"patient-intake-part-{subject.ToLowerInvariant()}", out var name) ? name : subject;
    }

    private (string Name, string Content) BuildCaseFile(
        Entity<PatientIntakeMachineComponent> ent,
        PatientIntakeCase activeCase,
        PatientIntakeCasePrototype caseProto,
        EntityUid patient)
    {
        var comp = Comp<PatientIntakePatientComponent>(patient);
        var species = TryComp<HumanoidProfileComponent>(patient, out var profile)
            ? Loc.GetString(_proto.Index(profile.Species).Name)
            : string.Empty;

        var content = new StringBuilder();
        content.AppendLine(Loc.GetString("patient-intake-file-header", ("number", ent.Comp.CaseCount)));
        content.AppendLine(Loc.GetString("patient-intake-kind", ("case", Loc.GetString(caseProto.Name))));
        content.AppendLine();
        content.AppendLine(Loc.GetString("patient-intake-file-patient",
            ("name", FormattedMessage.EscapeText(Name(patient))), ("species", species)));
        content.AppendLine(Loc.GetString(caseProto.DebitOnTimeout ? "patient-intake-file-deadline-hard" : "patient-intake-file-deadline",
            ("minutes", (int) activeCase.Window.TotalMinutes)));
        content.AppendLine(Loc.GetString("patient-intake-file-payout",
            ("amount", (int) (activeCase.Payout / (float) activeCase.PatientsTotal))));
        if (activeCase.PatientsTotal > 1)
            content.AppendLine(Loc.GetString("patient-intake-file-mass", ("count", activeCase.PatientsTotal)));

        content.AppendLine();
        content.AppendLine(Loc.GetString(caseProto.FullIssueList ? "patient-intake-file-issues" : "patient-intake-file-symptoms"));
        foreach (var id in comp.Issues.Distinct())
        {
            var issue = _proto.Index(id);
            var line = caseProto.FullIssueList ? issue.Description : issue.Symptom;
            content.AppendLine(Loc.GetString("patient-intake-examine-line", ("line", Loc.GetString(line))));
        }

        content.AppendLine();
        content.AppendLine(Loc.GetString("patient-intake-file-gear"));
        foreach (var item in comp.Gear)
        {
            content.AppendLine(Loc.GetString("patient-intake-examine-line", ("line", FormattedMessage.EscapeText(Name(item)))));
        }

        content.AppendLine();
        content.AppendLine(Loc.GetString("patient-intake-file-rules"));

        return (Loc.GetString("patient-intake-file-name", ("number", ent.Comp.CaseCount)), content.ToString());
    }

    private bool TrySendToFax(Entity<PatientIntakeMachineComponent> ent, string name, string content)
    {
        foreach (var sink in _deviceLink.GetLinkedSinks(ent.Owner, ent.Comp.FaxPort))
        {
            if (!TryComp<FaxMachineComponent>(sink, out var fax))
                continue;

            _fax.Receive(sink, new FaxPrintout(content, name), null, fax);
            return true;
        }

        return false;
    }

    private EntityUid SpawnPaper(EntityUid machine, string name, string content)
    {
        var paper = Spawn("Paper", Transform(machine).Coordinates);
        _paper.SetContent(paper, content);
        _metaData.SetEntityName(paper, name);
        return paper;
    }
}
