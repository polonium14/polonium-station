using Content.Client.Ghost;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Shared._Polonium.GhostThemes;
using Content.Shared.Ghost.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.GhostThemes;

/// <summary>
/// Opens the ghost theme window from the button in the ghost bar.
/// </summary>
public sealed class GhostThemeUIController : UIController, IOnSystemChanged<GhostThemeSystem>, IOnSystemChanged<GhostSystem>
{
    [UISystemDependency] private readonly GhostThemeSystem? _system = default;

    private GhostThemeWindow? _window;

    // Set while waiting for the server to say which themes the player can use.
    private bool _opening;

    private GhostGui? Gui => UIManager.GetActiveUIWidgetOrNull<GhostGui>();

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    public void OnSystemLoaded(GhostThemeSystem system)
    {
        system.ThemesReceived += OnThemesReceived;
        system.EnabledChanged += OnEnabledChanged;
    }

    public void OnSystemUnloaded(GhostThemeSystem system)
    {
        system.ThemesReceived -= OnThemesReceived;
        system.EnabledChanged -= OnEnabledChanged;
    }

    // A player who is no longer a ghost can't pick a theme, the server would ignore it.
    public void OnSystemLoaded(GhostSystem system)
    {
        system.PlayerRemoved += OnGhostRemoved;
        system.PlayerDetached += CloseWindow;
    }

    public void OnSystemUnloaded(GhostSystem system)
    {
        system.PlayerRemoved -= OnGhostRemoved;
        system.PlayerDetached -= CloseWindow;
    }

    /// <summary>
    /// Opens the window once the server says which themes the player can use.
    /// </summary>
    public void OpenWindow()
    {
        if (_window != null)
        {
            _window.MoveToFront();
            return;
        }

        if (_system is not { Enabled: true })
            return;

        _opening = true;
        _system.RequestThemes();
    }

    private void OnScreenLoad()
    {
        if (Gui is not { } gui)
            return;

        gui.GhostThemeButton.OnPressed += OnButtonPressed;
        gui.GhostThemeButton.Visible = _system?.Enabled ?? false;
    }

    private void OnScreenUnload()
    {
        if (Gui is { } gui)
            gui.GhostThemeButton.OnPressed -= OnButtonPressed;

        CloseWindow();
    }

    private void OnGhostRemoved(GhostComponent component)
    {
        CloseWindow();
    }

    private void CloseWindow()
    {
        _opening = false;
        _window?.Close();
    }

    private void OnButtonPressed(BaseButton.ButtonEventArgs args)
    {
        OpenWindow();
    }

    private void OnEnabledChanged(bool enabled)
    {
        if (Gui is { } gui)
            gui.GhostThemeButton.Visible = enabled;

        if (!enabled)
            CloseWindow();
    }

    private void OnThemesReceived(HashSet<ProtoId<GhostThemePrototype>> available)
    {
        if (!_opening || _system == null)
            return;

        _opening = false;

        _window = new GhostThemeWindow();
        _window.OnClose += () => _window = null;
        _window.ThemeChosen += (theme, color) => _system?.ChooseTheme(theme, color);
        _window.Populate(available, _system.SavedTheme, _system.SavedColor);
        _window.OpenCentered();
    }
}
