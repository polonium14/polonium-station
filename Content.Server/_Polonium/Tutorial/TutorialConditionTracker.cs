using System.Linq;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Content.Shared._Polonium.Tutorial.Watchers;
using Content.Shared._Shitmed.Targeting.Events;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Climbing.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Disposal.Components;
using Content.Shared.Examine;
using Content.Shared.Fluids;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Slippery;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Containers;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// Drives a run forward. Every quarter second it asks the current step whether it is done,
/// runs the watchers that comment on what the trainee did wrong, and steps in when nobody
/// has moved for long enough that the run looks stuck.
/// </summary>
public sealed partial class TutorialConditionTracker : EntitySystem
{
    [Dependency] private TutorialSystem _tutorial = default!;
    [Dependency] private TutorialActionExecutor _actions = default!;
    [Dependency] private TutorialMentorSystem _mentor = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(250);

    // a short beat so the trainee sees the action land before the instruction changes
    private static readonly TimeSpan SatisfiedDelay = TimeSpan.FromSeconds(0.35);

    // no step leaves the screen sooner than this. without it a run of steps that were already done
    // on entry - standing on the next spot, the sheet already shut - flashes past unread
    private static readonly TimeSpan MinStepDwell = TimeSpan.FromSeconds(1.5);

    /// <summary>Set on the session once the current step completion has been met at least once.</summary>
    private const string SatisfiedFlag = "completion";

    private const string TargetChangedFlag = "target-changed";

    // a stuck speech must never hold the player longer than this
    private static readonly TimeSpan MaxFreeze = TimeSpan.FromSeconds(60);

    // never-skip steps still get their props put back, they just do not advance on their own
    private static readonly TimeSpan NeverSkipRecoverAfter = TimeSpan.FromSeconds(240);

    // everything used to time out twice as fast as trainees actually need
    private const float SkipTimeScale = 2f;

    private TimeSpan _nextPoll;

    public override void Initialize()
    {
        SubscribeLocalEvent<TutorialAnchorComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<TutorialAnchorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<TutorialAnchorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<TutorialAnchorComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<TutorialAnchorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<TutorialAnchorComponent, AfterActivatableUIOpenEvent>(OnUiOpened);
        SubscribeLocalEvent<TutorialAnchorComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<TutorialAnchorComponent, ComponentShutdown>(OnAnchorRemoved);
        SubscribeLocalEvent<TutorialAnchorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<TutorialAnchorComponent, DamageDealtEvent>(OnAnchorDamaged);
        SubscribeLocalEvent<TutorialAnchorComponent, ClimbedOnEvent>(OnClimbedOn);
        SubscribeLocalEvent<TutorialAnchorComponent, AttackedEvent>(OnAttacked);

        SubscribeLocalEvent<TutorialSessionComponent, StartClimbEvent>(OnStartClimb);
        SubscribeLocalEvent<TutorialSessionComponent, IngestingEvent>(OnTraineeIngesting,
            after: [typeof(MessyDrinkerSystem)]);
        SubscribeLocalEvent<SlipperyComponent, SlipEvent>(OnSlip);

        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<DisposalUnitComponent, BeforeDisposalFlushEvent>(OnDisposalFlush);

        SubscribeNetworkEvent<TutorialAcknowledgeStepEvent>(OnAcknowledge);
        SubscribeNetworkEvent<TutorialCraftingMenuOpenedEvent>(OnCraftingMenu);
        SubscribeNetworkEvent<TutorialConstructionGhostStateEvent>(OnConstructionGhost);
        SubscribeLocalEvent<TutorialExamineProbeComponent, ExaminedEvent>(OnProbeExamined);
        SubscribeNetworkEvent<TutorialCameraRotatedEvent>(OnCameraRotated);
        SubscribeNetworkEvent<TargetChangeEvent>(OnTargetChanged);

        InitializeRange();
    }

    public override void Update(float frameTime)
    {
        AdvanceDue();

        if (_timing.CurTime < _nextPoll)
            return;

        _nextPoll = _timing.CurTime + PollingInterval;

        var query = EntityQueryEnumerator<TutorialSessionComponent>();
        while (query.MoveNext(out var player, out _))
        {
            Notify(player);
            ProcessFreeze(player);
            ProcessStuck(player);
            ProcessGloves(player);
        }
    }

