using System.Linq;
using Content.Server.Medical.Components;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Content.Shared._Polonium.Tutorial.Watchers;
using Content.Shared._Shitmed.Targeting.Events;
using Content.Shared.Buckle.Components;
using Content.Shared.Climbing.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Unit;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Nutrition;
using Content.Shared.Projectiles;
using Content.Shared.Slippery;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// Everything the trainee does reaches the tutorial through here. The handlers decide nothing
/// on their own - they leave a flag on the session and let the polling loop read it, so a step
/// entered after the fact still sees what happened.
/// </summary>
public sealed partial class TutorialConditionTracker
{
    [Dependency] private SharedDisposalUnitSystem _disposal = default!;

    private void OnInteractHand(Entity<TutorialAnchorComponent> anchor, ref InteractHandEvent ev)
    {
        MarkInteract(ev.User, anchor);
    }

    private void OnAfterInteract(Entity<TutorialAnchorComponent> anchor, ref AfterInteractEvent ev)
    {
        MarkInteract(ev.User, anchor);
        if (ev.Target is { } target && TryComp<TutorialAnchorComponent>(target, out var targetAnchor))
            MarkInteract(ev.User, (target, targetAnchor));
    }

    private void OnInteractUsing(Entity<TutorialAnchorComponent> anchor, ref InteractUsingEvent ev)
    {
        MarkInteract(ev.User, anchor);
        if (TryComp<TutorialAnchorComponent>(ev.Used, out var used))
            MarkInteract(ev.User, (ev.Used, used));

        // BaseAnalyzerComponent is abstract, so it is not in the component factory and
        // HasComp on it trips a debug assert. check the concrete analyzer instead
        if (!HasComp<HealthAnalyzerComponent>(ev.Used))
            return;

        SetFlag(ev.User, $"analyzed:{anchor.Comp.AnchorId}");
        Notify(ev.User);
    }

    private void OnActivate(Entity<TutorialAnchorComponent> anchor, ref ActivateInWorldEvent ev)
    {
        MarkInteract(ev.User, anchor);
    }

    private void OnUseInHand(Entity<TutorialAnchorComponent> anchor, ref UseInHandEvent ev)
    {
        MarkInteract(ev.User, anchor);
    }

    private void OnUiOpened(Entity<TutorialAnchorComponent> anchor, ref AfterActivatableUIOpenEvent args)
    {
        SetFlag(args.User, $"ui:{anchor.Comp.AnchorId}");
        Notify(args.User);
    }

    private bool UiOpenFor(EntityUid uid, EntityUid player)
    {
        if (!TryComp<UserInterfaceComponent>(uid, out var ui))
            return false;

        foreach (var actors in ui.Actors.Values)
        {
            if (actors.Contains(player))
                return true;
        }

        return false;
    }

    private void OnStrapped(Entity<TutorialAnchorComponent> strap, ref StrappedEvent args)
    {
        // bed/chair: the buckle might be the npc, flag everyone on this grid
        FlagGrid(strap, $"interact:{strap.Comp.AnchorId}");
    }

    private void MarkInteract(EntityUid user, Entity<TutorialAnchorComponent> anchor)
    {
        SetFlag(user, $"interact:{anchor.Comp.AnchorId}");
        Notify(user);
    }

    private void FlagGrid(EntityUid origin, string flag, bool activity = true)
    {
        if (!TryComp(origin, out TransformComponent? xform) || xform.GridUid is not { } grid)
            return;

        List<EntityUid>? players = null;

        var query = EntityQueryEnumerator<TutorialSessionComponent, TransformComponent>();
        while (query.MoveNext(out var player, out _, out var px))
        {
            if (px.GridUid == grid)
                (players ??= new()).Add(player);
        }

        if (players == null)
            return;

        foreach (var player in players)
        {
            SetFlag(player, flag, activity);
            Notify(player);
        }
    }

