// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client._Polonium.Tutorial.Lobby.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;

namespace Content.Client._Polonium.Tutorial.Lobby;

/// <summary>
/// Provides functionality for managing highlight overlays on UI controls during tutorial lobby flows.
/// </summary>
public sealed class TutorialUIController : UIController
{
    [Dependency] private readonly IUserInterfaceManager _uiMan = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly TutorialManager _tutorialMan = default!;
    [Dependency] private readonly ILogManager _log = default!;
    private ISawmill _sawmill = default!;

    public TutorialHighlightOverlay? ActiveOverlay { get; private set; }
    public TutorialBubble? ActiveBubble { get; private set; }
    public event Action? NewOverlayEvent;

    private readonly Queue<(string, Control, Color?, bool, bool, bool)> _pendingOverlays = new();
    private readonly Queue<(TutorialBubble, TutorialHighlightOverlay.OverlayControlPosition, string?, Control?, float)> _pendingBubbles = new();

    public override void Initialize()
    {
        _sawmill = _log.GetSawmill("tutorial.ui");
    }

    public TutorialHighlightOverlay PlanOverlay(string id, Control? rootControl = null, Color? backgroundColor = null, bool isSelfClosingOnClick = false, bool ignoreBackgroundClicks = true)
    {
        CreateOverlay(id, rootControl ?? _uiMan.RootControl, backgroundColor, isSelfClosingOnClick, ignoreBackgroundClicks, false);

        return ActiveOverlay!;
    }

    public TutorialHighlightOverlay PlanOverlay(
        string id,
        Control controlToHighlight,
        Color highlightColor,
        float highlightMargin = 1f,
        bool orphanOnHighlightClick = false,
        Control? rootControl = null,
        Color? backgroundColor = null,
        bool isSelfClosingOnClick = false,
        bool ignoreBackgroundClicks = true,
        bool ignoreHighlightClicks = false
        )
    {
        var overlay = PlanOverlay(id, rootControl, backgroundColor, isSelfClosingOnClick, ignoreBackgroundClicks);

        overlay.HighlightControl(controlToHighlight, highlightColor, highlightMargin, orphanOnHighlightClick);

        return ActiveOverlay!;
    }

    private void CreateOverlay(string id, Control? rootControl, Color? color, bool isSelfClosingOnClick, bool ignoreBackgroundClicks, bool ignoreHighlightClicks)
    {
        if (ActiveOverlay is not null)
        {
            _pendingOverlays.Enqueue((id, rootControl ?? _uiMan.RootControl, color, isSelfClosingOnClick, ignoreBackgroundClicks, ignoreHighlightClicks));
            return;
        }

        DrawOverlay(id, rootControl ?? _uiMan.RootControl, color, isSelfClosingOnClick, ignoreBackgroundClicks, ignoreHighlightClicks);
    }

    public void PlanBubble(TutorialBubble bubble, TutorialHighlightOverlay.OverlayControlPosition position, Control? relativeToControl = null, float spacing = 100f, string? overlayId = null)
    {
        if (ActiveOverlay is null || (overlayId is not null && ActiveOverlay.Id != overlayId))
        {
            _pendingBubbles.Enqueue((bubble, position, overlayId, relativeToControl, spacing));
            return;
        }

        if (relativeToControl is { VisibleInTree: false })
        {
            _sawmill.Warning("Relative control is not visible in tree.");
        }

        if (ActiveBubble is not null)
        {
            _pendingBubbles.Enqueue((bubble, position, overlayId, relativeToControl, spacing));
            return;
        }

        DrawBubble(bubble, position, overlayId, relativeToControl, spacing);
    }

    private void DrawOverlay(string id, Control rootControl, Color? color, bool isSelfClosingOnClick, bool ignoreBackgroundClicks, bool ignoreHighlightClicks)
    {
        if (_pendingOverlays.Any(o => o.Item1 == id))
            throw new ArgumentException($"Overlay with id \"{id}\" already exists.");

        var overlay = new TutorialHighlightOverlay(id, rootControl, color, isSelfClosingOnClick, ignoreBackgroundClicks, ignoreHighlightClicks);
        overlay.SetPositionLast();

        overlay.InternalOverlayClosedEvent += () =>
        {
            if (ActiveOverlay == overlay)
            {
                RemoveOverlay(overlay);
            }
        };

        _sawmill.Debug($"Created overlay with id: {id}");

        ActiveOverlay = overlay;

        NewOverlayEvent?.Invoke();

        ProcessPendingBubblesForActiveOverlay();
    }

