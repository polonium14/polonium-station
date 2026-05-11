// SPDX-FileCopyrightText: 2024 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Actions;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Weapons.Ranged.Systems;

public sealed class ActionGunSystem : EntitySystem
{
    /// <summary>
    /// Hidden container on the action user that holds the spawned guns, so they live on the user map (instead of nullspace where audio doesn't work)
    /// </summary>
    public const string GunContainerId = "action-gun";

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionGunComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ActionGunComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ActionGunComponent, ActionGunShootEvent>(OnShoot);
    }

    private void OnMapInit(Entity<ActionGunComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        if (!string.IsNullOrEmpty(ent.Comp.Action) &&
            !string.IsNullOrEmpty(ent.Comp.GunProto))
        {
            _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
            ent.Comp.Gun = SpawnGunInContainer(ent.Comp.GunProto, ent.Owner);
        }

        foreach (var (action, gunProto) in ent.Comp.Actions)
        {
            EntityUid? actionEntity = null;
            _actions.AddAction(ent, ref actionEntity, action);
            if (actionEntity != null)
            {
                ent.Comp.ActionEntities[action] = actionEntity.Value;
                ent.Comp.Guns[actionEntity.Value] = SpawnGunInContainer(gunProto, ent.Owner);
            }
        }

        Dirty(ent);
    }

    private EntityUid SpawnGunInContainer(EntProtoId proto, EntityUid owner)
    {
        var gun = Spawn(proto);
        var container = _container.EnsureContainer<Container>(owner, GunContainerId);
        container.ShowContents = false;
        container.OccludesLight = false;
        _container.Insert(gun, container);
        return gun;
    }

    private void OnShutdown(Entity<ActionGunComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsClient)
            return;

        if (ent.Comp.Gun is {} gun)
            QueueDel(gun);

        foreach (var spawnedGun in ent.Comp.Guns.Values)
        {
            QueueDel(spawnedGun);
        }
        ent.Comp.Guns.Clear();
    }

    private void OnShoot(Entity<ActionGunComponent> ent, ref ActionGunShootEvent args)
    {
        EntityUid gunUid;
        GunComponent gun;
        if (ent.Comp.Guns.TryGetValue(args.Action, out var multiGunUid) && TryComp<GunComponent>(multiGunUid, out var multiGun))
        {
            gunUid = multiGunUid;
            gun = multiGun;
        }
        else if (ent.Comp.Gun is { } singleGunUid && TryComp<GunComponent>(singleGunUid, out var singleGun))
        {
            gunUid = singleGunUid;
            gun = singleGun;
        }
        else
        {
            return;
        }

        if (gun.NextFire > _timing.CurTime)
            return;

        var ammoEv = new GetAmmoCountEvent();
        RaiseLocalEvent(gunUid, ref ammoEv);
        if (ammoEv.Count <= 0)
            return;

        args.Handled = true;

        if (_net.IsServer)
        {
            _gun.AttemptShoot(ent, gunUid, gun, args.Target);
            return;
        }

        // Client only
        _audio.PlayPredicted(gun.SoundGunshotModified, gunUid, ent.Owner);
    }
}

