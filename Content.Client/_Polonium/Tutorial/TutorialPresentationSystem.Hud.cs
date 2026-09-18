using Content.Client._Shitmed.UserInterface.Systems.Targeting.Widgets;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Client.UserInterface.Systems.Alerts.Widgets;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Client.UserInterface.Systems.Inventory.Widgets;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Client.UserInterface.Screens;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Robust.Client.Input;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Tutorial;

/// <summary>
/// Finding a place in the game HUD to hang the tutorial off. The HUD is rebuilt whenever the
/// client changes screens, so every control is looked up again rather than held onto.
/// </summary>
public sealed partial class TutorialPresentationSystem
{
    [Dependency] private SharedTransformSystem _xform = default!;

    private static bool WantsInstructionOverlay(TutorialStepPrototype step)
    {
        return step.Blocking
               || HasAcknowledge(step.Completion)
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

    private void DropForeignOverlay()
    {
        if (_state.CurrentState is not GameplayState)
            return;

        if (_tutorialUi.ActiveOverlay is { } overlay && overlay.Id != OverlayId)
            _tutorialUi.DiscardActive();
    }
}