    private void OnAcknowledge(TutorialAcknowledgeStepEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return;

        if (session.CurrentStep is not { } current || current.Id != ev.StepId)
            return;

        SetFlag(player, "ack");
        Notify(player);
    }

    private void OnCraftingMenu(TutorialCraftingMenuOpenedEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        SetFlag(player, "crafting");
        Notify(player);
    }

    private void OnConstructionGhost(TutorialConstructionGhostStateEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return;

        if (!_tutorial.TryGetCurrentStep(session, out _, out var step) || !StepWatchesGhost(step, ev.AnchorId))
            return;

        var onFlag = $"ghost:{ev.AnchorId}";
        var strayFlag = $"ghost-stray:{ev.AnchorId}";

        if (ev.OnTile)
            SetFlag(player, onFlag);
        else
            session.Flags.Remove(onFlag);

        if (ev.Stray)
            SetFlag(player, strayFlag);
        else
            session.Flags.Remove(strayFlag);

        Notify(player);
    }

    private bool StepWatchesGhost(TutorialStepPrototype step, string anchorId)
    {
        if (WatchesGhost(step.Completion, anchorId))
            return true;

        return EffectiveWatchers(step).Any(w => w is ConditionWatcher cond && WatchesGhost(cond.Condition, anchorId));
    }

    private static bool WatchesGhost(TutorialCondition? condition, string anchorId)
    {
        return condition switch
        {
            ConstructionGhostOnAnchorCondition ghost => ghost.AnchorId == anchorId,
            AnyCondition any => any.Conditions.Any(c => WatchesGhost(c, anchorId)),
            AllCondition all => all.Conditions.Any(c => WatchesGhost(c, anchorId)),
            _ => false,
        };
    }

    private void OnProbeExamined(Entity<TutorialExamineProbeComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Flag is not { } flag)
            return;

