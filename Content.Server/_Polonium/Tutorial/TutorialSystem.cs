using Content.Server.Construction;
using Content.Server.Database;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Construction;
using Content.Shared.Damage.Systems;
using Content.Shared.Electrocution;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Tools.Systems;
using Content.Shared.Wall;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// Owns a run from start to finish: which flow a trainee is on, when it begins and what
/// happens when it ends or the player walks away from it.
/// </summary>
public sealed partial class TutorialSystem : SharedTutorialSystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TutorialActionExecutor _actions = default!;
    [Dependency] private TutorialMentorSystem _mentor = default!;
    [Dependency] private TutorialNpcSystem _npcs = default!;
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    private static readonly TimeSpan ShockQuipCooldown = TimeSpan.FromSeconds(20);

    // joingame / ready still dump you on the shared map, so we keep those cmds out while the comic is up
    private readonly HashSet<NetUserId> _lobbyTour = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialStartRequestedEvent>(OnStartRequested);
        SubscribeLocalEvent<TutorialMapCreatedEvent>(OnMapCreated);
        SubscribeLocalEvent<TutorialSessionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TutorialSessionComponent, BeforeDamageChangedEvent>(OnPlayerDamage);
        SubscribeLocalEvent<TutorialSessionComponent, MobStateChangedEvent>(OnPlayerMobState);
        SubscribeLocalEvent<TutorialSessionComponent, ElectrocutedEvent>(OnTraineeShocked);
        SubscribeLocalEvent<TutorialSessionComponent, ConstructionStartAttemptEvent>(OnItemConstruction);
        SubscribeLocalEvent<TutorialSessionComponent, SatiationUpdateEvent>(OnTraineeSatiation);
        SubscribeLocalEvent<TutorialNoDeconstructComponent, ConstructionInteractAttemptEvent>(OnLockedConstruction);
        Type[] usingBefore = [typeof(CableSystem), typeof(ConstructionSystem)];
        SubscribeLocalEvent<TutorialNoDeconstructComponent, InteractUsingEvent>(OnLockedCableCut,
            before: usingBefore);
        SubscribeLocalEvent<GhostRoleComponent, ComponentInit>(OnGhostRoleInit);
        SubscribeLocalEvent<WallComponent, InteractUsingEvent>(OnWallUsing,
            before: usingBefore);
        SubscribeLocalEvent<WallMountComponent, InteractUsingEvent>(OnWindowUsing,
            before: usingBefore);
        SubscribeLocalEvent<TutorialSealedComponent, InteractUsingEvent>(OnSealedUsing,
            before: usingBefore);
        SubscribeNetworkEvent<TutorialRestartRequestedEvent>(OnRestartRequested);
        SubscribeNetworkEvent<TutorialStartPracticalEvent>(OnStartPractical);
        SubscribeNetworkEvent<TutorialReturnToLobbyEvent>(OnReturnToLobby);
        SubscribeNetworkEvent<TutorialLobbyFlowEvent>(OnLobbyFlow);
        SubscribeNetworkEvent<TutorialFinaleChoiceEvent>(OnFinaleChoice);
        _player.PlayerStatusChanged += OnPlayerStatus;
    }

    public override void Shutdown()
    {
        _player.PlayerStatusChanged -= OnPlayerStatus;
        base.Shutdown();
    }

    private void StartFlow(EntityUid player, ProtoId<TutorialFlowPrototype> flowId, bool fromBeginning)
    {
        if (ReadIntroMode() == IntroNone)
            return;

        if (!_player.TryGetSessionByEntity(player, out _))
            return;

        if (HasComp<TutorialNpcComponent>(player))
            return;

        if (!_proto.TryIndex(flowId, out var flow))
        {
            Log.Error($"Tutorial: flow prototype '{flowId}' not found");
            return;
        }

        if (flow.Steps.Count == 0)
        {
            Log.Error($"Tutorial: flow '{flowId}' has no steps");
            return;
        }

        // already in a flow? nuke it and start over (respawn case)
        if (HasComp<TutorialSessionComponent>(player))
            RemComp<TutorialSessionComponent>(player);

        var session = AddComp<TutorialSessionComponent>(player);
        session.Flow = flowId;
        session.FlowStartedAt = DateTime.UtcNow;
        session.Anchors = ResolveAnchorsOnGrid(player);

        EnsureComp<PacifiedComponent>(player);
        KeepTraineeComfortable(player);

        _actions.BoltAllAirlocks(player);
        _actions.PowerAllDevices(player);

        var start = 0;
        string? startRoom = null;
        if (!fromBeginning)
            start = ResolveDebugStartStep(flow, out startRoom);

        if (start > 0)
        {
            FastForwardBefore((player, session), flow, start);
            if (startRoom is not null)
                _actions.Teleport(player, startRoom);
        }

        EnterStep((player, session), start);
    }

    private void OnShutdown(Entity<TutorialSessionComponent> ent, ref ComponentShutdown args)
    {
        _mentor.Cleanup(ent.Comp);

        // both of these are only ever maintained while a session exists. leaving them behind on
        // the last step means a player who cannot move and cannot swing, forever
        RemComp<TutorialFrozenComponent>(ent.Owner);
        RemComp<PacifiedComponent>(ent.Owner);
    }

    private void CompleteFlow(Entity<TutorialSessionComponent> ent, bool redial = false)
    {
        Log.Debug($"Tutorial: {ToPrettyString(ent.Owner)} completed flow '{ent.Comp.Flow}'");

        if (redial)
            TryRedial(ent.Owner);

        ent.Comp.CurrentStep = null;
        ent.Comp.NavigationAnchor = null;
        Dirty(ent);

        RemComp<TutorialSessionComponent>(ent.Owner);
    }

    private void TryRedial(EntityUid player)
    {
        var address = _cfg.GetCVar(CCVars.TutorialReturnServerConnectionString);
        if (string.IsNullOrWhiteSpace(address))
            return;

        if (!_player.TryGetSessionByEntity(player, out var session))
            return;

        RaiseNetworkEvent(
            new TutorialRedialEvent(address, Loc.GetString("tutorial-redial-message")),
            session);
    }

    private bool TryGetFlow(TutorialSessionComponent session, out TutorialFlowPrototype flow)
    {
        return _proto.TryIndex(session.Flow, out flow!);
    }

    public bool TryGetCurrentStep(
        TutorialSessionComponent session,
        out ProtoId<TutorialStepPrototype> stepId,
        out TutorialStepPrototype proto)
    {
        stepId = default;
        proto = default!;

        if (session.CurrentStep is not { } id)
            return false;

        if (!_proto.TryIndex(id, out var result))
            return false;

        stepId = id;
        proto = result;
        return true;
    }
}
