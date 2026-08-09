// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Polonium.Tutorial.Lobby;
using Content.Client._Polonium.Tutorial.Lobby.UI;
using Content.Client._Polonium.Pathfinding;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Tutorial;

/// <summary>
/// Client-side - shows the instruction bubble and draws the path line
/// based on what the server put into <see cref="TutorialSessionComponent"/>.
/// </summary>
public sealed class TutorialPresentationSystem : SharedTutorialSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _uiMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly PlayerPathfindingSystem _pathfinding = default!;

    // keep this different from the lobby overlay id or they'll stomp each other
    private const string OverlayId = "tutorial-ingame";

    private ProtoId<TutorialStepPrototype>? _lastShownStep;
    private TutorialUIController _tutorialUi = default!;

    public override void Initialize()
    {
        base.Initialize();

        _tutorialUi = _uiMan.GetUIController<TutorialUIController>();

        SubscribeLocalEvent<TutorialSessionComponent, AfterAutoHandleStateEvent>(OnSessionState);
        SubscribeLocalEvent<TutorialSessionComponent, ComponentShutdown>(OnSessionShutdown);
    }

    private void OnSessionState(Entity<TutorialSessionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Owner != _player.LocalEntity)
            return;

        ApplyState(ent);
    }

    private void OnSessionShutdown(Entity<TutorialSessionComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner != _player.LocalEntity)
            return;

        ClearPathfinding();
        ClearBubble();
        _lastShownStep = null;
    }

    private void ApplyState(Entity<TutorialSessionComponent> ent)
    {
        UpdatePathfinding(ent.Owner, ent.Comp);

        if (ent.Comp.CurrentStep == _lastShownStep)
            return;

        ClearBubble();

        if (ent.Comp.CurrentStep is { } stepId && _proto.TryIndex(stepId, out var stepProto))
        {
            ShowInstructionBubble(stepId, stepProto);
            _lastShownStep = stepId;
        }
        else
        {
            _lastShownStep = null;
        }
    }

    private void UpdatePathfinding(EntityUid player, TutorialSessionComponent session)
    {
        if (session.NavigationAnchor is { } anchorId)
        {
            _pathfinding.SetDestinationAnchor(player, anchorId);
            return;
        }

        _pathfinding.SetDestinationAnchor(player, null);
    }

    private void ClearPathfinding()
    {
        if (_player.LocalEntity is not { } local)
            return;

        _pathfinding.SetDestinationAnchor(local, null);
    }

    private void ShowInstructionBubble(ProtoId<TutorialStepPrototype> stepId, TutorialStepPrototype stepProto)
    {
        if (_tutorialUi.ActiveOverlay is { Id: OverlayId })
            _tutorialUi.RequestClose(false);

        // transparent overlay, doesn't eat clicks
        _tutorialUi.PlanOverlay(
            OverlayId,
            rootControl: _uiMan.RootControl,
            backgroundColor: Color.Transparent,
            isSelfClosingOnClick: false,
            ignoreBackgroundClicks: false);

        var bubble = new TutorialBubble(_loc.GetString(stepProto.Instruction))
        {
            ClickAction = TutorialBubble.ClickBehaviour.Ignore,
            TippyVariant = TutorialBubble.Tippy.None,
        };

        // Manual ack step? Add a "Got it" button — the only way to advance.
        if (stepProto.Completion is ManualAcknowledgeCondition)
            AddAcknowledgeButton(bubble, stepId);

        _tutorialUi.PlanBubble(
            bubble,
            TutorialHighlightOverlay.OverlayControlPosition.BottomRight,
            overlayId: OverlayId,
            spacing: 40f);
    }

    private void AddAcknowledgeButton(TutorialBubble bubble, ProtoId<TutorialStepPrototype> stepId)
    {
        var button = new Button
        {
            Text = _loc.GetString("tutorial-bubble-acknowledge"),
            HorizontalAlignment = Control.HAlignment.Center,
        };

        button.OnPressed += _ =>
        {
            RaiseNetworkEvent(new TutorialAcknowledgeStepEvent(stepId.Id));
            button.Disabled = true;
        };

        bubble.ButtonsContainer.AddChild(button);
    }

    private void ClearBubble()
    {
        if (_tutorialUi.ActiveOverlay is { Id: OverlayId })
            _tutorialUi.RequestClose(false);
    }
}