    /// <summary>
    /// A satisfied step flips on the frame its delay runs out rather than on the next quarter second
    /// poll, which on a short delay would otherwise be most of the wait.
    /// </summary>
    private void AdvanceDue()
    {
        List<EntityUid>? due = null;

        var query = EntityQueryEnumerator<TutorialSessionComponent>();
        while (query.MoveNext(out var player, out var session))
        {
            if (session.PendingAdvanceAt is not { } at || _timing.CurTime < at)
                continue;

            // empty body sitting on a satisfied tile must not walk the flow for them
            if (!_players.TryGetSessionByEntity(player, out _))
                continue;

            (due ??= new()).Add(player);
        }

        if (due == null)
            return;

        // advancing can end the flow and take the session component with it, so not mid-query
        foreach (var player in due)
            Notify(player);
    }

    private void ProcessFreeze(EntityUid player)
    {
        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return;

        if (!_players.TryGetSessionByEntity(player, out _))
            return;

        if (!_tutorial.TryGetCurrentStep(session, out _, out var step))
        {
            RemComp<TutorialFrozenComponent>(player);
            return;
        }

        var held = step.Freeze || step.FreezeOnceDone && session.Flags.Contains(SatisfiedFlag);
        var speaking = step.FreezeWhileSpeaking
                       && session.Flags.Contains(TutorialMentorSystem.SpokeFlag)
                       && _mentor.IsSpeaking(player);

        if (!held && !speaking)
        {
            RemComp<TutorialFrozenComponent>(player);
            return;
        }

        if (!TryComp<TutorialFrozenComponent>(player, out var frozen))
        {
            frozen = EnsureComp<TutorialFrozenComponent>(player);
            frozen.ExpiresAt = _timing.CurTime + MaxFreeze;
            return;
        }

        // a step that holds on purpose lets go through its own completion, only a stuck speech times out
        if (!held && _timing.CurTime >= frozen.ExpiresAt)
            RemComp<TutorialFrozenComponent>(player);
    }

    public void Notify(EntityUid player)
    {
        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return;

        if (!_players.TryGetSessionByEntity(player, out _))
            return;

        if (!_tutorial.TryGetCurrentStep(session, out _, out var step))
            return;

        ProcessWatchers(player, session, step);

        // an eject already moved them, the old step has nothing left to decide
        if (session.JumpTo is { } jump)
        {
            _tutorial.JumpToStep((player, session), jump);
            return;
        }

        if (step.Completion is not { } completion)
            return;

        // once the step has been satisfied it stays satisfied. reach conditions are true only
        // while the player stands on the spot, and a player walking to the next door leaves that
        // spot long before the settle delay is up - which used to silently cancel the advance
        if (!session.Flags.Contains(SatisfiedFlag))
        {
            if (!Evaluate(player, session, completion))
            {
                session.PendingAdvanceAt = null;
                return;
            }

            session.Flags.Add(SatisfiedFlag);
        }

        if (session.PendingAdvanceAt == null)
        {
            var afterAction = _timing.CurTime + SatisfiedDelay;
            var afterDwell = session.StepStartedAt + MinStepDwell;
            session.PendingAdvanceAt = afterAction > afterDwell ? afterAction : afterDwell;
        }

        if (_timing.CurTime < session.PendingAdvanceAt)
            return;

        session.PendingAdvanceAt = null;
        _tutorial.ForceAdvance(player);
    }

    private void ProcessWatchers(EntityUid player, TutorialSessionComponent session, TutorialStepPrototype step)
    {
        var watchers = EffectiveWatchers(step);
        for (var i = 0; i < watchers.Count; i++)
        {
            var watcher = watchers[i];
            if (watcher.Once && session.FiredWatchers.Contains(i))
            {
                if (watcher.Rearm && !CheckWatcher(player, session, watcher))
                    session.FiredWatchers.Remove(i);

                continue;
            }

            if (!CheckWatcher(player, session, watcher))
                continue;

            if (watcher.Once)
                session.FiredWatchers.Add(i);

            if (watcher is SlipTeleportWatcher slip)
                _actions.Teleport(player, slip.TeleportAnchor);

            _mentor.Enqueue(player, watcher.Quip);
            _actions.ExecuteAll(player, watcher.Actions);

            if (session.JumpTo != null)
                break;
        }

        session.Flags.Remove("slipped");
    }

