using System.Diagnostics.CodeAnalysis;
using Content.Shared._Polonium.Tutorial.Actions;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Construction.Components;
using Content.Shared.Prying.Components;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Tools.Systems;
using Content.Shared.Wall;
using Content.Shared.Wires;
using Content.Shared.PDA;
using Content.Shared.CCVar;
using Content.Shared.Ghost.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Containers;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Tutorial;

public abstract partial class SharedTutorialSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    private static readonly ProtoId<TagPrototype> StructureTag = "Structure";
    private static readonly ProtoId<TagPrototype> WindowTag = "Window";
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<TagPrototype> SyringeTag = "Syringe";

    private static readonly TimeSpan ProtectPopupCooldown = TimeSpan.FromSeconds(2.5);

    // the only things the kitchen wants cut: dough into slices, the cheese wheel into slices
    private static readonly HashSet<EntProtoId> SliceableIngredients = new() { "FoodDough", "FoodCheese" };

    private TimeSpan _nextProtectPopup;
    private EntityUid _lastProtectTarget;

    // None turns the whole tutorial off
    public const string IntroNone = "None";
    public const string IntroMain = "Main";
    public const string IntroTutorial = "Tutorial";
    public const string DefaultIntroMode = IntroNone;

    public string ReadIntroMode() => ReadIntroMode(_cfg, Log);

    public static string ReadIntroMode(IConfigurationManager cfg, ISawmill log)
    {
        var raw = (cfg.GetCVar(CCVars.TutorialMode) ?? string.Empty).Trim();
        if (string.Equals(raw, IntroNone, StringComparison.OrdinalIgnoreCase))
            return IntroNone;
        if (string.Equals(raw, IntroMain, StringComparison.OrdinalIgnoreCase))
            return IntroMain;
        if (string.Equals(raw, IntroTutorial, StringComparison.OrdinalIgnoreCase))
            return IntroTutorial;

        log.Error($"tutorial.mode is '{raw}', expected None/Main/Tutorial - using {DefaultIntroMode}");
        return DefaultIntroMode;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialSessionComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<TutorialSessionComponent, ThrowAttemptEvent>(OnTraineeThrow);
        SubscribeLocalEvent<TutorialSessionComponent, GhostAttemptEvent>(OnGhostAttempt);
        SubscribeLocalEvent<DamageableComponent, BeforeDamageChangedEvent>(OnStructureDamage);
        SubscribeLocalEvent<TutorialFrozenComponent, UpdateCanMoveEvent>(OnFrozenCanMove);
        SubscribeLocalEvent<TutorialFrozenComponent, ComponentStartup>(OnFrozenChanged);
        SubscribeLocalEvent<TutorialFrozenComponent, ComponentShutdown>(OnFrozenChanged);
        SubscribeLocalEvent<TutorialSealedComponent, AttemptChangePanelEvent>(OnSealedPanel);
        SubscribeLocalEvent<TutorialSealedComponent, BeforePryEvent>(OnSealedPry);
        SubscribeLocalEvent<TutorialSealedComponent, WeldableAttemptEvent>(OnSealedWeld);
        SubscribeLocalEvent<TutorialNoDeconstructComponent, UnanchorAttemptEvent>(OnLockedUnanchor);
        SubscribeLocalEvent<PdaComponent, ItemSlotEjectAttemptEvent>(OnPdaIdEject);
        SubscribeLocalEvent<TutorialSessionComponent, IngestionAttemptEvent>(OnTraineeIngest);
        SubscribeLocalEvent<EdibleComponent, AttemptToolRefineEvent>(OnIngredientRefine);

        InitializeMedicine();
    }

    private void OnFrozenCanMove(Entity<TutorialFrozenComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.Cancel();
    }

    private void OnFrozenChanged<T>(Entity<TutorialFrozenComponent> ent, ref T args)
    {
        _blocker.UpdateCanMove(ent.Owner);
    }

    private void OnSealedPanel(Entity<TutorialSealedComponent> ent, ref AttemptChangePanelEvent args)
    {
        args.Cancelled = true;
    }

    private void OnSealedPry(Entity<TutorialSealedComponent> ent, ref BeforePryEvent args)
    {
        args.Cancelled = true;
    }

    private void OnSealedWeld(Entity<TutorialSealedComponent> ent, ref WeldableAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnLockedUnanchor(Entity<TutorialNoDeconstructComponent> ent, ref UnanchorAttemptEvent args)
    {
        args.Cancel();
        args.FailMessage = Loc.GetString("tutorial-cannot-break-structure");
    }

    private void OnPdaIdEject(Entity<PdaComponent> pda, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.Slot.ID != PdaComponent.PdaIdSlotId)
            return;

        if (args.User is not { } user || !HasComp<TutorialSessionComponent>(user))
            return;

        args.Cancelled = true;
    }

    private void OnTraineeThrow(EntityUid uid, TutorialSessionComponent session, ThrowAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_tags.HasTag(args.ItemUid, SyringeTag))
            return;

        args.Cancel();
    }

    private void OnGhostAttempt(Entity<TutorialSessionComponent> ent, ref GhostAttemptEvent args)
    {
        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("tutorial-cannot-ghost"), ent, ent);
    }

    private void OnAttackAttempt(EntityUid uid, TutorialSessionComponent session, AttackAttemptEvent args)
    {
        if (args.Cancelled || args.Disarm || args.Target is not { } target)
            return;

        if (HasComp<MobStateComponent>(target) || !HasComp<DamageableComponent>(target))
            return;

        if (IsAttackableTarget(session, target))
            return;

        PopupProtect(uid, target);
        args.Cancel();
    }

    // a trainee breaks nothing. walls and windows were covered before, tables, machines and loose
    // items were not, and a knife swung at the cow took out the table and the microwave window
    private void OnStructureDamage(Entity<DamageableComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled || args.Origin is not { } origin)
            return;

        if (!args.Damage.AnyPositive())
            return;

        if (!TryComp<TutorialSessionComponent>(origin, out var session))
            return;

        // people and animals are fair game, the steps decide who should be hit
        if (HasComp<MobStateComponent>(ent) || IsAttackableTarget(session, ent.Owner))
            return;

        PopupProtect(origin, ent.Owner);
        args.Cancelled = true;
    }

    private void OnTraineeIngest(Entity<TutorialSessionComponent> ent, ref IngestionAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryGetStep(ent.Comp, out var step) && step.AllowIngestion)
            return;

        args.Cancelled = true;
        PopupProtectMessage(ent.Owner, ent.Owner, "tutorial-cannot-eat");
    }

    private void OnIngredientRefine(Entity<EdibleComponent> ent, ref AttemptToolRefineEvent args)
    {
        if (args.IsCancelled)
            return;

        // the knife is in the trainee's hand, so the container holding it says who is cutting
        if (!_container.TryGetContainingContainer((args.Using, null, null), out var holder)
            || !HasComp<TutorialSessionComponent>(holder.Owner))
            return;

        if (MetaData(ent).EntityPrototype?.ID is { } id && SliceableIngredients.Contains(id))
            return;

        args = args with { IsCancelled = true, BlockCause = Loc.GetString("tutorial-cannot-slice") };
    }

    protected bool TryBlockStructureAttack(EntityUid user, TutorialSessionComponent session, EntityUid target)
    {
        if (!IsProtectedStructure(target))
            return false;

        if (IsAttackableTarget(session, target))
            return false;

        PopupProtect(user, target);
        return true;
    }

    protected bool IsHullStructure(EntityUid uid)
    {
        return HasComp<WallComponent>(uid) || _tags.HasTag(uid, WindowTag);
    }

    protected void PopupProtect(EntityUid user, EntityUid target)
    {
        PopupProtectMessage(user, target, "tutorial-cannot-break-structure");
    }

    private void PopupProtectMessage(EntityUid user, EntityUid target, LocId message)
    {
        if (target == _lastProtectTarget && _timing.CurTime < _nextProtectPopup)
            return;

        _lastProtectTarget = target;
        _nextProtectPopup = _timing.CurTime + ProtectPopupCooldown;
        _popup.PopupEntity(Loc.GetString(message), target, user);
    }

    private bool IsProtectedStructure(EntityUid uid)
    {
        return _tags.HasTag(uid, StructureTag)
               || _tags.HasTag(uid, WindowTag)
               || _tags.HasTag(uid, WallTag)
               || HasComp<WallComponent>(uid);
    }

    private bool IsAttackableTarget(TutorialSessionComponent session, EntityUid target)
    {
        if (!TryComp<TutorialAnchorComponent>(target, out var anchor))
            return false;

        return TryGetStep(session, out var step) && CompletionAllowsAttack(step, anchor.AnchorId);
    }

    protected bool TryGetStep(TutorialSessionComponent session, [NotNullWhen(true)] out TutorialStepPrototype? step)
    {
        step = null;
        return session.CurrentStep is { } id && _proto.TryIndex(id, out step);
    }

    private static bool CompletionAllowsAttack(TutorialStepPrototype step, string anchorId)
    {
        if (step.AttackableAnchors.Contains(anchorId))
            return true;

        foreach (var action in step.OnEnter)
        {
            // meteor owns this pane, fists would skip the drill
            if (action is MeteorWindowAction meteor && meteor.WindowAnchor == anchorId)
                return false;
        }

        return Walk(step.Completion);

        bool Walk(TutorialCondition? condition)
        {
            return condition switch
            {
                AllCondition all => all.Conditions.Exists(Walk),
                AnyCondition any => any.Conditions.Exists(Walk),
                AnchorDamagedCondition damaged => damaged.AnchorId == anchorId,
                MeleeHitAnchorCondition melee => melee.AnchorId == anchorId,
                ShootTargetsCondition shoot => shoot.AnchorId == anchorId,
                _ => false,
            };
        }
    }
}

/// <summary>Fires after the player spawns on a solitary map. TutorialSystem picks it up.</summary>
public sealed class TutorialStartRequestedEvent : EntityEventArgs
{
    public EntityUid Player { get; }
    public ProtoId<Prototypes.TutorialFlowPrototype> Flow { get; }

    public bool FromBeginning { get; }

    public TutorialStartRequestedEvent(
        EntityUid player,
        ProtoId<Prototypes.TutorialFlowPrototype> flow,
        bool fromBeginning = false)
    {
        Player = player;
        Flow = flow;
        FromBeginning = fromBeginning;
    }
}

/// <summary>Fires after a solitary tutorial map is loaded and initialized.</summary>
public sealed class TutorialMapCreatedEvent : EntityEventArgs
{
    public EntityUid MapUid { get; }

    public TutorialMapCreatedEvent(EntityUid mapUid)
    {
        MapUid = mapUid;
    }
}
