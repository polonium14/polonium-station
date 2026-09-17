using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Events;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction;
using Content.Shared.Medical;
using Content.Shared.Medical.Healing;
using Content.Shared.Mobs.Systems;

namespace Content.Shared._Polonium.Tutorial;

/// <summary>
/// The medical room hands out just enough medicine for one patient. None of it may end up on the
/// floor, in the trainee or in a body that is past saving.
/// </summary>
public abstract partial class SharedTutorialSystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    private void InitializeMedicine()
    {
        SubscribeLocalEvent<TutorialMedicineComponent, ComponentInit>(OnMedicineInit);
        // the injector and healing systems own the item-side events, so these listen on the trainee
        SubscribeLocalEvent<TutorialSessionComponent, UserInteractUsingEvent>(OnTraineeUseOn);
        SubscribeLocalEvent<TutorialSessionComponent, SelfBeforeInjectEvent>(OnTraineeInject);
        SubscribeLocalEvent<TutorialSessionComponent, HealingDoAfterEvent>(OnTraineeHealed, before: [typeof(HealingSystem)]);
    }

    private void OnMedicineInit(Entity<TutorialMedicineComponent> ent, ref ComponentInit args)
    {
        // a syringe draws through drawable, pouring and splashing go through these two
        RemComp<DrainableSolutionComponent>(ent.Owner);
        RemComp<SpillableComponent>(ent.Owner);
    }

    // runs before the item gets its turn, so a refusal here stops the do-after from ever starting
    private void OnTraineeUseOn(Entity<TutorialSessionComponent> ent, ref UserInteractUsingEvent args)
    {
        if (args.Handled)
            return;

        LocId? refusal = null;
        if (HasComp<InjectorComponent>(args.Used))
            refusal = InjectorRefusal(args.User, args.Used, args.Target);
        else if (HasComp<HealingComponent>(args.Used))
            refusal = HealingRefusal(args.User, args.Target);

        if (refusal is not { } message)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString(message), args.Target, args.User);
    }

    // the do-after has run by now. this catches the injector used in hand and stabbed in combat
    private void OnTraineeInject(Entity<TutorialSessionComponent> ent, ref SelfBeforeInjectEvent args)
    {
        if (args.Cancelled)
            return;

        var target = args.TargetGettingInjected;
        var refusal = InjectRefusal(ent.Owner, target);
        if (refusal == null && IsForeignContainer(target))
            refusal = "tutorial-med-container";

        if (refusal is not { } message)
            return;

        args.Cancel();
        args.OverrideMessage = Loc.GetString(message);
    }

    // a bruise pack used in hand heals whoever holds it
    private void OnTraineeHealed(Entity<TutorialSessionComponent> ent, ref HealingDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.User != ent.Owner)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("tutorial-med-self"), ent.Owner, ent.Owner);
    }

    private LocId? InjectorRefusal(EntityUid user, EntityUid injector, EntityUid target)
    {
        if (HasComp<BloodstreamComponent>(target))
        {
            if (InjectRefusal(user, target) is { } refusal)
                return refusal;

            // an empty syringe is in draw mode and would pull the patient's blood into itself
            if (IsEmpty(injector))
                return "tutorial-med-empty";

            return null;
        }

        // anything but the medicine itself would just swallow what the syringe holds
        return IsForeignContainer(target) ? "tutorial-med-container" : null;
    }

    private LocId? HealingRefusal(EntityUid user, EntityUid target)
    {
        if (target == user)
            return "tutorial-med-self";

        if (IsCorpse(target))
            return "tutorial-med-corpse";

        return null;
    }

    private LocId? InjectRefusal(EntityUid user, EntityUid target)
    {
        if (target == user)
            return "tutorial-med-self";

        if (IsCorpse(target))
            return "tutorial-med-corpse";

        // a dead body does not metabolise, whatever goes in just sits there
        if (_mobState.IsDead(target))
            return "tutorial-med-dead";

        return null;
    }

    private bool IsForeignContainer(EntityUid target)
    {
        if (HasComp<BloodstreamComponent>(target) || HasComp<TutorialMedicineComponent>(target))
            return false;

        return HasComp<InjectableSolutionComponent>(target) || HasComp<DrawableSolutionComponent>(target);
    }

    private bool IsCorpse(EntityUid uid)
    {
        return TryComp<TutorialPatientComponent>(uid, out var patient) && patient.SpawnedDead;
    }

    private bool IsEmpty(EntityUid item)
    {
        foreach (var (_, solution) in _solutions.EnumerateSolutions(item))
        {
            if (solution.Comp.Solution.Volume > 0)
                return false;
        }

        return true;
    }
}
