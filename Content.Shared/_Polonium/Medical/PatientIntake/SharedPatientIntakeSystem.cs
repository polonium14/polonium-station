// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Body;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Morgue;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Medical.PatientIntake;

/// <summary>
/// What the intake tray lets close. Shared so the client predicts a refused close instead of
/// showing the tray shut and snapping it back open.
/// </summary>
public abstract partial class SharedPatientIntakeSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedPopupSystem _popups = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedEntityStorageSystem _entityStorage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PatientIntakeMachineComponent, StorageAfterOpenEvent>(OnAfterOpen);
        SubscribeLocalEvent<PatientIntakeMachineComponent, StorageCloseAttemptEvent>(OnCloseAttempt);
        SubscribeLocalEvent<PatientIntakeMachineComponent, StorageBeforeCloseEvent>(OnBeforeClose,
            after: [typeof(EntityStorageLayingDownOverrideSystem)]);
    }

    private void OnAfterOpen(Entity<PatientIntakeMachineComponent> ent, ref StorageAfterOpenEvent args)
    {
        ent.Comp.CloseLockedUntil = _timing.CurTime + ent.Comp.CloseLockTime;
        ent.Comp.LastOpened = _timing.CurTime;
        Dirty(ent);
        OnOpened(ent);
    }

    protected virtual void OnOpened(Entity<PatientIntakeMachineComponent> ent)
    {
    }

    private void OnCloseAttempt(Entity<PatientIntakeMachineComponent> ent, ref StorageCloseAttemptEvent args)
    {
        if (!TryComp<EntityStorageComponent>(ent, out var storage))
            return;

        var contents = GetEntitiesToInsert(ent, storage);
        contents.UnionWith(storage.Contents.ContainedEntities);
        // Ghosts float over the tray but never go in.
        contents.RemoveWhere(HasComp<GhostComponent>);

        // Everything in the tray is deleted on close, so a mind anywhere inside it -
        // a body bag, a box, the patient's pockets - must stop it.
        if (contents.Any(ContainsMind))
        {
            Reject(ent, args.User, "patient-intake-popup-mind");
            args.Cancelled = true;
            return;
        }

        // Loose items may go in; a body may only go in as the issued patient, so a corpse
        // whose player ghosted (or anyone's body bag) is never deleted with it.
        if (contents.Any(e => ContainsForeignBody(ent, e)))
        {
            Reject(ent, args.User, "patient-intake-popup-no-patient");
            args.Cancelled = true;
            return;
        }

        if (_timing.CurTime < ent.Comp.CloseLockedUntil)
        {
            var seconds = (int) Math.Ceiling((ent.Comp.CloseLockedUntil - _timing.CurTime).TotalSeconds);
            if (args.User is { } user)
                _popups.PopupClient(Loc.GetString("patient-intake-popup-locked", ("seconds", seconds)), ent, user);
            args.Cancelled = true;
        }
    }

    /// <summary>
    /// The morgue only takes in bodies lying down; a healed patient may well be standing.
    /// While an earlier patient goes back, patients ejected by this opening stay on the tray:
    /// in a mass casualty, the next patient lands on the tray the medics open to return one.
    /// </summary>
    private void OnBeforeClose(Entity<PatientIntakeMachineComponent> ent, ref StorageBeforeCloseEvent args)
    {
        if (!TryComp<EntityStorageComponent>(ent, out var storage))
            return;

        var patients = GetTrayEntities(ent, storage).Where(e => IsCasePatient(ent, e)).ToList();
        var returning = patients.Any(p => !EjectedByLastOpening(ent, p));

        foreach (var patient in patients)
        {
            if (returning && EjectedByLastOpening(ent, patient))
                continue;

            args.Contents.Add(patient);
            args.BypassChecks.Add(patient);
        }
    }

    private bool EjectedByLastOpening(Entity<PatientIntakeMachineComponent> ent, EntityUid patient)
    {
        return TryComp<PatientIntakePatientComponent>(patient, out var comp) && comp.ClockStart == ent.Comp.LastOpened;
    }

    /// <summary>
    /// What closing would actually take in, after the laying-down rule and the insert checks,
    /// so a medic standing on the tray tile doesn't block the close.
    /// </summary>
    private HashSet<EntityUid> GetEntitiesToInsert(EntityUid machine, EntityStorageComponent storage)
    {
        var ev = new StorageBeforeCloseEvent(null, GetTrayEntities(machine, storage), []);
        RaiseLocalEvent(machine, ref ev);
        ev.Contents.RemoveWhere(e => !ev.BypassChecks.Contains(e) && !_entityStorage.CanInsert(e, machine, storage));
        return ev.Contents;
    }

    protected bool IsCasePatient(EntityUid machine, EntityUid entity)
    {
        return TryComp<PatientIntakePatientComponent>(entity, out var comp) && comp.Machine == machine;
    }

    private bool ContainsForeignBody(EntityUid machine, EntityUid uid)
    {
        if (HasComp<BodyComponent>(uid) && !IsCasePatient(machine, uid))
            return true;

        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return false;

        foreach (var container in _containers.GetAllContainers(uid, manager))
        {
            if (container.ContainedEntities.Any(e => ContainsForeignBody(machine, e)))
                return true;
        }

        return false;
    }

    private bool ContainsMind(EntityUid uid)
    {
        if (TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
            return true;

        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return false;

        foreach (var container in _containers.GetAllContainers(uid, manager))
        {
            if (container.ContainedEntities.Any(ContainsMind))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Same lookup EntityStorage uses to gather what closing would take in.
    /// </summary>
    private HashSet<EntityUid> GetTrayEntities(EntityUid machine, EntityStorageComponent storage)
    {
        var entities = _lookup.GetEntitiesInRange(
            new EntityCoordinates(machine, storage.EnteringOffset),
            storage.EnteringRange,
            LookupFlags.Approximate | LookupFlags.Dynamic | LookupFlags.Sundries);
        entities.Remove(machine);
        return entities;
    }

    private void Reject(EntityUid machine, EntityUid? user, LocId message)
    {
        if (user is { } u)
            _popups.PopupClient(Loc.GetString(message), machine, u);
    }
}
