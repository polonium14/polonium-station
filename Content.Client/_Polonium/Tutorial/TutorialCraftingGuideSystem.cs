using Content.Client._Polonium.Tutorial.UI;
using Content.Client._Polonium.UserInterface;
using Content.Client.Construction.UI;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Construction.Prototypes;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Tutorial;

/// <summary>
/// Teaches the crafting menu on a step that names a recipe. Until the menu is open the hud button glows;
/// inside it the glow moves from the search field or the recipe, to the build button, and a card beside
/// the menu says which of those the trainee is on. The open menu is reported to the server once per step.
/// </summary>
public sealed partial class TutorialCraftingGuideSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IUserInterfaceManager _uiMan = default!;
    [Dependency] private IResourceCache _cache = default!;

    private enum Phase : byte
    {
        Find,
        Build,
        Place,
    }

    private string? _step;
    private string? _recipe;
    private bool _reported;
    private TutorialGuideCard? _card;
    private UiGlowFrames? _frames;

    public override void Shutdown()
    {
        Reset();
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_player.LocalEntity is not { } player
            || !TryComp<TutorialSessionComponent>(player, out var session)
            || session.CurrentStep is not { } stepId
            || !_proto.TryIndex(stepId, out var step)
            || step.CraftingGuideRecipe is not { } recipeId
            || !_proto.TryIndex(recipeId, out var recipe))
        {
            Reset();
            return;
        }

        // server flags are per step, so the open menu has to be reported again on every step
        if (_step != stepId.Id)
        {
            _step = stepId.Id;
            _reported = false;
        }

        if (_recipe != recipe.ID)
        {
            RemoveCard();
            _recipe = recipe.ID;
        }

        var frames = EnsureFrames();
        var menu = FindMenu();
        if (menu == null)
        {
            if (_card != null)
                _card.Visible = false;

            frames.SetTargets(CraftingButton() is { } hudButton ? new Control[] { hudButton } : Array.Empty<Control>());
            return;
        }

        if (!_reported)
        {
            RaiseNetworkEvent(new TutorialCraftingMenuOpenedEvent());
            _reported = true;
        }

        var recipeButton = RecipeButton(menu, recipe.ID);
        var selected = recipeButton is { Pressed: true };
        var phase = !selected ? Phase.Find
            : menu.BuildButton.Pressed ? Phase.Place
            : Phase.Build;

        frames.SetTargets(phase switch
        {
            // the list only holds buttons for what is on screen, so without one the search field is the way
            Phase.Find => new Control[] { recipeButton ?? (Control) menu.SearchBar },
            Phase.Build => new Control[] { menu.BuildButton },
            _ => Array.Empty<Control>(),
        });

        var status = phase switch
        {
            Phase.Find when recipeButton == null => Loc.GetString("tutorial-craft-guide-status-search",
                ("name", recipe.Name ?? recipe.ID)),
            Phase.Find => Loc.GetString("tutorial-craft-guide-status-pick"),
            Phase.Build => Loc.GetString("tutorial-craft-guide-status-build"),
            _ => Loc.GetString("tutorial-craft-guide-status-place"),
        };

        var warning = recipe.ID == "CableTerminal" && phase == Phase.Place
            ? Loc.GetString("tutorial-craft-guide-need-rotate")
            : null;

        var card = EnsureCard(recipe);
        card.SetActiveStep((int) phase);
        card.SetStatus(status, null, warning);
        card.PlaceBeside(menu);
    }

    private ConstructionMenu? FindMenu()
    {
        foreach (var child in _uiMan.WindowRoot.Children)
        {
            if (child is ConstructionMenu { IsOpen: true } menu)
                return menu;
        }

        return null;
    }

    private Control? CraftingButton()
    {
        if (_uiMan.ActiveScreen is not { } screen)
            return null;

        return FindDescendant<GameTopMenuBar>(screen) is { VisibleInTree: true } bar ? bar.CraftingButton : null;
    }

    private static ListContainerButton? RecipeButton(ConstructionMenu menu, string recipe)
    {
        return FindDescendant<ListContainerButton>(menu.ListViewRecipes,
            button => button.Data is ConstructionMenu.ConstructionMenuListData data
                      && data.ConstructionProto.ID == recipe);
    }

    private static T? FindDescendant<T>(Control root, Func<T, bool>? match = null) where T : Control
    {
        foreach (var child in root.Children)
        {
            if (child is T found && (match == null || match(found)))
                return found;

            if (FindDescendant(child, match) is { } deeper)
                return deeper;
        }

        return null;
    }

    private UiGlowFrames EnsureFrames()
    {
        if (_frames != null)
            return _frames;

        _frames = new UiGlowFrames();
        _uiMan.PopupRoot.AddChild(_frames);
        LayoutContainer.SetAnchorPreset(_frames, LayoutContainer.LayoutPreset.Wide);
        return _frames;
    }

    private TutorialGuideCard EnsureCard(ConstructionPrototype recipe)
    {
        if (_card == null)
        {
            _card = new TutorialGuideCard(_cache,
                Loc.GetString("tutorial-craft-guide-title"),
                Loc.GetString("tutorial-craft-guide-goal", ("name", recipe.Name ?? recipe.ID)));

            _card.AddStep(null,
                Loc.GetString("tutorial-craft-guide-step-find"),
                Loc.GetString("tutorial-craft-guide-step-find-body"));
            _card.AddStep(null,
                Loc.GetString("tutorial-craft-guide-step-build"),
                Loc.GetString("tutorial-craft-guide-step-build-body"));
            _card.AddStep(null,
                Loc.GetString("tutorial-craft-guide-step-place"),
                Loc.GetString("tutorial-craft-guide-step-place-body"));

            _uiMan.PopupRoot.AddChild(_card);
        }

        _card.Visible = true;
        return _card;
    }

    private void RemoveCard()
    {
        _card?.Orphan();
        _card = null;
    }

    private void Reset()
    {
        RemoveCard();
        _frames?.Orphan();
        _frames = null;
        _step = null;
        _recipe = null;
        _reported = false;
    }
}
