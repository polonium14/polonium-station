using System.Linq;
using Content.Client._Polonium.Tutorial.Lobby.UI;
using Content.Client._Polonium.Tutorial.UI;
using Content.Client.Audio;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Tutorial;

/// <summary>
/// Builds the instruction the trainee actually reads - the bubble, its buttons and the hint
/// under it - and takes it down again without leaving anything on the screen.
/// </summary>
public sealed partial class TutorialPresentationSystem
{
    [Dependency] private ContentAudioSystem _audio = default!;
    private const float SpotlightMargin = 6f;

    private void ShowInstructionBubble(
        ProtoId<TutorialStepPrototype> stepId,
        TutorialStepPrototype stepProto)
    {
        // skip-later leftover has a different id so RequestClose(OverlayId) never saw it
        _tutorialUi.DiscardActive();

        var spotlight = TryGetHudControl(stepProto.HighlightHud);
        var wantsBubble = stepProto.BubbleText != null
                          || stepProto.Blocking
                          || HasAcknowledge(stepProto.Completion)
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

            if (HasAcknowledge(stepProto.Completion))
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

    // the button can sit next to a real condition, e.g. "pick a body part, or just press next"
    private static bool HasAcknowledge(TutorialCondition? condition)
    {
        return condition switch
        {
            ManualAcknowledgeCondition => true,
            AnyCondition any => any.Conditions.Any(HasAcknowledge),
            _ => false,
        };
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
        var details = string.Empty;
        var blocking = false;

        if (session.CurrentStep is { } stepId && _proto.TryIndex(stepId, out var stepProto))
        {
            blocking = stepProto.Blocking;
            objective = FormatTutorialLoc(stepProto.Instruction);

            // overlay steps already put this text in their bubble, the rest only have this bar
            if (stepProto.BubbleText is { } bubbleText && !WantsInstructionOverlay(stepProto))
                details = FormatTutorialLoc(bubbleText);
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

        EnsureHint().SetHint(objective, keys, details);
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
        if (_tutorialUi.ActiveOverlay is null)
            return;

        if (_tutorialUi.ActiveOverlay.Id == OverlayId)
        {
            _tutorialUi.DiscardActive();
            return;
        }

        DropForeignOverlay();
    }
}
