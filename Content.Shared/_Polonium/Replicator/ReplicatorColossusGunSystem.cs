using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Replicator;

public sealed partial class ReplicatorColossusGunSystem : EntitySystem
{
    private const float WallClearance = 0.15f;
    private const int LaserShotCost = 2;

    private static readonly EntProtoId LaserProto = "WeaponReplicatorColossusLaser";

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorColossusGunComponent, HandSelectedEvent>(OnSelected);
        SubscribeLocalEvent<ReplicatorColossusGunComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<ReplicatorColossusGunComponent, RMCBeforeMuzzleFlashEvent>(OnBeforeMuzzleFlash);
        SubscribeLocalEvent<ReplicatorColossusGunComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<ReplicatorColossusAmmoComponent, MapInitEvent>(OnAmmoMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ReplicatorColossusAmmoComponent>();
        while (query.MoveNext(out var uid, out var ammo))
        {
            if (ammo.NextCharge is not { } next || next > _timing.CurTime)
                continue;

            ammo.Count = Math.Min(ammo.Count + 1, ammo.Capacity);
            ammo.NextCharge = ammo.Count < ammo.Capacity ? next + ammo.RechargeCooldown : null;
            Dirty(uid, ammo);
            UpdateAmmoCounts((uid, ammo));
        }
    }

    private void OnAmmoMapInit(Entity<ReplicatorColossusAmmoComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Count = ent.Comp.Capacity;
        Dirty(ent);
    }

    private void OnSelected(Entity<ReplicatorColossusGunComponent> ent, ref HandSelectedEvent args)
    {
        if (_timing.ApplyingState || !TryComp<MeleeWeaponComponent>(ent, out var melee))
            return;

        var next = melee.NextAttack;
        foreach (var held in _hands.EnumerateHeld(args.User))
        {
            if (TryComp<MeleeWeaponComponent>(held, out var other) && other.NextAttack > next)
                next = other.NextAttack;
        }

        if (next == melee.NextAttack)
            return;

        melee.NextAttack = next;
        DirtyField(ent.Owner, melee, nameof(MeleeWeaponComponent.NextAttack));
    }

    private void OnAttemptShoot(Entity<ReplicatorColossusGunComponent> ent, ref AttemptShootEvent args)
    {
        if (!args.Cancelled && TryGetMuzzleOffset(ent, args.User, out var offset, clampToWalls: true))
            args.FromCoordinates = _transform.ToCoordinates(_transform.GetMapCoordinates(args.User).Offset(offset));
    }

    private void OnBeforeMuzzleFlash(Entity<ReplicatorColossusGunComponent> ent, ref RMCBeforeMuzzleFlashEvent args)
    {
        var holder = Transform(ent).ParentUid;
        if (TryGetMuzzleOffset(ent, holder, out var offset, clampToWalls: false))
            args.Offset = (-_transform.GetWorldRotation(holder)).RotateVec(offset);
    }

    private void OnGunShot(Entity<ReplicatorColossusGunComponent> ent, ref GunShotEvent args)
    {
        ent.Comp.Shots++;
        Dirty(ent);

        if (ent.Comp.SharedAmmo)
            Drain(args.User, args.Ammo.Count * (MetaData(ent).EntityPrototype?.ID == LaserProto.Id ? LaserShotCost : 1)); // hardcoded intentionally
    }

    public void Drain(Entity<ReplicatorColossusAmmoComponent?> ent, int amount)
    {
        if (amount <= 0 || !Resolve(ent, ref ent.Comp, false) || ent.Comp.Count == 0)
            return;

        ent.Comp.Count = Math.Max(ent.Comp.Count - amount, 0);
        ent.Comp.NextCharge ??= _timing.CurTime + ent.Comp.RechargeCooldown;
        Dirty(ent);
        UpdateAmmoCounts((ent.Owner, ent.Comp));
    }

    private void UpdateAmmoCounts(Entity<ReplicatorColossusAmmoComponent> ent)
    {
        foreach (var held in _hands.EnumerateHeld(ent.Owner))
        {
            if (TryComp<ReplicatorColossusGunComponent>(held, out var gun) && gun.SharedAmmo)
                _gun.UpdateBasicEntityAmmoCount(held, ent.Comp.Count);
        }
    }

    private bool TryGetMuzzleOffset(Entity<ReplicatorColossusGunComponent> ent, EntityUid holder, out Vector2 offset, bool clampToWalls)
    {
        offset = default;
        var view = TryComp<InputMoverComponent>(holder, out var mover) ? _mover.GetParentGridAngle(mover) : Angle.Zero;
        var direction = (_transform.GetWorldRotation(holder) - view).GetCardinalDir();
        if (!ent.Comp.MuzzleOffsets.TryGetValue(direction, out var muzzles) || muzzles.Count == 0)
            return false;

        offset = view.RotateVec(muzzles[ent.Comp.Shots % muzzles.Count]);
        if (offset == Vector2.Zero)
            return false;

        if (!clampToWalls)
            return true;

        var origin = _transform.GetMapCoordinates(holder);
        var length = offset.Length();
        var ray = new CollisionRay(origin.Position, offset / length, (int) CollisionGroup.HighImpassable);
        var hits = _physics.IntersectRay(origin.MapId, ray, length, holder, returnOnFirstHit: false).ToList();
        if (hits.Count == 0)
            return true;

        var free = hits.Min(hit => hit.Distance) - WallClearance;
        if (free <= 0f)
            return false;

        offset *= free / length;
        return true;
    }
}