    private void DrawBubble(
        TutorialBubble bubble,
        TutorialHighlightOverlay.OverlayControlPosition position,
        string? overlayId = null,
        Control? relativeToControl = null,
        float spacing = 100f
        )
    {
        if (ActiveOverlay is null)
        {
            _sawmill.Error("Cannot draw bubble without an active overlay.");
            bubble.Dispose();
            return;
        }

        if (overlayId is not null && ActiveOverlay.Id != overlayId)
        {
            _pendingBubbles.Enqueue((bubble, position, overlayId, relativeToControl, spacing));
            return;
        }

        bubble.OnBubbleClosed -= OnBubbleClosed;
        bubble.OnBubbleClosed += OnBubbleClosed;

        if (relativeToControl is null)
        {
            ActiveOverlay.AddControlRelative(bubble, position, spacing);
        }
        else
        {
            ActiveOverlay.AddControlRelative(bubble, relativeToControl, position, spacing);
        }

        ActiveBubble = bubble;
    }

    public void ClearPendingOverlays()
    {
        _pendingOverlays.Clear();
    }

    public void ClearPendingBubbles()
    {
        _pendingBubbles.Clear();
    }

    public void RequestClose(bool completely)
    {
        if (completely)
        {
            _tutorialMan.CancelTutorial();
        }
        else if (ActiveOverlay is not null)
        {
            // Zamiast natychmiast usuwać overlay, wznosi się zdarzenie zamknięcia, aby zadziałały subskrybcje w poszczegolnych krokach.
            ActiveOverlay.DestroyOverlay();
        }
    }

    [Access(typeof(TutorialManager))]
    public void RemoveOverlay(TutorialHighlightOverlay overlay)
    {
        if (ActiveOverlay == null)
            return;

        ActiveOverlay.Orphan();
        ActiveOverlay = null;
        OnBubbleClosed();

        if (_pendingOverlays.Count > 0)
        {
            var (id, control, color, isSelfClosingOnClick, ignoreBackgroundClicks, ignoreHighlightClicks) = _pendingOverlays.Dequeue();
            DrawOverlay(id, control, color, isSelfClosingOnClick, ignoreBackgroundClicks, ignoreHighlightClicks);
        }
    }

    private void OnBubbleClosed()
    {
        ActiveBubble = null;

        ProcessPendingBubblesForActiveOverlay();
    }

    /// <summary>
    /// Processes pending intro bubbles for the currently active overlay, displaying the next eligible bubble if no
    /// bubble is currently active.
    /// </summary>
    /// <remarks>Only bubbles assigned to the active overlay are considered. If a bubble is already active or
    /// no overlay is present, the method does not display additional bubbles. Remaining bubbles not processed are
    /// retained for future processing.</remarks>
    private void ProcessPendingBubblesForActiveOverlay()
    {
        if (ActiveOverlay is null || ActiveBubble is not null)
            return;

        var remaining = new Queue<(TutorialBubble, TutorialHighlightOverlay.OverlayControlPosition, string?, Control?, float)>();

        while (_pendingBubbles.Count > 0)
        {
            var (bubble, position, overlayId, relativeToControl, spacing) = _pendingBubbles.Dequeue();

            if (overlayId is not null && ActiveOverlay.Id != overlayId)
            {
                remaining.Enqueue((bubble, position, overlayId, relativeToControl, spacing));
                continue;
            }

            if (ActiveBubble is not null)
            {
                remaining.Enqueue((bubble, position, overlayId, relativeToControl, spacing));
                continue;
            }

            DrawBubble(bubble, position, overlayId, relativeToControl, spacing);
        }

        while (remaining.Count > 0)
        {
            _pendingBubbles.Enqueue(remaining.Dequeue());
        }
    }
}
