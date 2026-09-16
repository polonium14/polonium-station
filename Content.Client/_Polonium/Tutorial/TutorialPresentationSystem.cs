using Content.Client._Polonium.Tutorial.Lobby;
using Content.Client._Polonium.Tutorial.Lobby.UI;
using Content.Client._Polonium.Tutorial.UI;
using Content.Client._Shitmed.UserInterface.Systems.Targeting.Widgets;
using Content.Client.Audio;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Client.UserInterface.Systems.Alerts.Widgets;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Client.UserInterface.Systems.Inventory.Widgets;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Content.Shared.Input;
using Content.Shared.Movement.Components;
using Robust.Client;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameStates;
using Robust.Shared.Input;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Tutorial;

public sealed partial class TutorialPresentationSystem : SharedTutorialSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _uiMan = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IGameController _game = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private ContentAudioSystem _audio = default!;
    [Dependency] private TutorialManager _lobbyTutorial = default!;

    // lobby Reset() used to eat this if the ids matched. keep it distinct.
    public const string OverlayId = "tutorial-ingame";

    // keys stand out from the surrounding sentence in both the hint bar and the bubble
    private const string KeyColor = "#FFC83D";

    // same blue as the hint panel border, so the spotlight reads as part of the same ui
    private static readonly Color SpotlightColor = Color.FromHex("#65B8E2");
    private static readonly Color SpotlightDim = Color.Black.WithAlpha(0.62f);
    private const float SpotlightMargin = 6f;

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
        RaiseNetworkEvent(new TutorialRestartRequestedEvent());
    }

    public void RequestPracticalJoin()
    {
        RaiseNetworkEvent(new TutorialStartPracticalEvent());
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
            TryShowLocal(force: true);
    }

    private void OnSessionShutdown(Entity<TutorialSessionComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner != _player.LocalEntity)
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
        if (_player.LocalEntity is not { } uid || !TryComp<TutorialSessionComponent>(uid, out var session))
            return;

        if (force)
            _lastUi = default;

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

    private static bool WantsInstructionOverlay(TutorialStepPrototype step)
    {
        return step.Blocking
               || step.Completion is ManualAcknowledgeCondition
               || step.HighlightHud != TutorialHudTarget.None
               || step.Guidebook is not null;
    }

    private bool OverlayGateOpen(EntityUid player, TutorialStepPrototype step)
    {
        if (string.IsNullOrEmpty(step.SpeakAtAnchor))
            return true;

        if (!TryComp(player, out TransformComponent? xform) || xform.GridUid is not { } grid)
            return true;

        var playerPos = _xform.GetWorldPosition(xform);
        var rangeSq = step.SpeakAtRange * step.SpeakAtRange;
        var query = EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out _, out var anchor, out var ax))
        {
            if (anchor.AnchorId != step.SpeakAtAnchor || ax.GridUid != grid)
                continue;

            if ((_xform.GetWorldPosition(ax) - playerPos).LengthSquared() <= rangeSq)
                return true;
        }

        return false;
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

    /// <summary>
    /// Switching hud layout builds a whole new screen, so the control we spotlighted is gone and
    /// the bubble hangs off nothing. Notice that and draw the step again against the new screen.
    /// </summary>
    private void WatchScreenSwap()
    {
        if (_bubbleScreen == null)
            return;

        var stale = !ReferenceEquals(_bubbleScreen, _uiMan.ActiveScreen)
                    || _tutorialUi.ActiveOverlay is { Id: OverlayId, IsHighlightStale: true };

        if (!stale)
            return;

        _bubbleScreen = null;
        TryShowLocal(force: true);
    }

    /// <summary>
    /// Speak-at steps only light the hud once the trainee is actually at the pad. Session
    /// state does not change on that walk, so the gate has to be watched every frame.
    /// </summary>
    private void WatchHudOverlayGate()
    {
        if (_player.LocalEntity is not { } player || !TryComp<TutorialSessionComponent>(player, out var session))
            return;

        if (session.CurrentStep is not { } stepId || !_proto.TryIndex(stepId, out var step))
            return;

        if (!WantsInstructionOverlay(step))
            return;

        var open = OverlayGateOpen(player, step);
        var shown = _tutorialUi.ActiveOverlay is { Id: OverlayId };

        if (open == shown)
            return;

        if (open)
        {
            _lastUi = default;
            TryShow((player, session));
            return;
        }

        ClearBubble();
    }

    private void ShowInstructionBubble(
        ProtoId<TutorialStepPrototype> stepId,
        TutorialStepPrototype stepProto)
    {
        if (_tutorialUi.ActiveOverlay is { Id: OverlayId })
            _tutorialUi.RequestClose(false);

        var spotlight = TryGetHudControl(stepProto.HighlightHud);
        var wantsBubble = stepProto.BubbleText != null
                          || stepProto.Blocking
                          || stepProto.Completion is ManualAcknowledgeCondition
                          || stepProto.Guidebook is not null;

        if (spotlight == null && !wantsBubble)
            return;

        _bubbleScreen = _uiMan.ActiveScreen;

        if (spotlight != null)
        {
            // cuts a hole in the dim exactly around the widget and frames it.
            // a walk-to-pad step has no bubble of its own, so leave the world undimmed
            _tutorialUi.PlanOverlay(
                OverlayId,
                spotlight,
                SpotlightColor,
                highlightMargin: SpotlightMargin,
                orphanOnHighlightClick: false,
                rootControl: _uiMan.RootControl,
                backgroundColor: wantsBubble ? SpotlightDim : Color.Transparent,
                isSelfClosingOnClick: false,
                // pass clicks through, the player should be able to poke the thing being explained
                ignoreBackgroundClicks: false);
        }
        else
        {
            _tutorialUi.PlanOverlay(
                OverlayId,
                rootControl: _uiMan.RootControl,
                backgroundColor: stepProto.Blocking ? Color.Black.WithAlpha(0.75f) : Color.Transparent,
                isSelfClosingOnClick: false,
                ignoreBackgroundClicks: stepProto.Blocking);
        }

        if (!wantsBubble)
            return;

        var bubble = new TutorialBubble(FormatTutorialLoc(stepProto.BubbleText ?? stepProto.Instruction))
        {
            ClickAction = TutorialBubble.ClickBehaviour.Ignore,
            TippyVariant = TutorialBubble.Tippy.None,
        };

        if (stepProto.Finale)
        {
            AddFinaleButtons(bubble);
            if (!_finaleMusic)
            {
                _finaleMusic = true;
                _audio.StartLobbyMusicFromCollection();
            }
            _lobbyTutorial.MarkCompleted();
        }
        else
        {
            bubble.ApplyFunctionalStyle();

            if (stepProto.Guidebook is { } guideId)
                AddGuidebookButton(bubble, guideId);

            if (stepProto.Completion is ManualAcknowledgeCondition)
                AddAcknowledgeButton(bubble, stepId, stepProto.Blocking);
        }

        _tutorialUi.PlanBubble(
            bubble,
            stepProto.Blocking
                ? TutorialHighlightOverlay.OverlayControlPosition.Center
                : BubbleSideFor(stepProto.HighlightHud),
            overlayId: OverlayId,
            spacing: 40f);
    }

    /// <summary>Keep the bubble on the opposite side of whatever is being pointed at.</summary>
    private static TutorialHighlightOverlay.OverlayControlPosition BubbleSideFor(TutorialHudTarget target)
    {
        return target switch
        {
            TutorialHudTarget.Actions => TutorialHighlightOverlay.OverlayControlPosition.CenterRight,
            TutorialHudTarget.TopBar => TutorialHighlightOverlay.OverlayControlPosition.CenterRight,
            TutorialHudTarget.Guidebook => TutorialHighlightOverlay.OverlayControlPosition.CenterRight,
            TutorialHudTarget.ActionsAndMenu => TutorialHighlightOverlay.OverlayControlPosition.CenterRight,
            TutorialHudTarget.Chat => TutorialHighlightOverlay.OverlayControlPosition.CenterLeft,
            _ => TutorialHighlightOverlay.OverlayControlPosition.CenterLeft,
        };
    }

    private Control? TryGetHudControl(TutorialHudTarget target)
    {
        if (target == TutorialHudTarget.None || _uiMan.ActiveScreen is not { } screen)
            return null;

        if (target == TutorialHudTarget.Chat && screen is InGameScreen inGame)
            return inGame.ChatBox;

        if (target == TutorialHudTarget.ActionsAndMenu)
            return TryGetActionsAndMenu(screen);

        if (target == TutorialHudTarget.Guidebook)
            return FindDescendant(screen, typeof(GameTopMenuBar)) is GameTopMenuBar bar ? bar.GuidebookButton : null;

        var wanted = target switch
        {
            TutorialHudTarget.Hands => typeof(HotbarGui),
            TutorialHudTarget.Alerts => typeof(AlertsUI),
            TutorialHudTarget.Inventory => typeof(InventoryGui),
            TutorialHudTarget.Actions => typeof(ActionsBar),
            TutorialHudTarget.TopBar => typeof(GameTopMenuBar),
            TutorialHudTarget.Targeting => typeof(TargetingControl),
            _ => null,
        };

        if (wanted == null)
            return null;

        var found = FindDescendant(screen, wanted);
        return found is { VisibleInTree: true, Parent: not null } ? found : null;
    }

    /// <summary>
    /// The action slots and the menu buttons above them live in one container on the default
    /// screen, but not necessarily on every layout - so find whatever actually holds both.
    /// </summary>
    private static Control? TryGetActionsAndMenu(Control screen)
    {
        var actions = FindDescendant(screen, typeof(ActionsBar));
        var menu = FindDescendant(screen, typeof(GameTopMenuBar));

        if (actions == null)
            return menu;

        if (menu == null)
            return actions;

        var shared = FindCommonAncestor(actions, menu);
        if (shared == null)
            return actions;

        // on an exotic layout the common parent can be the whole screen, which would
        // spotlight everything and dim nothing. fall back to the slots in that case
        var area = shared.PixelSize.X * shared.PixelSize.Y;
        var screenArea = screen.PixelSize.X * screen.PixelSize.Y;
        if (screenArea > 0 && area > screenArea * 0.5f)
            return actions;

        return shared;
    }

    private static Control? FindCommonAncestor(Control a, Control b)
    {
        var chain = new HashSet<Control>();
        for (var cur = a; cur != null; cur = cur.Parent)
            chain.Add(cur);

        for (var cur = b; cur != null; cur = cur.Parent)
        {
            if (chain.Contains(cur))
                return cur;
        }

        return null;
    }

    // widget fields on the screen are protected, so walk the tree instead of reaching for them
    private static Control? FindDescendant(Control root, Type type)
    {
        if (type.IsInstanceOfType(root))
            return root;

        foreach (var child in root.Children)
        {
            if (FindDescendant(child, type) is { } hit)
                return hit;
        }

        return null;
    }

    private void AddGuidebookButton(TutorialBubble bubble, ProtoId<Content.Shared.Guidebook.GuideEntryPrototype> guideId)
    {
        var button = new Button
        {
            Text = _loc.GetString("tutorial-bubble-guidebook"),
            HorizontalAlignment = Control.HAlignment.Center,
        };

        button.OnPressed += _ => _guidebook.OpenGuidebook(selected: guideId);
        bubble.ButtonsContainer.AddChild(button);
    }

    private void AddAcknowledgeButton(TutorialBubble bubble, ProtoId<TutorialStepPrototype> stepId, bool blocking)
    {
        var button = new Button
        {
            Text = _loc.GetString(blocking ? "tutorial-bubble-exit" : "tutorial-bubble-acknowledge"),
            HorizontalAlignment = Control.HAlignment.Center,
        };

        button.OnPressed += _ =>
        {
            RaiseNetworkEvent(new TutorialAcknowledgeStepEvent(stepId.Id));
            button.Disabled = true;
        };

        bubble.ButtonsContainer.AddChild(button);
    }

    private void AddFinaleButtons(TutorialBubble bubble)
    {
        var join = TutorialBubble.MakeButton(_loc.GetString("tutorial-bubble-finale-join"));
        var quit = TutorialBubble.MakeButton(_loc.GetString("tutorial-bubble-finale-quit"), primary: false);

        var column = new BoxContainer
        {
            Align = BoxContainer.AlignMode.Center,
            Orientation = BoxContainer.LayoutOrientation.Vertical,
        };
        column.AddChild(join);
        column.AddChild(quit);
        bubble.ButtonsContainer.AddChild(column);

        join.OnPressed += _ =>
        {
            join.Disabled = true;
            quit.Disabled = true;
            RaiseNetworkEvent(new TutorialFinaleChoiceEvent(true));
        };

        quit.OnPressed += _ =>
        {
            join.Disabled = true;
            quit.Disabled = true;
            RaiseNetworkEvent(new TutorialFinaleChoiceEvent(false));
            _game.Shutdown();
        };
    }

    private void UpdateHint(TutorialSessionComponent session)
    {
        var objective = string.Empty;
        var blocking = false;

        if (session.CurrentStep is { } stepId && _proto.TryIndex(stepId, out var stepProto))
        {
            blocking = stepProto.Blocking;
            objective = FormatTutorialLoc(stepProto.Instruction);
        }

        var keys = session.KeybindHint is { } id
            ? FormatTutorialLoc(id)
            : string.Empty;

        // the blocking bubble already carries the same text, no point printing it twice
        if (blocking)
        {
            EnsureHint().SetHint(string.Empty, string.Empty);
            return;
        }

        EnsureHint().SetHint(objective, keys);
    }

    private TutorialControlHint EnsureHint()
    {
        if (_hint != null)
            return _hint;

        _hint = new TutorialControlHint();
        _uiMan.PopupRoot.AddChild(_hint);
        LayoutContainer.SetAnchorPreset(_hint, LayoutContainer.LayoutPreset.Wide);
        return _hint;
    }

    private void ClearHint()
    {
        _hint?.SetHint(string.Empty, string.Empty);
    }

    private void ClearBubble()
    {
        if (_tutorialUi.ActiveOverlay is { Id: OverlayId })
            _tutorialUi.RequestClose(false);
    }

    private void OnKeybindChanged(IKeyBinding _)
    {
        _keyArgs = null;

        if (_player.LocalEntity is not { } uid || !TryComp<TutorialSessionComponent>(uid, out var session))
            return;

        UpdateHint(session);
    }

    /// <summary>
    /// Every tutorial string gets the whole keybind set. Fluent ignores the args it does not use,
    /// so loc files can drop { $useHand } and friends anywhere without touching this file.
    /// </summary>
    private string FormatTutorialLoc(LocId id)
    {
        return _loc.GetString(id, _keyArgs ??= BuildKeyArgs());
    }

    private (string, object)[] BuildKeyArgs()
    {
        return new (string, object)[]
        {
            ("keys", Paint(FormatMoveKeys())),
            ("move", Paint(FormatMoveKeys())),
            ("walk", PaintKey(EngineKeyFunctions.Walk)),
            ("crawl", PaintKey(ContentKeyFunctions.ToggleKnockdown)),
            ("swap", PaintKey(ContentKeyFunctions.SwapHands)),
            ("drop", PaintKey(ContentKeyFunctions.Drop)),
            ("throwItem", PaintKey(ContentKeyFunctions.ThrowItemInHand)),
            ("useHand", PaintKey(ContentKeyFunctions.UseItemInHand)),
            ("altUseHand", PaintKey(ContentKeyFunctions.AltUseItemInHand)),
            ("useWorld", PaintKey(ContentKeyFunctions.ActivateItemInWorld)),
            ("altUseWorld", PaintKey(ContentKeyFunctions.AltActivateItemInWorld)),
            ("examine", PaintKey(ContentKeyFunctions.ExamineEntity)),
            ("inventory", PaintKey(ContentKeyFunctions.OpenInventoryMenu)),
            ("character", PaintKey(ContentKeyFunctions.OpenCharacterMenu)),
            ("backpack", PaintKey(ContentKeyFunctions.OpenBackpack)),
            ("smartBackpack", PaintKey(ContentKeyFunctions.SmartEquipBackpack)),
            ("smartBelt", PaintKey(ContentKeyFunctions.SmartEquipBelt)),
            ("belt", PaintKey(ContentKeyFunctions.OpenBelt)),
            ("actions", PaintKey(ContentKeyFunctions.OpenActionsMenu)),
            ("craft", PaintKey(ContentKeyFunctions.OpenCraftingMenu)),
            // turns a construction preview before it is placed
            ("rotate", PaintKey(EngineKeyFunctions.EditorRotateObject)),
            ("pull", PaintKey(ContentKeyFunctions.TryPullObject)),
            ("releasePull", PaintKey(ContentKeyFunctions.ReleasePulledObject)),
            ("movePulled", PaintKey(ContentKeyFunctions.MovePulledObject)),
            ("guidebook", PaintKey(ContentKeyFunctions.OpenGuidebook)),
            ("point", PaintKey(ContentKeyFunctions.Point)),
            ("attack", PaintKey(EngineKeyFunctions.Use)),
            ("disarm", PaintKey(EngineKeyFunctions.UseSecondary)),
            ("click", PaintKey(EngineKeyFunctions.Use)),
            ("rightClick", PaintKey(EngineKeyFunctions.UseSecondary)),
            ("escape", PaintKey(EngineKeyFunctions.CloseModals)),
            ("chat", PaintKey(ContentKeyFunctions.FocusChat)),
            ("chatSay", PaintKey(ContentKeyFunctions.FocusLocalChat)),
            ("chatWhisper", PaintKey(ContentKeyFunctions.FocusWhisperChat)),
            ("chatRadio", PaintKey(ContentKeyFunctions.FocusRadio)),
            ("chatEmote", PaintKey(ContentKeyFunctions.FocusEmote)),
            ("chatOoc", PaintKey(ContentKeyFunctions.FocusOOC)),
            ("chatLooc", PaintKey(ContentKeyFunctions.FocusLOOC)),
            ("chatCycle", PaintKey(ContentKeyFunctions.CycleChatChannelForward)),
            // loc files spell these out in full, keep the names they already use
            ("switch-channel-key", PaintKey(ContentKeyFunctions.CycleChatChannelForward)),
            ("default-chat-channel", _loc.GetString("hud-chatbox-select-channel-Local")),
            ("verb-categories-eject", _loc.GetString("verb-categories-eject")),
            ("examine-verb", _loc.GetString("examine-verb-name")),
            ("climb-verb", _loc.GetString("comp-climbable-verb-climb")),
            ("guide-radio", _loc.GetString("guide-entry-radio")),
            ("channel-local", _loc.GetString("hud-chatbox-select-channel-Local")),
            ("channel-whisper", _loc.GetString("hud-chatbox-select-channel-Whisper")),
            ("channel-radio", _loc.GetString("hud-chatbox-select-channel-Radio")),
            ("channel-emote", _loc.GetString("hud-chatbox-select-channel-Emotes")),
            ("channel-ooc", _loc.GetString("hud-chatbox-select-channel-OOC")),
            ("channel-looc", _loc.GetString("hud-chatbox-select-channel-LOOC")),
            ("internals-toggle", _loc.GetString("ent-ActionToggleInternals")),
            ("gas-tank-toggle", _loc.GetString("gas-tank-window-internals-toggle-button")),
            ("tutorial-bubble-acknowledge", _loc.GetString("tutorial-bubble-acknowledge")),
            ("camera", PaintKey(EngineKeyFunctions.CameraRotateRight)),
            ("cameraLeft", PaintKey(EngineKeyFunctions.CameraRotateLeft)),
            ("cameraReset", PaintKey(EngineKeyFunctions.CameraReset)),
            // old loc files used bare {$key}, keep them rendering instead of printing the placeholder
            ("key", PaintKey(ContentKeyFunctions.UseItemInHand)),
            ("hand", PaintKey(ContentKeyFunctions.UseItemInHand)),
            ("world", PaintKey(ContentKeyFunctions.ActivateItemInWorld)),
        };
    }

    private string PaintKey(BoundKeyFunction function)
    {
        return Paint(_input.GetKeyFunctionButtonString(function));
    }

    private static string Paint(string key)
    {
        return $"[color={KeyColor}]{key}[/color]";
    }

    private string Key(BoundKeyFunction function)
    {
        return _input.GetKeyFunctionButtonString(function);
    }

    private string FormatMoveKeys()
    {
        var parts = new[]
        {
            Key(EngineKeyFunctions.MoveUp),
            Key(EngineKeyFunctions.MoveLeft),
            Key(EngineKeyFunctions.MoveDown),
            Key(EngineKeyFunctions.MoveRight),
        };

        if (Array.TrueForAll(parts, static p => p.Length == 1))
            return string.Concat(parts);

        return string.Join(" / ", parts);
    }
}