    private bool CheckWatcher(EntityUid player, TutorialSessionComponent session, TutorialWatcher watcher)
    {
        return watcher switch
        {
            SlipTeleportWatcher => session.Flags.Contains("slipped"),
            FlagWatcher flag => session.Flags.Contains(flag.Flag),
            AnchorNearWatcher near => CheckAnchorsNear(player, near.AnchorId, near.NearAnchorId, near.Range, near.Away),
            PuddleNearbyWatcher puddle => CheckPuddleNearby(player, session, puddle),
            AbsorbentSpentWatcher => CheckAbsorbentSpent(player),
            HoldingAnchorWatcher hold => CheckHolding(player, hold.AnchorId),
            ConditionWatcher cond => Evaluate(player, session, cond.Condition),
            _ => false,
        };
    }

    /// <summary>
    /// The step's own watchers first, then what its sets add. Indices stay put for as long as the step
    /// does, which is all <see cref="TutorialSessionComponent.FiredWatchers"/> needs of them.
    /// </summary>
    private List<TutorialWatcher> EffectiveWatchers(TutorialStepPrototype step)
    {
        if (step.WatcherSets.Count == 0)
            return step.Watchers;

        var all = new List<TutorialWatcher>(step.Watchers);
        foreach (var id in step.WatcherSets)
        {
            if (_proto.TryIndex(id, out var set))
                all.AddRange(set.Watchers);
        }

        return all;
    }

    private void ProcessStuck(EntityUid player)
    {
        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return;

        if (!_players.TryGetSessionByEntity(player, out _))
            return;

        if (!_tutorial.TryGetCurrentStep(session, out _, out var step))
            return;

        if (step.Blocking || step.Completion is ManualAcknowledgeCondition)
            return;

        // "you seem stuck" on top of a briefing that is still being read out is just rude, and
        // the long speeches in room 17 would trip it every time
        if (_mentor.IsSpeaking(player))
            return;

        // an afk trainee used to get the whole station unlocked for them, one room per timeout.
        // after a skip the flow waits until they do something before it walks on again
        if (session.AutoSkipped && HasMovedSinceSkip(player, session))
            session.AutoSkipped = false;

        var skip = GetSkipSeconds(step);
        var elapsed = _timing.CurTime - session.StepStartedAt;
        var hintAt = skip > 0f ? TimeSpan.FromSeconds(skip * 0.5) : NeverSkipRecoverAfter;

        if (!session.StuckHinted && elapsed >= hintAt)
        {
            session.StuckHinted = true;
            var stepId = session.CurrentStep;
            _actions.TryRecover(player, step);
            if (!TryComp(player, out session) || session.CurrentStep != stepId)
                return;

            _mentor.Enqueue(player, new LocId[] { "tutorial-holopad-stuck-hint" });
        }

        if (skip <= 0f || elapsed < TimeSpan.FromSeconds(skip))
            return;

        if (session.AutoSkipped)
            return;

        session.AutoSkipped = true;
        session.SkippedAt = _transform.GetWorldPosition(player);

        _mentor.Enqueue(player, new LocId[] { "tutorial-holopad-stuck-skip" });
        _tutorial.ForceAdvance(player);
    }

    /// <summary>Walked off far enough that someone is clearly at the keyboard again.</summary>
    private bool HasMovedSinceSkip(EntityUid player, TutorialSessionComponent session)
    {
        if (session.SkippedAt is not { } from)
            return true;

        if (!TryComp(player, out TransformComponent? xform))
            return false;

        return (_transform.GetWorldPosition(xform) - from).LengthSquared() > 4f;
    }

    private static float GetSkipSeconds(TutorialStepPrototype step)
    {
        if (step.StuckSkipSeconds is { } explicitSkip)
            return explicitSkip * SkipTimeScale;

        return (NeedsLongSkip(step.Completion) ? 300f : 180f) * SkipTimeScale;
    }

    private static bool NeedsLongSkip(TutorialCondition? cond)
    {
        return cond switch
        {
            AmeInjectingCondition => true,
            PoweredAnchorCondition => true,
            AnyCondition any => any.Conditions.Any(NeedsLongSkip),
            AllCondition all => all.Conditions.Any(NeedsLongSkip),
            _ => false,
        };
    }
}
