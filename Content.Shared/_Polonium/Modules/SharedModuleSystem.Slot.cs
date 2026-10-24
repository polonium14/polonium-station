using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared._Polonium.Modules.Components;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Wires;

namespace Content.Shared._Polonium.Modules;

public partial class SharedModuleSystem
{
    private void InitializeSlot()
    {
        SubscribeLocalEvent<ModuleContainerAddOnInteractComponent, InteractUsingEvent>(OnAddOnInteract, before: [typeof(SharedStorageSystem)]);
        SubscribeLocalEvent<ModuleContainerComponent, ModuleInsertDoAfterEvent>(OnInsertDoAfter);
        SubscribeLocalEvent<ModuleContainerComponent, ModuleRemoveDoAfterEvent>(OnRemoveDoAfter);
    }

    private void OnAddOnInteract(Entity<ModuleContainerAddOnInteractComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ModuleContainerComponent>(ent.Owner, out var moduleContainer))
            return;

        if (!_whitelist.CheckBoth(args.Used, moduleContainer.Blacklist, moduleContainer.Whitelist))
            return;

        args.Handled = true;

        if (!CanModifyModules(ent.Owner, args.User) || !_container.CanInsert(args.Used, moduleContainer.Modules))
            return;

        StartModuleDoAfter(args.User, ent.Owner, args.Used, moduleContainer.InsertDelay, new ModuleInsertDoAfterEvent());
    }

    private void OnInsertDoAfter(Entity<ModuleContainerComponent> ent, ref ModuleInsertDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } module)
            return;

        args.Handled = true;

        if (CanModifyModules(ent.Owner, args.User))
            _container.Insert(module, ent.Comp.Modules);
    }

    private void OnRemoveDoAfter(Entity<ModuleContainerComponent> ent, ref ModuleRemoveDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } module)
            return;

        args.Handled = true;

        if (GetContainer(module) != ent.Owner || !CanModifyModules(ent.Owner, args.User))
            return;

        if (_container.Remove(module, ent.Comp.Modules))
            _hands.TryPickupAnyHand(args.User, module);
    }

    private void StartModuleDoAfter(EntityUid user, EntityUid container, EntityUid module, TimeSpan delay, DoAfterEvent ev)
    {
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, delay, ev, container, container, module)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        });
    }

    /// <summary>
    /// Whether a player can add or remove modules right now. Containers that require an open wire panel
    /// only allow it while the panel is open, and the popup tells the user why it failed.
    /// </summary>
    public bool CanModifyModules(EntityUid container, EntityUid user)
    {
        if (!HasComp<ModuleContainerRequireWirePanelComponent>(container) ||
            !TryComp<WiresPanelComponent>(container, out var wiresPanel) ||
            wiresPanel.Open)
            return true;

        _popup.PopupEntity(Loc.GetString("module-container-panel-closed", ("container", container)), container, user);
        return false;
    }
}
