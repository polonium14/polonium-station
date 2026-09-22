using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
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

public sealed partial class TankTurretSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private bool _wasMainDown;
    private Angle _lastSentAngle;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalEntity is not { Valid: true } player)
            return;

        if (TryComp(player, out MobStateComponent? mob) &&
            mob.CurrentState != MobState.Alive)
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

        var mouseMap = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mouseMap == MapCoordinates.Nullspace)
            return;

        var tankPos = _xform.GetWorldPosition(xform);
        var dir = mouseMap.Position - tankPos;
        if (dir.LengthSquared() < 0.0001f)
            return;

        var target = dir.ToWorldAngle();
        var current = turret.TurretAngle;
        var delta = Angle.ShortestDistance(current, target);
        var maxStep = Math.Max(turret.RotateSpeed, 8f) * frameTime;
        var step = Math.Clamp(delta.Theta, -maxStep, maxStep);
        turret.TurretAngle = current + new Angle(step);

        if (TryComp(tank, out SpriteComponent? sprite))
        {
            var se = (Entity<SpriteComponent?>)(tank, sprite);
            if (_sprite.LayerMapTryGet(se, TankVisualLayers.Turret, out var layer, false))
            {
                var worldRot = _xform.GetWorldRotation(xform);
                var relative = turret.TurretAngle - worldRot + Angle.FromDegrees(turret.AimOffset);
                _sprite.LayerSetRotation(se, layer, relative);
            }
        }

        if (!_timing.IsFirstTimePredicted)
            return;

        var angleDiff = Angle.ShortestDistance(_lastSentAngle, turret.TurretAngle);
        if (Math.Abs(angleDiff.Theta) > 0.02)
        {
            Dirty(tank, turret);
            _lastSentAngle = turret.TurretAngle;
        }

        var mainDown = _input.IsKeyDown(Keyboard.Key.F);
        var mgDown = _input.IsKeyDown(Keyboard.Key.R);

        if (mainDown && !_wasMainDown)
            RaisePredictiveEvent(new TankShootEvent(true, turret.TurretAngle, tankPos));

        if (mgDown)
            RaisePredictiveEvent(new TankShootEvent(false, turret.TurretAngle, tankPos));

        _wasMainDown = mainDown;
    }
}