using System.Linq;
using System.Text;
using Content.Shared._Polonium.Body;
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
                // A prosthesis request is only met by a prosthesis; a missing part is reported on its own.
                var notDone = organs.Contains(original)
                    || (patient.Comp.Prosthetic.Contains(category)
                        && organs.ContainedEntities.Any(o => TryComp<OrganComponent>(o, out var organ) && organ.Category == category && !HasComp<ProstheticComponent>(o)));
                if (notDone)
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
            // Full pay inside the window; past it, pay runs out over one more window.
            var timeCut = caseProto.DebitOnTimeout
                ? 0
                : (int) (share * Math.Clamp((float) ((elapsed - activeCase.Window) / activeCase.Window), 0f, 1f));

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
        content.AppendLine(DocHeader(ent, "patient-intake-receipt-title", caseProto));
        foreach (var line in activeCase.Receipt)
        {
            content.AppendLine(line);
        }

        content.AppendLine();
        content.AppendLine(Loc.GetString("patient-intake-receipt-total", ("amount", activeCase.Net)));
        content.AppendLine();
        content.Append(Loc.GetString("patient-intake-doc-footer"));

        var title = Loc.GetString("patient-intake-receipt-name");
        if (!TrySendToFax(ent, title, content.ToString()))
            SpawnPaper(ent, title, content.ToString());

        _radio.SendRadioMessage(ent, Loc.GetString(activeCase.Net < 0 ? "patient-intake-radio-finished-debit" : "patient-intake-radio-finished",
            ("case", Loc.GetString(caseProto.Name)), ("amount", Math.Abs(activeCase.Net))), ent.Comp.Channel, ent);
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

    private const string StampState = "paper_stamp-centcom";

    private StampDisplayInfo Stamp => new() { StampedName = Loc.GetString("patient-intake-fax-sender"), StampedColor = Color.FromHex("#50afbf") };

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

        var name = FormattedMessage.EscapeText(Name(patient));
        var content = new StringBuilder();
        content.AppendLine(DocHeader(ent, "patient-intake-file-title", caseProto));
        content.AppendLine(Loc.GetString("patient-intake-file-identity", ("name", name)));
        content.AppendLine(Loc.GetString("patient-intake-file-patient", ("species", species)));
        content.AppendLine(Loc.GetString(caseProto.DebitOnTimeout ? "patient-intake-file-deadline-hard" : "patient-intake-file-deadline",
            ("minutes", (int) activeCase.Window.TotalMinutes)));
        content.AppendLine(Loc.GetString("patient-intake-file-payout",
            ("amount", (int) (activeCase.Payout / (float) activeCase.PatientsTotal))));
        if (activeCase.PatientsTotal > 1)
            content.AppendLine(Loc.GetString("patient-intake-file-mass", ("count", activeCase.PatientsTotal)));

        content.AppendLine(Loc.GetString(caseProto.FullIssueList ? "patient-intake-file-issues" : "patient-intake-file-symptoms"));
        var index = 0;
        foreach (var id in comp.Issues.Distinct())
        {
            var issue = _proto.Index(id);
            var line = caseProto.FullIssueList ? issue.Description : issue.Symptom;
            var part = comp.IssueParts.TryGetValue(id, out var category) ? PartName(category) : string.Empty;
            content.AppendLine(Loc.GetString("patient-intake-doc-item", ("index", ++index), ("line", Loc.GetString(line, ("part", part)))));
        }

        content.AppendLine(Loc.GetString("patient-intake-file-gear"));
        index = 0;
        foreach (var item in comp.Gear)
        {
            content.AppendLine(Loc.GetString("patient-intake-doc-item", ("index", ++index), ("line", FormattedMessage.EscapeText(Name(item)))));
        }

        content.AppendLine();
        content.Append(Loc.GetString("patient-intake-doc-footer"));
        return (Loc.GetString("patient-intake-file-name", ("name", Name(patient))), content.ToString());
    }

    /// <summary>
    /// Letterhead in the style of the station's NanoTrasen medical forms (see the document printer).
    /// </summary>
    private string DocHeader(EntityUid machine, LocId title, PatientIntakeCasePrototype caseProto)
    {
        var station = _station.GetOwningStation(machine) is { } s ? Name(s) : Loc.GetString("doc-text-printer-default-station");
        return Loc.GetString("patient-intake-doc-header",
            ("title", Loc.GetString(title)),
            ("station", FormattedMessage.EscapeText(station)),
            ("date", DateTime.Now.AddYears(1000).ToString("dd.MM.yyyy")),
            ("case", Loc.GetString(caseProto.Name)));
    }

    private bool TrySendToFax(Entity<PatientIntakeMachineComponent> ent, string name, string content)
    {
        foreach (var sink in _deviceLink.GetLinkedSinks(ent.Owner, ent.Comp.FaxPort))
        {
            if (!TryComp<FaxMachineComponent>(sink, out var fax))
                continue;

            _fax.Receive(sink, new FaxPrintout(content, name,
                stampState: StampState, stampedBy: [Stamp], senderFaxName: Loc.GetString("patient-intake-fax-sender")), null, fax);
            return true;
        }

        return false;
    }

    private EntityUid SpawnPaper(EntityUid machine, string name, string content)
    {
        var paper = Spawn("Paper", Transform(machine).Coordinates);
        _paper.SetContent(paper, content);
        _paper.TryStamp((paper, Comp<PaperComponent>(paper)), Stamp, StampState);
        _metaData.SetEntityName(paper, name);
        return paper;
    }
}
