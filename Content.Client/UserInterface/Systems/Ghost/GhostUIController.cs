using Content.Client.Gameplay;
using Content.Client.Ghost;
using Content.Client._Polonium.NewLife;
using Content.Client._Polonium.Tutorial;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Shared._Polonium.Tutorial;
using Content.Shared.Ghost.Components;
using Content.Shared.Ghost.Systems;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.Ghost;

// TODO hud refactor BEFORE MERGE fix ghost gui being too far up
public sealed partial class GhostUIController : UIController, IOnSystemChanged<GhostSystem>
{
    [Dependency] private IEntityNetworkManager _net = default!;

    [UISystemDependency] private readonly GhostSystem? _system = default;
    [UISystemDependency] private readonly TutorialPresentationSystem _tutorial = default!;
    [UISystemDependency] private readonly NewLifeSystem? _newLife = default;

    private NewLifeWindow? _newLifeWindow;
    private TimeSpan _nextNewLifeRefresh;

    private GhostGui? Gui => UIManager.GetActiveUIWidgetOrNull<GhostGui>();

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        LoadGui();
    }

    private void OnScreenUnload()
    {
        UnloadGui();
    }

    public void OnSystemLoaded(GhostSystem system)
    {
        system.PlayerRemoved += OnPlayerRemoved;
        system.PlayerUpdated += OnPlayerUpdated;
        system.PlayerAttached += OnPlayerAttached;
        system.PlayerDetached += OnPlayerDetached;
        system.GhostWarpsResponse += OnWarpsResponse;
        system.GhostRoleCountUpdated += OnRoleCountUpdated;
    }

    public void OnSystemUnloaded(GhostSystem system)
    {
        system.PlayerRemoved -= OnPlayerRemoved;
        system.PlayerUpdated -= OnPlayerUpdated;
        system.PlayerAttached -= OnPlayerAttached;
        system.PlayerDetached -= OnPlayerDetached;
        system.GhostWarpsResponse -= OnWarpsResponse;
        system.GhostRoleCountUpdated -= OnRoleCountUpdated;
    }

    public void UpdateGui()
    {
        if (Gui == null)
        {
            return;
        }

        Gui.Visible = _system?.IsGhost ?? false;

        var tutorialLobby = _tutorial.ReadIntroMode() == SharedTutorialSystem.IntroTutorial;

        Gui.Update(
            _system?.AvailableGhostRoleCount,
            _system?.Player?.CanReturnToBody,
            tutorialLobby);

        UpdateNewLife(tutorialLobby);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);


        _nextNewLifeRefresh -= TimeSpan.FromSeconds(args.DeltaSeconds);
        if (_nextNewLifeRefresh > TimeSpan.Zero || Gui is not { Visible: true })
            return;

        _nextNewLifeRefresh = TimeSpan.FromSeconds(1);

        UpdateNewLife(_tutorial.ReadIntroMode() == SharedTutorialSystem.IntroTutorial);
    }

    private void UpdateNewLife(bool tutorialLobby)
    {
        if (Gui == null)
            return;

        var remaining = TimeSpan.Zero;

        var visible = !tutorialLobby && _newLife != null && _newLife.TryGetRemaining(out remaining);

        Gui.UpdateNewLife(visible, remaining);

        if (!visible)
            _newLifeWindow?.Close();
        else
            RefreshNewLifeWindow(remaining);
    }

    private void RefreshNewLifeWindow(TimeSpan remaining)
    {
        _newLifeWindow?.SetAvailable(remaining <= TimeSpan.Zero);
        _newLifeWindow?.SetLivesLeft(_newLife?.GetLivesLeft());
    }

    private void OnPlayerRemoved(GhostComponent component)
    {
        Gui?.Hide();
        _newLifeWindow?.Close();
    }

    private void OnPlayerUpdated(GhostComponent component)
    {
        UpdateGui();
    }

    private void OnPlayerAttached(GhostComponent component)
    {
        if (Gui == null)
            return;

        Gui.Visible = true;
        UpdateGui();
    }

    private void OnPlayerDetached()
    {
        Gui?.Hide();
        _newLifeWindow?.Close();
    }

    private void OnWarpsResponse(GhostWarpsResponseEvent msg)
    {
        if (Gui?.TargetWindow is not { } window)
            return;

        window.UpdateWarps(msg.Warps);
        window.Populate();
    }

    private void OnRoleCountUpdated(GhostUpdateGhostRoleCountEvent msg)
    {
        UpdateGui();
    }

    private void OnWarpClicked(NetEntity player)
    {
        var msg = new GhostWarpToTargetRequestEvent(player);
        _net.SendSystemNetworkMessage(msg);
    }

    private void OnGhostnadoClicked()
    {
        var msg = new GhostnadoRequestEvent();
        _net.SendSystemNetworkMessage(msg);
    }

    private void OnWarpToRandomFollowedClicked()
    {
        var msg = new WarpToRandomFollowedRequestEvent();
        _net.SendSystemNetworkMessage(msg);
    }

    private void OnWarpToRandomClicked()
    {
        var msg = new WarpToRandomRequestEvent();
        _net.SendSystemNetworkMessage(msg);
    }

    public void LoadGui()
    {
        if (Gui == null)
            return;

        Gui.RequestWarpsPressed += RequestWarps;
        Gui.ReturnToBodyPressed += ReturnToBody;
        Gui.ReturnToLobbyPressed += ReturnToLobby;
        Gui.GhostRolesPressed += GhostRolesPressed;
        Gui.NewLifePressed += NewLifePressed;
        Gui.TargetWindow.WarpClicked += OnWarpClicked;
        Gui.TargetWindow.OnGhostnadoClicked += OnGhostnadoClicked;
        Gui.TargetWindow.OnWarpToRandomFollowedClicked += OnWarpToRandomFollowedClicked;
        Gui.TargetWindow.OnWarpToRandomClicked += OnWarpToRandomClicked;

        UpdateGui();
    }

    public void UnloadGui()
    {
        if (Gui == null)
            return;

        Gui.RequestWarpsPressed -= RequestWarps;
        Gui.ReturnToBodyPressed -= ReturnToBody;
        Gui.ReturnToLobbyPressed -= ReturnToLobby;
        Gui.GhostRolesPressed -= GhostRolesPressed;
        Gui.NewLifePressed -= NewLifePressed;
        Gui.TargetWindow.WarpClicked -= OnWarpClicked;

        Gui.Hide();
        _newLifeWindow?.Close();
    }

    private void ReturnToBody()
    {
        _system?.ReturnToBody();
    }

    private void ReturnToLobby()
    {
        _tutorial.RequestReturnToLobby();
    }

    private void RequestWarps()
    {
        _system?.RequestWarps();
        Gui?.TargetWindow.Populate();
        Gui?.TargetWindow.OpenCentered();
    }

    private void GhostRolesPressed()
    {
        _system?.OpenGhostRoles();
    }

    private void NewLifePressed()
    {
        if (_newLifeWindow != null)
        {
            _newLifeWindow.MoveToFront();
            return;
        }

        _newLifeWindow = new NewLifeWindow();

        _newLifeWindow.Confirmed += () => _newLife?.RequestNewLife();
        _newLifeWindow.OnClose += () => _newLifeWindow = null;
        _newLifeWindow.OpenCentered();

        UpdateGui();
    }
}
