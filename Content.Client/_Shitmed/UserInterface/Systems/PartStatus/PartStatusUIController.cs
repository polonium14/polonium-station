using Content.Client._Shitmed.Targeting;
using Content.Client._Shitmed.UserInterface.Systems.PartStatus.Widgets;
using Content.Client.Gameplay;
using Content.Shared._Shitmed.PartStatus.Events;
using Content.Shared._Shitmed.Targeting;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Shitmed.UserInterface.Systems.PartStatus;

public sealed partial class PartStatusUIController : UIController, IOnStateEntered<GameplayState>, IOnSystemChanged<TargetingSystem>
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IEntityNetworkManager _net = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private SpriteSystem? _spriteSystem;
    private TargetingComponent? _targetingComponent;
    private PartStatusControl? PartStatusControl => UIManager.GetActiveUIWidgetOrNull<PartStatusControl>();

    public void OnSystemLoaded(TargetingSystem system)
    {
        system.PartStatusStartup += AddPartStatusControl;
        system.PartStatusShutdown += RemovePartStatusControl;
        system.PartStatusUpdate += UpdatePartStatusControl;
    }

    public void OnSystemUnloaded(TargetingSystem system)
    {
        system.PartStatusStartup -= AddPartStatusControl;
        system.PartStatusShutdown -= RemovePartStatusControl;
        system.PartStatusUpdate -= UpdatePartStatusControl;
    }

    public void OnStateEntered(GameplayState state)
    {
        if (PartStatusControl == null)
            return;

        PartStatusControl.SetVisible(_targetingComponent != null);

        if (_targetingComponent != null)
            PartStatusControl.SetTextures(_targetingComponent.BodyStatus);
    }

    public void AddPartStatusControl(TargetingComponent component)
    {
        _targetingComponent = component;

        if (PartStatusControl == null)
            return;

        PartStatusControl.SetVisible(true);
        PartStatusControl.SetTextures(component.BodyStatus);
    }

    public void RemovePartStatusControl()
    {
        PartStatusControl?.SetVisible(false);
        _targetingComponent = null;
    }

    public void UpdatePartStatusControl(TargetingComponent component)
    {
        if (PartStatusControl != null && _targetingComponent != null)
            PartStatusControl.SetTextures(_targetingComponent.BodyStatus);
    }

    public Texture GetTexture(SpriteSpecifier specifier)
    {
        _spriteSystem ??= _entManager.System<SpriteSystem>();
        return _spriteSystem.Frame0(specifier);
    }

    public void GetPartStatusMessage()
    {
        if (_playerManager.LocalEntity is not { } user
            || _entManager.GetComponent<TargetingComponent>(user) is not { }
            || PartStatusControl == null
            || !_timing.IsFirstTimePredicted)
            return;

        var player = _entManager.GetNetEntity(user);
        _net.SendSystemNetworkMessage(new GetPartStatusEvent(player));
    }
}
