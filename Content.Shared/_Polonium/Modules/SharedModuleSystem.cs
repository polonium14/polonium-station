using System.Diagnostics.CodeAnalysis;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared._Polonium.Modules.Components;
using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared._Polonium.Modules.ModSuit.Events;
using Content.Shared._Polonium.Modules.ModSuit.UI;
using Content.Shared._Polonium.Modules.ModSuit.UI.Modules;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._Polonium.Modules;

public abstract partial class SharedModuleSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedModSuitSystem _modSuit = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        InitializePower();
        InitializeRelay();
        InitializeRequirements();
        InitializeSlot();

        SubscribeLocalEvent<ModuleContainerComponent, ComponentInit>(OnContainerInit);

        SubscribeLocalEvent<ModuleContainerComponent, ContainerIsInsertingAttemptEvent>(OnContainerInsertAttempt);
        SubscribeLocalEvent<ModuleContainerComponent, ContainerIsRemovingAttemptEvent>(OnContainerRemoveAttempt);

        SubscribeLocalEvent<ModuleContainerComponent, EntInsertedIntoContainerMessage>(OnContainerInserted);
        SubscribeLocalEvent<ModuleContainerComponent, EntRemovedFromContainerMessage>(OnContainerRemoved);

        SubscribeLocalEvent<ModuleContainerComponent, ModSuitGetUiEntriesEvent>(OnContainerGetModSuitUiEntry);
        SubscribeLocalEvent<ModuleContainedComponent, ModuleRelayedEvent<ModSuitGetUiEntriesEvent>>(OnModuleGetModSuitUiEntry);

        SubscribeLocalEvent<ModuleContainerComponent, ModSuitEjectButtonMessage>(OnModSuitEjectButton);
    }

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
    }

    private void OnContainerInit(Entity<ModuleContainerComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Modules = _container.EnsureContainer<Container>(ent.Owner, ent.Comp.ModuleContainerId);
    }

    private void OnContainerInsertAttempt(Entity<ModuleContainerComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.Container.ID != ent.Comp.ModuleContainerId)
            return;

        if (!_whitelist.CheckBoth(args.EntityUid, ent.Comp.Blacklist, ent.Comp.Whitelist))
        {
            args.Cancel();
            return;
        }

        var containerEv = new ModuleContainerModuleAddingAttemptEvent(args.EntityUid);
        RaiseLocalEvent(ent.Owner, containerEv);

        if (containerEv.Cancelled)
        {
            if (_net.IsServer && containerEv.Reason != null)
                _popup.PopupEntity(Loc.GetString(containerEv.Reason), ent.Owner);

            args.Cancel();
            return;
        }

        var moduleEv = new ModuleAddingAttemptEvent(ent.Owner);
        RaiseLocalEvent(args.EntityUid, moduleEv);

        if (moduleEv.Cancelled)
        {
            if (_net.IsServer && moduleEv.Reason != null)
                _popup.PopupEntity(Loc.GetString(moduleEv.Reason), ent.Owner);

            args.Cancel();
            return;
        }
    }

    private void OnContainerRemoveAttempt(Entity<ModuleContainerComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.Container.ID != ent.Comp.ModuleContainerId)
            return;

        // no whitelist check as how else did it get in here?

        var containerEv = new ModuleContainerModuleRemovingAttemptEvent(args.EntityUid);
        RaiseLocalEvent(ent.Owner, containerEv);

        if (containerEv.Cancelled)
        {
            if (_net.IsServer && containerEv.Reason != null)
                _popup.PopupEntity(Loc.GetString(containerEv.Reason), ent.Owner);

            args.Cancel();
            return;
        }

        var moduleEv = new ModuleRemovingAttemptEvent(ent.Owner);
        RaiseLocalEvent(args.EntityUid, moduleEv);

        if (moduleEv.Cancelled)
        {
            if (_net.IsServer && moduleEv.Reason != null)
                _popup.PopupEntity(Loc.GetString(moduleEv.Reason), ent.Owner);

            args.Cancel();
            return;
        }
    }

    private void OnContainerInserted(Entity<ModuleContainerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ModuleContainerId)
            return;

        var contained = EnsureComp<ModuleContainedComponent>(args.Entity);
        contained.Container = ent.Owner;
        Dirty(args.Entity, contained);

        var containerEv = new ModuleContainerModuleAddedEvent(args.Entity);
        RaiseLocalEvent(ent.Owner, containerEv);

        var moduleEv = new ModuleAddedContainerEvent(ent.Owner);
        RaiseLocalEvent(args.Entity, moduleEv);

        _item.VisualsChanged(ent.Owner);
    }

    private void OnContainerRemoved(Entity<ModuleContainerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ModuleContainerId)
            return;

        var containerEv = new ModuleContainerModuleRemovedEvent(args.Entity);
        RaiseLocalEvent(ent.Owner, containerEv);

        var moduleEv = new ModuleRemovedContainerEvent(ent.Owner);
        RaiseLocalEvent(args.Entity, moduleEv);

        RemComp<ModuleContainedComponent>(args.Entity);

        _item.VisualsChanged(ent.Owner);
    }

    private void OnContainerGetModSuitUiEntry(Entity<ModuleContainerComponent> ent, ref ModSuitGetUiEntriesEvent args)
    {
        // add a blank one so we override incase there is no more modules
        foreach (var entry in args.Entries)
        {
            if (entry is ModSuitModuleBuiEntry)
                return;
        }

        args.Entries.Add(new ModSuitModuleBuiEntry());
        RelayToModules(ent, ref args);
    }

    private ModSuitBaseModuleBuiEntry GetDefaultModSuitBui(Entity<ModuleContainedComponent> ent)
    {
        return new ModSuitBaseModuleBuiEntry(CompOrNull<ModSuitModuleComplexityComponent>(ent.Owner)?.Complexity);
    }

    private void OnModuleGetModSuitUiEntry(Entity<ModuleContainedComponent> ent, ref ModuleRelayedEvent<ModSuitGetUiEntriesEvent> args)
    {
        ModSuitModuleBuiEntry? foundEntry = null;

        foreach (var entry in args.Args.Entries)
        {
            if (entry is not ModSuitModuleBuiEntry moduleEntry)
                continue;

            foundEntry = moduleEntry;
            break;
        }

        if (foundEntry == null)
            return; // should never happen as the modsuit adds a blank one to reset any lingering modules

        var ev = new ModSuitGetModuleUiEvent();
        RaiseLocalEvent(ent.Owner, ev);

        var netEntity = GetNetEntity(ent.Owner);
        var buiEntry = GetDefaultModSuitBui(ent);

        foreach (var entry in ev.BuiEntries)
        {
            if (entry.Priority < buiEntry.Priority)
                continue;

            buiEntry = entry;
        }

        var newModules = new KeyValuePair<NetEntity, ModSuitBaseModuleBuiEntry>[foundEntry.Modules.Length + 1];
        Array.Copy(foundEntry.Modules, newModules, foundEntry.Modules.Length);
        newModules[^1] = KeyValuePair.Create(netEntity, buiEntry);
        foundEntry.Modules = newModules;
    }

    private void OnModSuitEjectButton(Entity<ModuleContainerComponent> ent, ref ModSuitEjectButtonMessage args)
    {
        if (!TryGetEntity(args.Module, out var module) || GetContainer(module.Value) != ent.Owner)
            return;

        if (!CanModifyModules(ent.Owner, args.Actor))
            return;

        if (!_container.Remove(module.Value, ent.Comp.Modules))
            return;

        _hands.TryPickupAnyHand(args.Actor, module.Value);
    }

    [PublicAPI]
    public IEnumerable<EntityUid> GetModules(Entity<ModuleContainerComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            yield break;

        foreach (var module in ent.Comp.Modules.ContainedEntities)
            yield return module;
    }

    [PublicAPI]
    public void RaiseEventToModules<T>(Entity<ModuleContainerComponent?> ent, T ev) where T : notnull
    {
        foreach (var module in GetModules(ent))
            RaiseLocalEvent(module, ev);
    }

    public void RaiseEventToModules<T>(Entity<ModuleContainerComponent?> ent, ref T ev) where T : notnull
    {
        foreach (var module in GetModules(ent))
            RaiseLocalEvent(module, ref ev);
    }

    [PublicAPI]
    public void RaiseEventToContainer<T>(Entity<ModuleContainedComponent?> ent, T ev) where T : notnull
    {
        if (TryGetContainer(ent, out var container))
            RaiseLocalEvent(container.Value, ev);
    }

    [PublicAPI]
    public void RaiseEventToContainer<T>(Entity<ModuleContainedComponent?> ent, ref T ev) where T : notnull
    {
        if (TryGetContainer(ent, out var container))
            RaiseLocalEvent(container.Value, ref ev);
    }

    [PublicAPI]
    public void UpdateUis(EntityUid uid)
    {
        _modSuit.UpdateUI(uid);
    }

    [PublicAPI]
    public bool TryGetContainer(Entity<ModuleContainedComponent?> ent, [NotNullWhen(true)] out EntityUid? container)
    {
        container = null;

        if (!Resolve(ent.Owner, ref ent.Comp))
            return false;

        container = ent.Comp.Container;
        return container != null;
    }

    [PublicAPI]
    public EntityUid? GetContainer(Entity<ModuleContainedComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return null;

        return ent.Comp.Container;
    }

    [PublicAPI]
    public bool TryGetUser(Entity<ModuleContainedComponent?> ent, [NotNullWhen(true)] out EntityUid? user)
    {
        user = null;

        if (!Resolve(ent.Owner, ref ent.Comp))
            return false;

        var ev = new ModuleGetUserEvent();
        RaiseLocalEvent(ent.Comp.Container, ev);

        user = ev.User;
        return ev.User != null;
    }
}
