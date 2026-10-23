using Content.Shared.Interaction;
using Content.Shared._Polonium.Modules.Components;
using Content.Shared.Wires;

namespace Content.Shared._Polonium.Modules;

public partial class SharedModuleSystem
{
    private void InitializeSlot()
    {
        SubscribeLocalEvent<ModuleContainerAddOnInteractComponent, InteractUsingEvent>(OnAddOnInteract);
    }

    private void OnAddOnInteract(Entity<ModuleContainerAddOnInteractComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ModuleContainerComponent>(ent.Owner, out var moduleContainer))
            return;

        if (!_whitelist.CheckBoth(args.Used, moduleContainer.Blacklist, moduleContainer.Whitelist))
            return;

        if (!CanModifyModules(ent.Owner, args.User))
        {
            args.Handled = true;
            return;
        }

        args.Handled = _container.Insert(args.Used, moduleContainer.Modules);
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

        _popup.PopupClient(Loc.GetString("module-container-panel-closed", ("container", container)), container, user);
        return false;
    }
}