        SetFlag(args.Examiner, flag);
        Notify(args.Examiner);
    }

    private void OnTargetChanged(TargetChangeEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        SetFlag(player, TargetChangedFlag);
        Notify(player);
    }

    private void OnCameraRotated(TutorialCameraRotatedEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        SetFlag(player, "camera");
        Notify(player);
    }

    private void OnAnchorRemoved(Entity<TutorialAnchorComponent> anchor, ref ComponentShutdown args)
    {
        var anchorId = anchor.Comp.AnchorId;
        if (string.IsNullOrWhiteSpace(anchorId))
            return;

        List<EntityUid>? players = null;

        var query = EntityQueryEnumerator<TutorialSessionComponent>();
        while (query.MoveNext(out var player, out _))
            (players ??= new()).Add(player);

        if (players == null)
            return;

        foreach (var player in players)
            Notify(player);
    }

    private void OnExamined(Entity<TutorialAnchorComponent> anchor, ref ExaminedEvent args)
    {
        SetFlag(args.Examiner, $"examined:{anchor.Comp.AnchorId}");
        Notify(args.Examiner);
    }

    private void OnAnchorDamaged(Entity<TutorialAnchorComponent> anchor, ref DamageDealtEvent args)
    {
        if (!args.Damage.AnyPositive())
            return;

        // the flow hurts its own props (the poisoned patient, the meteor), so this alone proves
        // nothing about the trainee. real hits come through OnAttacked and OnProjectileHit
        FlagGrid(anchor, $"damaged:{anchor.Comp.AnchorId}", activity: false);
    }

    private void OnProjectileHit(Entity<ProjectileComponent> proj, ref ProjectileHitEvent args)
    {
        if (args.Shooter is not { } shooter || !HasComp<TutorialSessionComponent>(shooter))
            return;

        if (!TryComp<TutorialAnchorComponent>(args.Target, out var anchor))
            return;

        CountDrillHit(shooter, args.Target);
        SetFlag(shooter, $"damaged:{anchor.AnchorId}");
        Notify(shooter);
    }

    private void OnClimbedOn(Entity<TutorialAnchorComponent> anchor, ref ClimbedOnEvent args)
    {
        SetFlag(args.Climber, $"climb:{anchor.Comp.AnchorId}");
        Notify(args.Climber);
    }

    private void OnStartClimb(Entity<TutorialSessionComponent> ent, ref StartClimbEvent args)
    {
        if (TryComp<TutorialAnchorComponent>(args.Climbable, out var anchor))
            SetFlag(ent.Owner, $"climb:{anchor.AnchorId}");

        Notify(ent.Owner);
    }

    private void OnSlip(Entity<SlipperyComponent> ent, ref SlipEvent args)
    {
        SetFlag(args.Slipped, "slipped");
        Notify(args.Slipped);
    }

    private void OnAttacked(Entity<TutorialAnchorComponent> anchor, ref AttackedEvent args)
    {
        if (!HasComp<TutorialSessionComponent>(args.User))
            return;

        SetFlag(args.User, $"melee:{anchor.Comp.AnchorId}");
        Notify(args.User);
    }

    private void OnDisposalFlush(Entity<DisposalUnitComponent> unit, ref BeforeDisposalFlushEvent args)
    {
        if (!TryComp<TutorialAnchorComponent>(unit, out var unitAnchor))
            return;

        var contents = _disposal.GetContainedEntities((unit.Owner, unit.Comp));
        List<EntityUid>? players = null;

        var query = EntityQueryEnumerator<TutorialSessionComponent>();
        while (query.MoveNext(out var player, out _))
            (players ??= new()).Add(player);

        if (players == null)
            return;

        foreach (var player in players)
        {
            if (!TryComp<TutorialSessionComponent>(player, out var session)
                || !_tutorial.TryGetCurrentStep(session, out _, out var step))
                continue;

            MarkFlushFlags(player, session, step.Completion, unitAnchor.AnchorId, contents);
            Notify(player);
        }
    }

    private void MarkFlushFlags(
        EntityUid player,
        TutorialSessionComponent session,
        TutorialCondition? condition,
        string unitAnchorId,
        IReadOnlyList<EntityUid> contents)
    {
        void Visit(TutorialCondition? cond)
        {
            switch (cond)
            {
                case AnyCondition any:
                    foreach (var c in any.Conditions)
                        Visit(c);
                    break;
                case AllCondition all:
                    foreach (var c in all.Conditions)
                        Visit(c);
                    break;
                case DisposalFlushedWithAnchorCondition flush when flush.DisposalAnchorId == unitAnchorId:
                    if (string.IsNullOrEmpty(flush.ItemAnchorId))
                    {
                        if (contents.Count > 0)
                            SetFlag(player, FlushFlag(flush));
                        break;
                    }

                    foreach (var item in contents)
                    {
                        if (TryComp<TutorialAnchorComponent>(item, out var itemAnchor)
                            && itemAnchor.AnchorId == flush.ItemAnchorId)
                        {
                            SetFlag(player, FlushFlag(flush));
                            break;
                        }
                    }

                    break;
            }
        }

        Visit(condition);
    }

    private static string FlushFlag(DisposalFlushedWithAnchorCondition flush)
    {
        return $"flush:{flush.DisposalAnchorId}:{flush.ItemAnchorId}";
    }

    private void SetFlag(EntityUid player, string flag, bool activity = true)
    {
        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return;

        session.Flags.Add(flag);

        // almost everything that lands here is the trainee doing something, which is exactly the
        // signal the idle skip lock waits for. the tutorial's own damage is the exception
        if (activity)
            session.AutoSkipped = false;
    }

    private void OnTraineeIngesting(Entity<TutorialSessionComponent> ent, ref IngestingEvent args)
    {
        foreach (var reagent in args.Split)
        {
            if (reagent.Quantity <= 0)
                continue;

            ent.Comp.Flags.Add(IngestedReagentCondition.Flag(reagent.Reagent.Prototype));
        }
    }
}
