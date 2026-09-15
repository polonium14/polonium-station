using System.Linq;
using System.Numerics;
using Content.Server.Ame.EntitySystems;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.Medical.Components;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Content.Shared._Polonium.Tutorial.Watchers;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Climbing.Events;
using Content.Shared.CombatMode;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Unit;
using Content.Shared.Doors.Components;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Light.Components;
using Content.Shared.Materials;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Paper;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Slippery;
using Content.Shared.Stacks;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wires;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Tutorial;

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
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private MachineFrameSystem _machineFrame = default!;
    [Dependency] private SharedDisposalUnitSystem _disposal = default!;
    [Dependency] private AmeControllerSystem _ame = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;
    [Dependency] private MobStateSystem _mobs = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;
    [Dependency] private PlantTraySystem _plantTray = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TutorialEyeRecoverySystem _eyes = default!;
    [Dependency] private TutorialItemCountSystem _itemCount = default!;
    [Dependency] private SharedMaterialStorageSystem _materials = default!;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(250);
    // a short beat so the trainee sees the action land before the instruction changes
    private static readonly TimeSpan SatisfiedDelay = TimeSpan.FromSeconds(0.35);

    // no step leaves the screen sooner than this. without it a run of steps that were already done
    // on entry - standing on the next spot, the sheet already shut - flashes past unread
    private static readonly TimeSpan MinStepDwell = TimeSpan.FromSeconds(1.5);

    /// <summary>Set on the session once the current step completion has been met at least once.</summary>
    private const string SatisfiedFlag = "completion";

    // a stuck speech must never hold the player longer than this
    private static readonly TimeSpan MaxFreeze = TimeSpan.FromSeconds(60);

    // never-skip steps still get their props put back, they just do not advance on their own
    private static readonly TimeSpan NeverSkipRecoverAfter = TimeSpan.FromSeconds(120);

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
        SubscribeLocalEvent<SlipperyComponent, SlipEvent>(OnSlip);

        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<DisposalUnitComponent, BeforeDisposalFlushEvent>(OnDisposalFlush);

        SubscribeNetworkEvent<TutorialAcknowledgeStepEvent>(OnAcknowledge);
        SubscribeNetworkEvent<TutorialCraftingMenuOpenedEvent>(OnCraftingMenu);
        SubscribeNetworkEvent<TutorialConstructionGhostStateEvent>(OnConstructionGhost);
        SubscribeLocalEvent<TutorialExamineProbeComponent, ExaminedEvent>(OnProbeExamined);
        SubscribeNetworkEvent<TutorialCameraRotatedEvent>(OnCameraRotated);
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
            if (session.PendingAdvanceAt is { } at && _timing.CurTime >= at)
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
        for (var i = 0; i < step.Watchers.Count; i++)
        {
            var watcher = step.Watchers[i];
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
            PuddleNearbyWatcher puddle => CheckPuddleNearby(player, puddle),
            AbsorbentSpentWatcher => CheckAbsorbentSpent(player),
            HoldingAnchorWatcher hold => CheckHolding(player, hold.AnchorId),
            ConditionWatcher cond => Evaluate(player, session, cond.Condition),
            _ => false,
        };
    }

    public bool Evaluate(EntityUid player, TutorialSessionComponent session, TutorialCondition condition)
    {
        return condition switch
        {
            AnyCondition any => any.Conditions.Any(c => Evaluate(player, session, c)),
            AllCondition all => all.Conditions.Count > 0 && all.Conditions.All(c => Evaluate(player, session, c)),
            ReachAnchorCondition reach => CheckReach(player, reach.AnchorId, reach.Range),
            AnyReachAnchorsCondition anyReach => anyReach.AnchorIds.Any(id => CheckReach(player, id, anyReach.Range)),
            CrawlingReachCondition crawl => _standing.IsDown(player) && crawl.AnchorIds.Any(id => CheckReach(player, id, crawl.Range)),
            BothHandsAnchorsCondition both => CheckBothHands(player, both.AnchorIds),
            NotCondition not => !Evaluate(player, session, not.Condition),
            SlotContainsAnchorRecursiveCondition slot => CheckSlotContainsRecursive(player, slot),
            ToolQualitiesCondition tools => CheckToolQualities(player, tools),
            DoorStateAnchorCondition doorState => AnyAnchor(player, doorState.AnchorId,
                uid => TryComp<DoorComponent>(uid, out var door) && door.State == doorState.State),
            ItemPulledCondition pull => CheckPulling(player, session, pull),
            AnchorsNearCondition near => CheckAnchorsNear(player, near.AnchorId, near.NearAnchorId, near.Range),
            ManualAcknowledgeCondition => session.Flags.Contains("ack"),
            InternalsOnCondition => _internals.AreInternalsWorking(player),
            DrainableReagentNearbyCondition reagent => CheckDrainableReagent(player, reagent),
            AnchorSolutionContainsCondition sol => CheckAnchorSolution(player, sol),
            PrototypeSolutionContainsCondition protoSol => CheckPrototypeSolution(player, protoSol),
            PlantTrayLevelsCondition levels => AnyAnchor(player, levels.AnchorId,
                uid => TryComp<PlantTrayComponent>(uid, out var tray)
                       && tray.WaterLevel >= levels.Water
                       && tray.NutritionLevel >= levels.Nutrition),
            UiOpenedAnchorCondition ui => session.Flags.Contains($"ui:{ui.AnchorId}"),
            // no anchor at all reads as closed on purpose - a trainee who ate the sheet or threw
            // it down a disposal is not left standing in front of a step that can never finish
            UiClosedAnchorCondition uiShut => !AnyAnchor(player, uiShut.AnchorId, uid => UiOpenFor(uid, player)),
            CombatModeCondition combat => (TryComp<CombatModeComponent>(player, out var mode)
                                           && mode.IsInCombatMode) == combat.Enabled,
            WiresPanelOpenCondition panel => AnyAnchor(player, panel.AnchorId,
                uid => TryComp<WiresPanelComponent>(uid, out var wires) && wires.Open == panel.Open),
            PlantTrayPlantedCondition tray => AnyAnchor(player, tray.AnchorId,
                uid => _plantTray.TryGetPlant((uid, null), out _)),
            DoorBoltedAnchorCondition bolts => AnyAnchor(player, bolts.AnchorId,
                uid => TryComp<DoorBoltComponent>(uid, out var bolt) && bolt.BoltsDown == bolts.Bolted),
            EntityDeletedCondition del => CheckDeleted(session, del.AnchorId),
            ClimbAnchorCondition climb => session.Flags.Contains($"climb:{climb.AnchorId}"),
            BuckledToAnchorCondition buckle => buckle.EntityAnchorId is { } sitter
                ? AnyAnchor(player, sitter, uid => BuckledTo(uid, buckle.AnchorId))
                : BuckledTo(player, buckle.AnchorId),
            AnchorAmmoFullCondition ammo => CountFullAmmo(player, ammo.AnchorId) >= ammo.Count,
            AnchorAmmoEmptyCondition drained => AnyAnchor(player, drained.AnchorId, uid =>
            {
                var ev = new GetAmmoCountEvent();
                RaiseLocalEvent(uid, ref ev);
                return ev.Capacity > 0 && ev.Count == 0;
            }),
            // the map spawner keeps its anchor next to the mob it spawned and has no damage at all,
            // which would read as fully healed the moment the step starts
            AnchorDamageBelowCondition hurt => AnyAnchor(player, hurt.AnchorId,
                uid => HasComp<DamageableComponent>(uid) && DamageOf(uid, hurt.DamageType) <= hurt.Max),
            AnchorInsideAnchorCondition inside => AnyAnchor(player, inside.AnchorId, uid =>
                _container.TryGetContainingContainer((uid, null, null), out var holder)
                && TryComp<TutorialAnchorComponent>(holder.Owner, out var box)
                && box.AnchorId == inside.ContainerAnchorId),
            ItemSlotFilledCondition slot => AnyAnchor(player, slot.AnchorId,
                uid => _container.TryGetContainer(uid, slot.Slot, out var held) && held.ContainedEntities.Count > 0),
            UnbuckledCondition => !TryComp<BuckleComponent>(player, out var buckle) || !buckle.Buckled,
            CameraRotatedCondition => session.Flags.Contains("camera"),
            ExaminedAnchorCondition exam => session.Flags.Contains($"examined:{exam.AnchorId}"),
            ItemToggledCondition toggle => CheckToggled(player, toggle),
            EntityStunnedCondition stun => AnyAnchor(player, stun.AnchorId, uid => HasComp<StunnedComponent>(uid) || HasComp<KnockedDownComponent>(uid)),
            CuffedAnchorCondition cuff => AnyAnchor(player, cuff.AnchorId, uid => TryComp<CuffableComponent>(uid, out var c) && c.CuffedHandCount > 0),
            PaperSignedCondition paper => AnyAnchor(player, paper.AnchorId, IsSigned),
            PoweredAnchorCondition powered => AnyAnchor(player, powered.AnchorId, uid => _power.IsPowered(uid)),
            AmeInjectingCondition ame => AnyAnchor(player, ame.AnchorId, uid => _ame.IsInjecting(uid)),
            WearingSlotCondition wear => _inventory.TryGetSlotEntity(player, wear.Slot, out _),
            PuddlesClearedCondition puddles => CheckPuddlesCleared(player, puddles),
            DisposalFlushedWithAnchorCondition flush => session.Flags.Contains(FlushFlag(flush)),
            HealthAnalyzedCondition scan => session.Flags.Contains($"analyzed:{scan.AnchorId}"),
            MeleeHitAnchorCondition melee => session.Flags.Contains($"melee:{melee.AnchorId}"),
            AnchorDamagedCondition dmg => CheckDamaged(player, session, dmg.AnchorId),
            DeadAnchorCondition dead => AnyAnchor(player, dead.AnchorId, uid => _mobs.IsDead(uid)),
            AnchorAliveCondition alive => AnyAnchor(player, alive.AnchorId,
                uid => HasComp<MobStateComponent>(uid) && _mobs.IsAlive(uid)),
            CraftingMenuOpenedCondition => session.Flags.Contains("crafting"),
            ConstructionGhostOnAnchorCondition ghost => session.Flags.Contains(
                ghost.Stray ? $"ghost-stray:{ghost.AnchorId}" : $"ghost:{ghost.AnchorId}"),
            ExaminedNearAnchorCondition examinedNear => CheckExaminedNear(player, session, examinedNear),
            MachineFrameCompleteNearAnchorCondition frameDone => CheckFrameComplete(player, frameDone),
            AnchorEmptyOrGoneCondition empty => CheckEmptyOrGone(session, empty.AnchorId),
            HoldingAnchorCondition hold => CheckHolding(player, hold.AnchorId),
            HoldingPrototypeCondition holdProto => CheckHoldingPrototype(player, holdProto.Prototype),
            PrototypeNearAnchorCondition near => CountPrototype(player, near) >= near.Count,
            PrototypesOnAnchorsCondition onAnchors => CheckPrototypesOnAnchors(player, onAnchors),
            EyeProtectedCondition => IsEyeProtected(player),
            EyesHurtCondition => _eyes.EyesHurt(player),
            LatheMaterialsLoadedCondition loaded => AnyAnchor(player, loaded.AnchorId,
                uid => loaded.Materials.All(material => _materials.GetMaterialAmount(uid, material.Id) > 0)),
            LatheGuideGatheredCondition => CheckLatheGuideGathered(player, session),
            StackAmountNearCondition stackNear => CountStackNear(player, stackNear) >= stackNear.Min,
            MentorFinishedSpeakingCondition => session.Flags.Contains(TutorialMentorSystem.SpokeFlag)
                                               && !_mentor.IsSpeaking(player),
            _ => false,
        };
    }

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
        // stasis/chair: the buckle might be the npc, flag everyone on this grid
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

        var query = EntityQueryEnumerator<TutorialSessionComponent, TransformComponent>();
        while (query.MoveNext(out var player, out _, out var px))
        {
            if (px.GridUid != grid)
                continue;

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

    private static bool StepWatchesGhost(TutorialStepPrototype step, string anchorId)
    {
        if (WatchesGhost(step.Completion, anchorId))
            return true;

        return step.Watchers.Any(w => w is ConditionWatcher cond && WatchesGhost(cond.Condition, anchorId));
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

    /// <summary>
    /// Every construction stage is a new entity, so tag whatever stands at the anchor right now and let
    /// an examine of any tagged one count. The flag is per step, the tags are simply left behind.
    /// </summary>
    private bool CheckExaminedNear(EntityUid player, TutorialSessionComponent session, ExaminedNearAnchorCondition cond)
    {
        var flag = $"examined-near:{cond.AnchorId}";
        if (session.Flags.Contains(flag))
            return true;

        foreach (var anchor in AnchorsOnGrid(player, cond.AnchorId))
        {
            foreach (var uid in _lookup.GetEntitiesInRange(Transform(anchor).Coordinates, cond.Range))
            {
                if (MetaData(uid).EntityPrototype?.ID is not { } id || !cond.Prototypes.Any(proto => proto.Id == id))
                    continue;

                EnsureComp<TutorialExamineProbeComponent>(uid).Flag = flag;
            }

            break;
        }

        return false;
    }

    private bool CheckFrameComplete(EntityUid player, MachineFrameCompleteNearAnchorCondition cond)
    {
        foreach (var anchor in AnchorsOnGrid(player, cond.AnchorId))
        {
            foreach (var uid in _lookup.GetEntitiesInRange(Transform(anchor).Coordinates, cond.Range))
            {
                if (TryComp<MachineFrameComponent>(uid, out var frame) && _machineFrame.IsComplete(frame))
                    return true;
            }

            break;
        }

        return false;
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

        var query = EntityQueryEnumerator<TutorialSessionComponent>();
        while (query.MoveNext(out var player, out _))
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
        var query = EntityQueryEnumerator<TutorialSessionComponent>();
        while (query.MoveNext(out var player, out var session))
        {
            if (!_tutorial.TryGetCurrentStep(session, out _, out var step))
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

    private bool CheckReach(EntityUid player, string anchorId, float range)
    {
        if (!TryComp(player, out TransformComponent? playerXform) || playerXform.GridUid is not { } grid)
            return false;

        var playerPos = _transform.GetWorldPosition(playerXform);
        var rangeSq = range * range;

        var query = EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out _, out var anchor, out var anchorXform))
        {
            if (anchor.AnchorId != anchorId || anchorXform.GridUid != grid)
                continue;

            if ((_transform.GetWorldPosition(anchorXform) - playerPos).LengthSquared() <= rangeSq)
                return true;
        }

        return false;
    }

    private bool CheckBothHands(EntityUid player, List<string> anchorIds)
    {
        var found = new HashSet<string>();
        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (TryComp<TutorialAnchorComponent>(held, out var anchor) && anchorIds.Contains(anchor.AnchorId))
                found.Add(anchor.AnchorId);
        }

        return anchorIds.Count > 0 && anchorIds.All(found.Contains);
    }

    private bool CheckSlotContainsRecursive(EntityUid player, SlotContainsAnchorRecursiveCondition cond)
    {
        if (cond.AnchorIds.Count == 0)
            return false;

        if (!_inventory.TryGetSlotEntity(player, cond.Slot, out var slotEntity))
        {
            return !cond.Strict && InventoryHasAnchors(player, cond.AnchorIds);
        }

        var found = new HashSet<string>();
        CollectAnchorsRecursive(slotEntity.Value, found, cond.AnchorIds);
        if (cond.AnchorIds.All(found.Contains))
            return true;

        return !cond.Strict && InventoryHasAnchors(player, cond.AnchorIds);
    }

    private bool CheckToolQualities(EntityUid player, ToolQualitiesCondition cond)
    {
        if (cond.Qualities.Count == 0)
            return false;

        var carried = new List<EntityUid>();
        if (cond.Slot is { } slot)
        {
            if (!_inventory.TryGetSlotEntity(player, slot, out var worn))
                return false;

            CollectEntitiesRecursive(worn.Value, carried);
        }
        else
        {
            EntityUid? skipped = null;
            if (cond.OutsideSlot is { } outside && _inventory.TryGetSlotEntity(player, outside, out var skip))
                skipped = skip;

            var slots = _inventory.GetSlotEnumerator(player);
            while (slots.NextItem(out var item))
            {
                if (item == skipped)
                    continue;

                carried.Add(item);
                CollectEntitiesRecursive(item, carried);
            }

            foreach (var held in _hands.EnumerateHeld(player))
            {
                if (held == skipped)
                    continue;

                carried.Add(held);
                CollectEntitiesRecursive(held, carried);
            }
        }

        bool Carries(ProtoId<ToolQualityPrototype> quality) => carried.Any(uid => _tool.HasQuality(uid, quality.Id));

        return cond.RequireAll
            ? cond.Qualities.All(Carries)
            : cond.Qualities.Any(Carries);
    }

    private void CollectEntitiesRecursive(EntityUid uid, List<EntityUid> found)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var containers))
            return;

        foreach (var container in _container.GetAllContainers(uid, containers))
        {
            foreach (var child in container.ContainedEntities)
            {
                found.Add(child);
                CollectEntitiesRecursive(child, found);
            }
        }
    }

    private bool InventoryHasAnchors(EntityUid player, List<string> wanted)
    {
        var found = new HashSet<string>();
        var slots = _inventory.GetSlotEnumerator(player);
        while (slots.NextItem(out var item))
            CollectAnchorsRecursive(item, found, wanted);

        foreach (var held in _hands.EnumerateHeld(player))
            CollectAnchorsRecursive(held, found, wanted);

        return wanted.Count > 0 && wanted.All(found.Contains);
    }

    private void CollectAnchorsRecursive(EntityUid uid, HashSet<string> found, IReadOnlyCollection<string> wanted)
    {
        if (TryComp<TutorialAnchorComponent>(uid, out var anchor) && wanted.Contains(anchor.AnchorId))
            found.Add(anchor.AnchorId);

        if (found.Count == wanted.Count)
            return;

        // a pen or a plushie holds nothing, and enumerating its containers throws
        if (!TryComp<ContainerManagerComponent>(uid, out var containers))
            return;

        foreach (var container in _container.GetAllContainers(uid, containers))
        {
            foreach (var child in container.ContainedEntities)
            {
                CollectAnchorsRecursive(child, found, wanted);
                if (found.Count == wanted.Count)
                    return;
            }
        }
    }

    private bool CheckPulling(EntityUid player, TutorialSessionComponent session, ItemPulledCondition cond)
    {
        if (!TryComp<PullerComponent>(player, out var puller) || puller.Pulling is not { } pulling)
            return false;

        return TryComp<TutorialAnchorComponent>(pulling, out var anchor) && anchor.AnchorId == cond.AnchorId
               || session.Anchors.TryGetValue(cond.AnchorId, out var target) && pulling == target;
    }

    private bool CheckDeleted(TutorialSessionComponent session, string anchorId)
    {
        if (!session.Anchors.TryGetValue(anchorId, out var uid))
            return false;

        return Deleted(uid);
    }

    private bool CheckDamaged(EntityUid player, TutorialSessionComponent session, string anchorId)
    {
        if (session.Flags.Contains($"damaged:{anchorId}"))
            return true;

        if (session.Anchors.TryGetValue(anchorId, out var uid) && Deleted(uid))
            return true;

        return AnyAnchor(player, anchorId, HasAnyDamage);
    }

    private bool HasAnyDamage(EntityUid uid)
    {
        return TryComp<DamageableComponent>(uid, out var dmg)
               && _damageable.GetPositiveDamage((uid, dmg)).AnyPositive();
    }

    private bool CheckEmptyOrGone(TutorialSessionComponent session, string anchorId)
    {
        if (!session.Anchors.TryGetValue(anchorId, out var uid) || Deleted(uid))
            return true;

        var volume = 0f;
        var any = false;
        foreach (var (_, solutionEnt) in _solution.EnumerateSolutions((uid, null)))
        {
            any = true;
            volume += solutionEnt.Comp.Solution.Volume.Float();
        }

        return any && volume <= 0.01f;
    }

    private bool BuckledTo(EntityUid who, string strapAnchorId)
    {
        if (!TryComp<BuckleComponent>(who, out var buckle) || !buckle.Buckled || buckle.BuckledTo is not { } strap)
            return false;

        return TryComp<TutorialAnchorComponent>(strap, out var anchor) && anchor.AnchorId == strapAnchorId;
    }

    private int CountFullAmmo(EntityUid player, string anchorId)
    {
        var full = 0;
        foreach (var uid in AnchorsOnGrid(player, anchorId))
        {
            // the event instead of the component, the ballistic provider is locked to the gun system
            var ev = new GetAmmoCountEvent();
            RaiseLocalEvent(uid, ref ev);
            if (ev.Capacity > 0 && ev.Count >= ev.Capacity)
                full++;
        }

        return full;
    }

    private float DamageOf(EntityUid uid, ProtoId<DamageTypePrototype> type)
    {
        if (!TryComp<DamageableComponent>(uid, out var dmg))
            return 0f;

        return _damageable.GetPositiveDamage((uid, dmg)).DamageDict.TryGetValue(type.Id, out var amount)
            ? amount.Float()
            : 0f;
    }

    private bool CheckToggled(EntityUid player, ItemToggledCondition toggle)
    {
        return AnyAnchor(player, toggle.AnchorId, uid =>
        {
            if (TryComp<ItemToggleComponent>(uid, out var item))
                return item.Activated == toggle.Activated;

            // lanterns and flashlights run their own toggle and never get an ItemToggle
            if (TryComp<HandheldLightComponent>(uid, out var light))
                return light.Activated == toggle.Activated;

            return false;
        });
    }

    private bool CheckHolding(EntityUid player, string anchorId)
    {
        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (TryComp<TutorialAnchorComponent>(held, out var anchor) && anchor.AnchorId == anchorId)
                return true;
        }

        return false;
    }

    /// <summary>
    /// A mop works by trading the water it holds for whatever is on the floor. Once the water is
    /// gone it silently stops working, which looks like a broken tutorial step.
    /// </summary>
    private bool CheckAbsorbentSpent(EntityUid player)
    {
        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (!TryComp<AbsorbentComponent>(held, out var absorbent))
                continue;

            if (!_solution.TryGetSolution(held, absorbent.SolutionName, out _, out var solution))
                continue;

            if (solution.Volume <= 0)
                continue;

            if (solution.GetTotalPrototypeQuantity(_puddle.GetAbsorbentReagents(solution)) <= 0)
                return true;
        }

        return false;
    }

    private bool CheckHoldingPrototype(EntityUid player, string proto)
    {
        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (MetaData(held).EntityPrototype?.ID == proto)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Counts matching entities lying around the anchor plus whatever the trainee is carrying,
    /// because half the time the ingredients are already in their hands or backpack.
    /// </summary>
    private int CountPrototype(EntityUid player, PrototypeNearAnchorCondition cond)
    {
        var seen = new HashSet<EntityUid>();
        var origin = Transform(player).Coordinates;

        if (cond.AnchorId is { } anchorId)
        {
            var found = false;
            foreach (var anchor in AnchorsOnGrid(player, anchorId))
            {
                // "inside the microwave" is a question about its contents, nothing else
                if (cond.Inside)
                    return CountPrototypeIn(anchor, cond.Prototype);

                origin = Transform(anchor).Coordinates;
                found = true;
                break;
            }

            if (!found)
                return 0;
        }

        foreach (var uid in _lookup.GetEntitiesInRange(origin, cond.Range))
            seen.Add(uid);

        if (!cond.IgnoreCarried)
        {
            foreach (var held in _hands.EnumerateHeld(player))
                seen.Add(held);

            var slots = _inventory.GetSlotEnumerator(player);
            while (slots.NextItem(out var item))
                seen.Add(item);
        }

        var count = 0;
        foreach (var uid in seen)
        {
            // a bolted down thing is never inside anything, so no need to look into containers
            if (cond.Anchored)
                count += Transform(uid).Anchored && MetaData(uid).EntityPrototype?.ID == cond.Prototype.Id ? 1 : 0;
            else
                count += CountPrototypeIn(uid, cond.Prototype);
        }

        return count;
    }

    private int CountPrototypeIn(EntityUid uid, string proto)
    {
        var count = MetaData(uid).EntityPrototype?.ID == proto ? 1 : 0;

        if (!TryComp<ContainerManagerComponent>(uid, out var containers))
            return count;

        foreach (var container in _container.GetAllContainers(uid, containers))
        {
            foreach (var child in container.ContainedEntities)
                count += CountPrototypeIn(child, proto);
        }

        return count;
    }

    private bool CheckPrototypesOnAnchors(EntityUid player, PrototypesOnAnchorsCondition cond)
    {
        var tiles = new HashSet<(EntityUid, Vector2i)>();
        var markers = new List<(EntityUid Uid, (EntityUid, Vector2i) Tile)>();

        foreach (var id in cond.AnchorIds)
        {
            var anchor = AnchorsOnGrid(player, id).FirstOrDefault();
            if (!anchor.IsValid() || TileOf(anchor) is not { } tile)
                return false;

            tiles.Add(tile);
            markers.Add((anchor, tile));
        }

        if (cond.Stray)
        {
            foreach (var (uid, _) in markers)
            {
                foreach (var near in _lookup.GetEntitiesInRange(Transform(uid).Coordinates, cond.StrayRange))
                {
                    if (Matches(near) && TileOf(near) is { } tile && !tiles.Contains(tile))
                        return true;
                }
            }

            return false;
        }

        foreach (var (uid, tile) in markers)
        {
            // a flatpack dropped at the cursor can land anywhere on the tile, so look a bit past the centre
            var filled = false;
            foreach (var near in _lookup.GetEntitiesInRange(Transform(uid).Coordinates, 0.75f))
            {
                if (Matches(near) && TileOf(near) == tile && RotationFits(near, uid))
                {
                    filled = true;
                    break;
                }
            }

            if (!filled)
                return false;
        }

        return true;

        bool Matches(EntityUid uid)
        {
            if (MetaData(uid).EntityPrototype is not { } proto
                || !cond.Prototypes.Any(p => p.Id == proto.ID)
                || _container.IsEntityInContainer(uid))
                return false;

            return !cond.Anchored || Transform(uid).Anchored;
        }

        bool RotationFits(EntityUid uid, EntityUid marker)
        {
            if (cond.Rotation == TutorialRotationCheck.Any)
                return true;

            var same = Transform(uid).LocalRotation.GetCardinalDir() == Transform(marker).LocalRotation.GetCardinalDir();
            return same == (cond.Rotation == TutorialRotationCheck.Match);
        }
    }

    private bool CheckLatheGuideGathered(EntityUid player, TutorialSessionComponent session)
    {
        if (!_tutorial.TryGetCurrentStep(session, out _, out var step) || step.LatheGuide is not { } guide)
            return false;

        var places = new List<EntityUid>();
        places.AddRange(AnchorsOnGrid(player, guide.Lathe));
        foreach (var id in guide.CountAt)
            places.AddRange(AnchorsOnGrid(player, id));

        var gathered = _itemCount.Gather(player, places);
        return guide.Items.All(item => _itemCount.CountRecipe(item.Recipe, gathered) >= item.Count);
    }

    private int CountStackNear(EntityUid player, StackAmountNearCondition cond)
    {
        var places = cond.AnchorId is { } anchorId
            ? AnchorsOnGrid(player, anchorId).ToList()
            : new List<EntityUid> { player };

        var total = 0;
        foreach (var uid in _itemCount.Gather(player, places, cond.Range))
        {
            if (TryComp<StackComponent>(uid, out var stack) && stack.StackTypeId == cond.StackType)
                total += stack.Count;
        }

        return total;
    }

    // the same question the welder asks before it burns anyone's eyes
    private bool IsEyeProtected(EntityUid player)
    {
        var ev = new GetEyeProtectionEvent();
        RaiseLocalEvent(player, ev);
        return ev.Protection > TimeSpan.Zero;
    }

    private (EntityUid, Vector2i)? TileOf(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return null;

        return (grid, _map.TileIndicesFor(grid, gridComp, xform.Coordinates));
    }

    /// <summary>
    /// Adds up a reagent across everything the trainee could put in a machine - on the floor
    /// nearby, in their hands, in their bags - counting only containers that will hand it over.
    /// </summary>
    private bool CheckDrainableReagent(EntityUid player, DrainableReagentNearbyCondition cond)
    {
        var origin = Transform(player).Coordinates;

        if (cond.AnchorId is { } anchorId)
        {
            var found = false;
            foreach (var anchor in AnchorsOnGrid(player, anchorId))
            {
                if (cond.Inside)
                    return SumDrainable(anchor, cond.Reagent) >= cond.Amount;

                origin = Transform(anchor).Coordinates;
                found = true;
                break;
            }

            if (!found)
                return false;
        }

        var seen = new HashSet<EntityUid>();

        foreach (var uid in _lookup.GetEntitiesInRange(origin, cond.Range))
            seen.Add(uid);

        foreach (var held in _hands.EnumerateHeld(player))
            seen.Add(held);

        var slots = _inventory.GetSlotEnumerator(player);
        while (slots.NextItem(out var item))
            seen.Add(item);

        var total = 0f;
        foreach (var uid in seen)
        {
            total += SumDrainable(uid, cond.Reagent);
            if (total >= cond.Amount)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Adds up a reagent across every container of one prototype the trainee could have left
    /// lying around. Narrow on purpose - a kitchen sink is full of water and would otherwise
    /// answer for the beaker.
    /// </summary>
    private bool CheckPrototypeSolution(EntityUid player, PrototypeSolutionContainsCondition cond)
    {
        var total = 0f;

        foreach (var uid in ReachableEntities(player, cond.Range))
        {
            if (MetaData(uid).EntityPrototype?.ID != cond.Prototype.Id)
                continue;

            if (cond.Solution is { } name)
            {
                if (_solution.TryGetSolution(uid, name, out _, out var single))
                    total += single.GetTotalPrototypeQuantity(cond.Reagent).Float();
            }
            else
            {
                foreach (var (_, soln) in _solution.EnumerateSolutions((uid, null)))
                    total += soln.Comp.Solution.GetTotalPrototypeQuantity(cond.Reagent).Float();
            }

            if (total >= cond.Amount)
                return true;
        }

        return false;
    }

    /// <summary>Everything around the trainee plus whatever they are carrying, bags included.</summary>
    private HashSet<EntityUid> ReachableEntities(EntityUid player, float range)
    {
        var seen = new HashSet<EntityUid>();

        foreach (var uid in _lookup.GetEntitiesInRange(Transform(player).Coordinates, range))
            Collect(uid, seen);

        foreach (var held in _hands.EnumerateHeld(player))
            Collect(held, seen);

        var slots = _inventory.GetSlotEnumerator(player);
        while (slots.NextItem(out var item))
            Collect(item, seen);

        return seen;
    }

    private void Collect(EntityUid uid, HashSet<EntityUid> seen)
    {
        if (!seen.Add(uid) || !TryComp<ContainerManagerComponent>(uid, out var containers))
            return;

        foreach (var container in _container.GetAllContainers(uid, containers))
        {
            foreach (var child in container.ContainedEntities)
                Collect(child, seen);
        }
    }

    /// <summary>
    /// What the anchor is holding itself. A hydroponics tray only turns soil reagents into water
    /// and nutrient levels once something is planted, so before planting this is the only way to
    /// tell that the trainee watered and fertilised it.
    /// </summary>
    private bool CheckAnchorSolution(EntityUid player, AnchorSolutionContainsCondition cond)
    {
        foreach (var uid in AnchorsOnGrid(player, cond.AnchorId))
        {
            var total = 0f;

            if (cond.Solution is { } name)
            {
                if (_solution.TryGetSolution(uid, name, out _, out var single))
                    total = single.GetTotalPrototypeQuantity(cond.Reagent).Float();
            }
            else
            {
                foreach (var (_, soln) in _solution.EnumerateSolutions((uid, null)))
                    total += soln.Comp.Solution.GetTotalPrototypeQuantity(cond.Reagent).Float();
            }

            if (total >= cond.Amount)
                return true;
        }

        return false;
    }

    private float SumDrainable(EntityUid uid, ProtoId<ReagentPrototype> reagent)
    {
        var total = 0f;

        if (_solution.TryGetDrainableSolution(uid, out _, out var solution))
            total += solution.GetTotalPrototypeQuantity(reagent).Float();

        if (!TryComp<ContainerManagerComponent>(uid, out var containers))
            return total;

        foreach (var container in _container.GetAllContainers(uid, containers))
        {
            foreach (var child in container.ContainedEntities)
                total += SumDrainable(child, reagent);
        }

        return total;
    }

    private bool CheckPuddleNearby(EntityUid player, PuddleNearbyWatcher watcher)
    {
        if (!TryComp(player, out TransformComponent? xform))
            return false;

        // anything at or below this is a dribble, not a spill
        var floor = MathF.Max(watcher.MinVolume, 0.01f);

        foreach (var uid in _lookup.GetEntitiesInRange(xform.Coordinates, watcher.Range))
        {
            if (!TryComp<PuddleComponent>(uid, out var puddle) || _container.IsEntityInContainer(uid))
                continue;

            if (!_solution.TryGetSolution(uid, puddle.SolutionName, out _, out var solution))
                continue;

            var volume = watcher.Reagent is { } reagent
                ? solution.GetTotalPrototypeQuantity(reagent)
                : solution.Volume;

            if (volume.Float() >= floor)
                return true;
        }

        return false;
    }

    private bool CheckPuddlesCleared(EntityUid player, PuddlesClearedCondition cond)
    {
        if (!TryComp(player, out TransformComponent? xform) || xform.GridUid is not { } grid)
            return false;

        // scoped version only looks around the anchor, a spill two rooms back is not this step's problem
        var origin = Vector2.Zero;
        var scoped = false;
        if (cond.NearAnchorId is { } anchorId)
        {
            foreach (var anchor in AnchorsOnGrid(player, anchorId))
            {
                origin = _transform.GetWorldPosition(anchor);
                scoped = true;
                break;
            }
        }

        var rangeSq = cond.Range * cond.Range;

        var query = EntityQueryEnumerator<PuddleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var puddle, out var px))
        {
            if (px.GridUid != grid)
                continue;

            if (_container.IsEntityInContainer(uid))
                continue;

            if (scoped && (_transform.GetWorldPosition(px) - origin).LengthSquared() > rangeSq)
                continue;

            // the mop leaves plain water behind and that dries up by itself. making the player
            // stand around watching it evaporate is not a lesson, so treat it as already clean
            if (IsSelfDrying(uid, puddle))
                continue;

            return false;
        }

        return true;
    }

    private bool IsSelfDrying(EntityUid uid, PuddleComponent puddle)
    {
        if (!_solution.ResolveSolution(uid, puddle.SolutionName, ref puddle.Solution, out var solution))
            return false;

        if (solution.Volume <= 0)
            return true;

        var evaporating = solution.GetTotalPrototypeQuantity(_puddle.GetEvaporatingReagents(solution));
        return solution.Volume - evaporating <= 0;
    }

    private bool CheckAnchorsNear(EntityUid player, string a, string b, float range, bool away = false)
    {
        var closest = float.MaxValue;

        foreach (var left in AnchorsOnGrid(player, a))
        foreach (var right in AnchorsOnGrid(player, b))
        {
            var delta = _transform.GetWorldPosition(left) - _transform.GetWorldPosition(right);
            closest = MathF.Min(closest, delta.LengthSquared());
        }

        // neither "near" nor "away" means anything when one of the two is gone
        if (closest is float.MaxValue)
            return false;

        var rangeSq = range * range;
        return away ? closest > rangeSq : closest <= rangeSq;
    }

    private bool AnyAnchor(EntityUid player, string anchorId, Func<EntityUid, bool> pred)
    {
        foreach (var uid in AnchorsOnGrid(player, anchorId))
        {
            if (pred(uid))
                return true;
        }

        return false;
    }

    private IEnumerable<EntityUid> AnchorsOnGrid(EntityUid player, string anchorId)
    {
        if (!TryComp(player, out TransformComponent? xform) || xform.GridUid is not { } grid)
            yield break;

        var query = EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anchor, out var ax))
        {
            if (anchor.AnchorId == anchorId && ax.GridUid == grid && !Deleted(uid))
                yield return uid;
        }
    }

    private bool IsSigned(EntityUid uid)
    {
        return TryComp<PaperComponent>(uid, out var paper) && paper.StampedBy.Count > 0;
    }

    private void ProcessStuck(EntityUid player)
    {
        if (!TryComp<TutorialSessionComponent>(player, out var session))
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
            return explicitSkip;

        return NeedsLongSkip(step.Completion) ? 300f : 180f;
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
