using System;
using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.Viewport;
using Content.Shared._Polonium.Photography;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client._Polonium.Photography;

/// <summary>Drives the <see cref="CameraViewfinderOverlay"/> while a camera is in the active hand and the cursor is over the game view: each frame resolves the target (entity under cursor, else cursor tile), eases the window toward it, and reddens the border when the shot is blocked by line of sight / range.</summary>
public sealed class CameraViewfinderSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IStateManager _state = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private const float LerpFactor = 20f;

    private CameraViewfinderOverlay _viewfinder = default!;
    private bool _added;

    private Vector2 _center;
    private MapId _centerMap = MapId.Nullspace;

    public override void Initialize()
    {
        base.Initialize();
        _viewfinder = new CameraViewfinderOverlay();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Hide();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!TryGetTarget(out var player, out var target, out var onEntity))
        {
            Hide();
            return;
        }

        if (_centerMap != target.MapId)
        {
            _center = target.Position;
            _centerMap = target.MapId;
        }
        else
        {
            _center = Vector2.Lerp(_center, target.Position, MathF.Min(1f, LerpFactor * frameTime));
        }

        var valid = _examine.InRangeUnOccluded(player, target, PhotographyConstants.PhotoMaxRange);
        var border = !valid ? Color.Red : onEntity ? Color.Blue : Color.Black;

        Show(_center, border);
    }

    private bool TryGetTarget(out EntityUid player, out MapCoordinates target, out bool onEntity)
    {
        player = default;
        target = default;
        onEntity = false;

        if (_player.LocalSession?.AttachedEntity is not { } p)
            return false;
        player = p;

        if (_hands.GetActiveItem(player) is not { } item || !HasComp<PoloniumCameraComponent>(item))
            return false;

        if (_inventory.TryGetSlotEntity(player, SharedPoloniumPhotographySystem.EyeSlot, out _))
            return false;

        if (_state.CurrentState is not GameplayStateBase screen)
            return false;
        if (_ui.CurrentlyHovered is not IViewportControl vp || !_input.MouseScreenPosition.IsValid)
            return false;

        var mousePos = vp.PixelToMap(_input.MouseScreenPosition.Position);
        if (mousePos.MapId == MapId.Nullspace)
            return false;

        var entity = _input.IsKeyDown(Keyboard.Key.Shift)
            ? null
            : vp is ScalingViewport svp
                ? screen.GetClickedEntity(mousePos, svp.Eye)
                : screen.GetClickedEntity(mousePos);

        if (entity is { } e && !Deleted(e))
        {
            target = _transform.GetMapCoordinates(e);
            onEntity = true;
        }
        else
        {
            target = mousePos;
        }

        return true;
    }

    private void Show(Vector2 centerWorld, Color border)
    {
        if (!_added)
        {
            _overlay.AddOverlay(_viewfinder);
            _added = true;
        }

        _viewfinder.CenterWorld = centerWorld;
        _viewfinder.BorderColor = border;
    }

    private void Hide()
    {
        if (_added)
        {
            _overlay.RemoveOverlay(_viewfinder);
            _added = false;
        }

        _centerMap = MapId.Nullspace;
    }
}
