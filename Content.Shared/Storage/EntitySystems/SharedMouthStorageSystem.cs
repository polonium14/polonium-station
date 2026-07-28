// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 coderabbitai[bot] <136622811+coderabbitai[bot]@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Standing;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Shared.Storage.EntitySystems;

public abstract partial class SharedMouthStorageSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MouthStorageComponent, MapInitEvent>(OnMouthStorageInit);
        SubscribeLocalEvent<MouthStorageComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<MouthStorageComponent, DisarmedEvent>(OnDisarmed);
        SubscribeLocalEvent<MouthStorageComponent, DamageChangedEvent>(OnDamageModified);
        SubscribeLocalEvent<MouthStorageComponent, ExaminedEvent>(OnExamined);
    }

    protected bool IsMouthBlocked(MouthStorageComponent component)
    {
using Content.Shared.Storage.Components;
using Content.Shared.Storage;
    }

    private void OnMouthStorageInit(EntityUid uid, MouthStorageComponent component, MapInitEvent args)
    {
        component.Mouth = _containerSystem.EnsureContainer<Container>(uid, MouthStorageComponent.MouthContainerId);
        component.Mouth.ShowContents = false;
        component.Mouth.OccludesLight = false;

        var mouth = Spawn(component.MouthProto, new EntityCoordinates(uid, 0, 0));
        _containerSystem.Insert(mouth, component.Mouth);
        component.MouthId = mouth;

        if (component.OpenStorageAction != null && component.Action == null)
            _actionsSystem.AddAction(uid, ref component.Action, component.OpenStorageAction, mouth);
    }

    private void OnDowned(EntityUid uid, MouthStorageComponent component, DownedEvent args)
    {
        SpitOutContents(uid, component);
    }

    private void OnDisarmed(EntityUid uid, MouthStorageComponent component, ref DisarmedEvent args)
    {
        SpitOutContents(uid, component);
    }

    private void OnDamageModified(EntityUid uid, MouthStorageComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta == null
            || !args.DamageIncreased
            || args.DamageDelta.GetTotal() < component.SpitDamageThreshold)
            return;

        SpitOutContents(uid, component);
    }

    /// <summary>
    /// Dumps whatever is stashed in the cheeks onto the floor.
    /// </summary>
    private void SpitOutContents(EntityUid uid, MouthStorageComponent component)
    {
        if (!TryComp<StorageComponent>(component.MouthId, out var storage))
            return;

        _containerSystem.EmptyContainer(storage.Container, destination: Transform(uid).Coordinates);
    }

    // Other people can see if this person has items in their mouth.
    private void OnExamined(EntityUid uid, MouthStorageComponent component, ExaminedEvent args)
    {
        if (!IsMouthBlocked(component))
            return;

        var subject = Identity.Entity(uid, EntityManager);
        args.PushMarkup(Loc.GetString("mouth-storage-examine-condition-occupied", ("entity", subject)));
    }
}
