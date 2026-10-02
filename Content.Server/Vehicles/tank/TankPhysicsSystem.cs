using System.Numerics;
using Content.Shared.Doors.Components;
using Content.Shared.Projectiles;
using Content.Shared.Vehicles;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server.Vehicles;

/// <summary>
/// Soft-hitbox pocisków jako prostokąt w lokalnych osiach czołgu
/// (trochę węższy, prawie pełna długość kadłuba) + miażdżenie tylko drzwi.
/// </summary>
public sealed partial class TankPhysicsSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;

private const float HalfWidth = 0.72f;   // trochę wężej
private const float HalfHeight = 1.55f;  // końcówki góra/dół

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TankTurretComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(EntityUid uid, TankTurretComponent component, ref PreventCollideEvent args)
    {
        var other = args.OtherEntity;

        // --- POCISKI ---
        if (TryComp(other, out ProjectileComponent? proj))
        {
            // Własne pociski zawsze przelatują
            if (proj.Shooter == uid || proj.Weapon == uid)
            {
                args.Cancelled = true;
                return;
            }

            // Soft-hitbox w lokalnych osiach czołgu (obraca się razem z kadłubem)
            var tankPos = _xform.GetWorldPosition(uid);
            var tankRot = _xform.GetWorldRotation(uid);
            var projPos = _xform.GetWorldPosition(other);

            var worldDelta = projPos - tankPos;
            var local = (-tankRot).RotateVec(worldDelta);

            // Poza prostokątem = brak trafienia
            if (MathF.Abs(local.X) > HalfWidth || MathF.Abs(local.Y) > HalfHeight)
            {
                args.Cancelled = true;
                return;
            }

            return;
        }

        // --- TYLKO DRZWI ---
        if (HasComp<DoorComponent>(other))
        {
            QueueDel(other);
            args.Cancelled = true;
            return;
        }

        // Reszta — normalna kolizja
    }
}