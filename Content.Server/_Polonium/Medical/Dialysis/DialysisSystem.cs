using Content.Server._Polonium.Body;
using Content.Server.Administration.Logs;
using Content.Shared._Polonium.Medical.Dialysis;
using Content.Shared._Polonium.Medical.IV;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Medical.Dialysis;

/// <inheritdoc cref="DialysisComponent"/>
public sealed partial class DialysisSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private CirculationSystem _circulation = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DialysisComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<DialysisComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DialysisComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnEmagged(Entity<DialysisComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || _emag.CheckFlag(ent, EmagType.Interaction))
            return;

        args.Handled = true;
        _adminLog.Add(LogType.Emag, LogImpact.High,
            $"{ToPrettyString(args.UserUid):user} emagged {ToPrettyString(ent):machine}, which now strips medicine from the blood");
    }

    private void OnExamined(Entity<DialysisComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange && _emag.CheckFlag(ent, EmagType.Interaction))
            args.PushMarkup(Loc.GetString("dialysis-examine-emagged"));
    }

    private void OnShutdown(Entity<DialysisComponent> ent, ref ComponentShutdown args)
    {
        SetPatient(ent, null);
    }

    /// <summary>
    /// The dialysis machine working on a patient, if any.
    /// </summary>
    public Entity<DialysisComponent>? GetMachine(EntityUid patient)
    {
        return CompOrNull<OnDialysisComponent>(patient)?.Machine is { } machine && TryComp<DialysisComponent>(machine, out var dialysis)
            ? (machine, dialysis)
            : null;
    }

    private void SetPatient(Entity<DialysisComponent> ent, EntityUid? patient)
    {
        if (ent.Comp.Patient == patient)
            return;

        if (ent.Comp.Patient is { } old && CompOrNull<OnDialysisComponent>(old)?.Machine == ent.Owner)
        {
            RemComp<OnDialysisComponent>(old);
            _circulation.Refresh(old);
        }

        ent.Comp.Patient = patient;
        if (patient is not { } next || TerminatingOrDeleted(next))
            return;

        EnsureComp<OnDialysisComponent>(next).Machine = ent;
        _circulation.Refresh(next);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DialysisComponent, IVDripComponent>();
        while (query.MoveNext(out var uid, out var dialysis, out var iv))
        {
            // Attaching, detaching and ripping are the IV drip's business; this only follows it.
            SetPatient((uid, dialysis), iv.AttachedTo);

            if (dialysis.Patient is not { } patient || _timing.CurTime < dialysis.NextUpdate)
                continue;

            dialysis.NextUpdate = _timing.CurTime + UpdateInterval;
            Filter((uid, dialysis), patient);
        }
    }

    private void Filter(Entity<DialysisComponent> ent, EntityUid patient)
    {
        if (!TryComp<BloodstreamComponent>(patient, out var bloodstream))
            return;

        Entity<SolutionComponent>? blood = null;
        if (!_solutions.ResolveSolution(patient, bloodstream.BloodSolutionName, ref blood))
            return;

        // The patient's own blood always goes back in. Read field by field, BloodstreamComponent only grants read access.
        var ownBlood = new HashSet<string>();
        foreach (var reagent in bloodstream.BloodReferenceSolution.Contents)
        {
            ownBlood.Add(reagent.Reagent.Prototype);
        }

        var keepMedicine = !_emag.CheckFlag(ent, EmagType.Interaction);
        var budget = ent.Comp.RemovePerSecond * UpdateInterval.TotalSeconds;
        var removals = new List<(ReagentId Reagent, FixedPoint2 Amount)>();

        foreach (var (reagent, quantity) in blood.Value.Comp.Solution.Contents)
        {
            if (budget <= FixedPoint2.Zero)
                break;

            if (ownBlood.Contains(reagent.Prototype)
                || !_proto.TryIndex<ReagentPrototype>(reagent.Prototype, out var proto))
                continue;

            var medicine = proto.Group == ent.Comp.KeptGroup;
            if (medicine == keepMedicine)
                continue;

            var amount = FixedPoint2.Min(quantity, budget);
            budget -= amount;
            removals.Add((reagent, amount));
        }

        foreach (var (reagent, amount) in removals)
        {
            _solutions.RemoveReagent(blood.Value, reagent, amount);
        }
    }
}

/// <summary>
/// A patient a dialysis machine is working on.
/// </summary>
[RegisterComponent]
[Access(typeof(DialysisSystem))]
public sealed partial class OnDialysisComponent : Component
{
    [ViewVariables]
    public EntityUid Machine;
}
