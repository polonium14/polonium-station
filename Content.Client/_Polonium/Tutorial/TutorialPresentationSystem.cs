using Content.Client._Polonium.Tutorial.Lobby;
using Content.Client._Polonium.Tutorial.UI;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared.Movement.Components;
using Robust.Client;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Tutorial;

/// <summary>
/// The trainee's side of a run: it holds the step the server last sent and decides, every
/// frame, whether the screen in front of the player is ready to be written on.
/// </summary>
public sealed partial class TutorialPresentationSystem : SharedTutorialSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _uiMan = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IGameController _game = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private TutorialManager _lobbyTutorial = default!;

    // lobby Reset() used to eat this if the ids matched. keep it distinct.
    public const string OverlayId = "tutorial-ingame";

    // keys stand out from the surrounding sentence in both the hint bar and the bubble
    private const string KeyColor = "#FFC83D";

    // same blue as the hint panel border, so the spotlight reads as part of the same ui
    private static readonly Color SpotlightColor = Color.FromHex("#65B8E2");

    private static readonly Color SpotlightDim = Color.Black.WithAlpha(0.62f);

    private (string? Step, LocId? Hint) _lastUi;

    private (string, object)[]? _keyArgs;
    private UIScreen? _bubbleScreen;
    private TutorialUIController _tutorialUi = default!;
    private GuidebookUIController _guidebook = default!;
    private TutorialControlHint? _hint;
    private string? _cameraStep;
    private Angle? _cameraInitial;
    private float _cameraDegrees = 80f;
    private bool _cameraSent;
    private bool _finaleMusic;

    public override void Initialize()
    {
        base.Initialize();

        _tutorialUi = _uiMan.GetUIController<TutorialUIController>();
        _guidebook = _uiMan.GetUIController<GuidebookUIController>();

        SubscribeLocalEvent<TutorialSessionComponent, AfterAutoHandleStateEvent>(OnSessionState);
        SubscribeLocalEvent<TutorialSessionComponent, ComponentShutdown>(OnSessionShutdown);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalAttached);
        SubscribeNetworkEvent<TutorialRedialEvent>(OnRedial);
        SubscribeNetworkEvent<TutorialPlayerCompletionEvent>(OnCompletionStatus);
        _state.OnStateChanged += OnStateChanged;
        _input.OnKeyBindingAdded += OnKeybindChanged;
        _input.OnKeyBindingRemoved += OnKeybindChanged;
    }

    public override void Shutdown()
    {
        _state.OnStateChanged -= OnStateChanged;
        _input.OnKeyBindingAdded -= OnKeybindChanged;
        _input.OnKeyBindingRemoved -= OnKeybindChanged;
        _hint?.Orphan();
        _hint = null;
        base.Shutdown();
    }

    public void RequestRestart()
    {
        _lastUi = default;
        _finaleMusic = false;
        _cameraStep = null;
        _cameraInitial = null;
        _cameraSent = false;
        ClearBubble();
        ClearHint();
        RaiseNetworkEvent(new TutorialRestartRequestedEvent());
    }

    public void RequestPracticalJoin()
    {
        RaiseNetworkEvent(new TutorialStartPracticalEvent());
    }

    public void RequestReturnToLobby()
    {
        RaiseNetworkEvent(new TutorialReturnToLobbyEvent());
    }

    public void SetLobbyTourActive(bool active)
    {
        RaiseNetworkEvent(new TutorialLobbyFlowEvent(active));
    }

    private void OnRedial(TutorialRedialEvent ev)
    {
        try
        {
            _game.Redial(ev.Address, ev.Message);
        }
        catch (Exception e)
        {
            // without the launcher this just throws, that's fine
            Log.Warning($"Tutorial redial failed: {e.Message}");
        }
    }

    private void OnCompletionStatus(TutorialPlayerCompletionEvent ev)
    {
        _lobbyTutorial.OnDbCompletion(ev.Completed);
    }

    private void OnSessionState(Entity<TutorialSessionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Owner != _player.LocalEntity)
            return;

        TryShow(ent);
    }

    private void OnLocalAttached(LocalPlayerAttachedEvent ev)
    {
        TryShowLocal(force: true);
    }

    private void OnStateChanged(StateChangedEventArgs args)
    {
        if (args.NewState is GameplayState)
        {
            TryShowLocal(force: true);
            return;
        }

        ClearBubble();
        ClearHint();
        _bubbleScreen = null;
        _lastUi = default;
    }

    private void OnSessionShutdown(Entity<TutorialSessionComponent> ent, ref ComponentShutdown args)
    {
        // wipe/delete detaches first, LocalEntity is already null. still drop the hud
        // or a leftover lobby overlay sits there and eats the next welcome
        if (_player.LocalEntity is { } local && local != ent.Owner)
            return;

        ClearBubble();
        ClearHint();
        _bubbleScreen = null;
        _cameraStep = null;
        _cameraInitial = null;
        _cameraSent = false;
        _finaleMusic = false;
        _lastUi = default;
    }

    private void TryShowLocal(bool force)
    {
        if (force)
        {
            _lastUi = default;
            DropForeignOverlay();
        }

        if (_player.LocalEntity is not { } uid || !TryComp<TutorialSessionComponent>(uid, out var session))
            return;

        TryShow((uid, session));
    }

    private void TryShow(Entity<TutorialSessionComponent> ent)
    {
        // lobby CancelTutorial/Reset runs on the same state change and used to delete this overlay
        if (_state.CurrentState is not GameplayState)
            return;

        ApplyState(ent);
    }

    private void ApplyState(Entity<TutorialSessionComponent> ent)
    {
        UpdateHint(ent.Comp);
        WatchCamera(ent.Owner, ent.Comp);

        var ui = (ent.Comp.CurrentStep?.Id, ent.Comp.KeybindHint);
        if (ui == _lastUi)
            return;

        ClearBubble();
        _lastUi = ui;

        if (ent.Comp.CurrentStep is not { } stepId || !_proto.TryIndex(stepId, out var stepProto))
            return;

        // acknowledge steps need the button, so they get a bubble even when they do not block.
        // so does anything pointing at a hud widget or offering a guidebook page - without this
        // the guidebook button on the bubble was never drawn at all
        if (!WantsInstructionOverlay(stepProto))
            return;

        // last room: the step points at the guidebook, but the trainee is still walking to the pad.
        // dimming the world with "go to the holopad" on top of that is just in the way
        if (!OverlayGateOpen(ent.Owner, stepProto))
            return;

        ShowInstructionBubble(stepId, stepProto);
    }

    private void WatchCamera(EntityUid player, TutorialSessionComponent session)
    {
        var step = session.CurrentStep?.Id;
        if (step != _cameraStep)
        {
            _cameraStep = step;
            _cameraSent = false;
            _cameraDegrees = 80f;
            _cameraInitial = TryComp<InputMoverComponent>(player, out var mover)
                ? mover.TargetRelativeRotation
                : null;

            if (session.CurrentStep is { } id
                && _proto.TryIndex(id, out var proto)
                && proto.Completion is CameraRotatedCondition cam)
                _cameraDegrees = cam.Degrees;
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        WatchScreenSwap();
        WatchHudOverlayGate();

        if (_cameraSent || _cameraInitial is not { } initial)
            return;

        if (_player.LocalEntity is not { } player || !TryComp<InputMoverComponent>(player, out var mover))
            return;

        if (Math.Abs(Angle.ShortestDistance(initial, mover.TargetRelativeRotation).Degrees) < _cameraDegrees)
            return;

        _cameraSent = true;
        RaiseNetworkEvent(new TutorialCameraRotatedEvent());
    }
}
