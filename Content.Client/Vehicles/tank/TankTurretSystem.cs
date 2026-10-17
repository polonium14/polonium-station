using Content.Shared.Movement.Components;
using Content.Shared.Vehicles;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.Vehicles;

/// <summary>
/// Wieża za kursorem + strzał F/R.
/// Kąt strzału = prosto do myszy; AimOffset tylko pod sprite.
/// </summary>
public sealed partial class TankTurretSystem : SharedTankTurretSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private bool _fWasDown;
    private TimeSpan _nextMgSend = TimeSpan.Zero;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalEntity is not { Valid: true } player)
            return;

        if (!TryComp(player, out RelayInputMoverComponent? relay))
            return;

        var tank = relay.RelayEntity;
        if (!tank.IsValid())
            return;

        if (!TryComp(tank, out TankTurretComponent? turret))
            return;

        if (!TryComp(tank, out TransformComponent? xform))
            return;

        var tankPos = _xform.GetWorldPosition(xform);

        var mouseMap = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mouseMap != MapCoordinates.Nullspace && mouseMap.MapId == xform.MapID)
        {
            var dir = mouseMap.Position - tankPos;
            if (dir.LengthSquared() > 0.0001f)
            {
                // Kierunek strzału = prosto do myszy (bez AimOffset)
                var target = dir.ToWorldAngle();
                var diff = Angle.ShortestDistance(turret.TurretAngle, target);
                var maxStep = Math.Max(turret.RotateSpeed, 8f) * frameTime;

                Angle newAngle;
                if (Math.Abs(diff.Theta) <= maxStep)
                    newAngle = target;
                else
                    newAngle = turret.TurretAngle + new Angle(Math.Sign(diff.Theta) * maxStep);

                turret.TurretAngle = newAngle;

                if (_timing.IsFirstTimePredicted)
                    Dirty(tank, turret);

                // AimOffset tylko pod grafikę warstwy wieży
                if (TryComp(tank, out SpriteComponent? sprite))
                {
                    var se = new Entity<SpriteComponent?>(tank, sprite);
                    if (_sprite.LayerMapTryGet(se, TankVisualLayers.Turret, out var layer, false))
                    {
                        var worldRot = _xform.GetWorldRotation(xform);
                        var relative = newAngle - worldRot + Angle.FromDegrees(turret.AimOffset);
                        _sprite.LayerSetRotation(se, layer, relative);
                    }
                }
            }
        }

        if (!_timing.IsFirstTimePredicted)
            return;

        var aim = turret.TurretAngle;

        // F — działo (jeden strzał na naciśnięcie)
        var fDown = _input.IsKeyDown(Keyboard.Key.F);
        if (fDown && !_fWasDown)
            RaiseNetworkEvent(new TankShootEvent(false, aim, tankPos));
        _fWasDown = fDown;

        // R — karabin (seria)
        if (_input.IsKeyDown(Keyboard.Key.R))
        {
            var now = _timing.CurTime;
            if (now >= _nextMgSend)
            {
                _nextMgSend = now + TimeSpan.FromSeconds(0.08);
                RaiseNetworkEvent(new TankShootEvent(true, aim, tankPos));
            }
        }
    }
}