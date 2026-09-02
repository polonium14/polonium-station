using Content.Client._Polonium.VendingMachines.UI;
using Content.Client.VendingMachines;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Emag.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.VendingMachines;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using System.Linq;
using Content.Shared._Polonium.VendingMachines;
using Content.Shared.VendingMachines.Components;

namespace Content.Client._Polonium.VendingMachines;

[UsedImplicitly]
public sealed partial class VendingMachineKeypadBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IPlayerManager _playerManager = default!;

    [ViewVariables]
    private VendingMachineKeypadMenu? _menu;

    [ViewVariables]
    private List<VendingMachineInventoryEntry> _cachedInventory = new();

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindowCenteredLeft<VendingMachineKeypadMenu>();
        _menu.VendingMachineOwner = Owner;
        _menu.User = _playerManager.LocalSession?.AttachedEntity;
        _menu.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _menu.OnCodeEntered += OnCodeEntered;
        _menu.OnAudioPlayed += OnAudioPlayed;
        Refresh();
    }

    public void Refresh()
    {
        var system = EntMan.System<VendingMachineSystem>();
        _cachedInventory = system.GetAllInventory(Owner);

        _menu?.Populate(_cachedInventory);
    }

    public void UpdateAmounts()
    {
        var system = EntMan.System<VendingMachineSystem>();
        _cachedInventory = system.GetAllInventory(Owner);
        _menu?.UpdateAmounts(_cachedInventory);
    }

    private void OnAudioPlayed(VendingMachineKeypadSound type, float pitch)
    {
        SendMessage(new VendingMachineKeypadAudioMessage(type, pitch));
    }

    private VendingMachineKeypadFeedback OnCodeEntered(int slotIndex)
    {
        var selectedItem = _cachedInventory.ElementAtOrDefault(slotIndex);

        if (selectedItem == null)
            return VendingMachineKeypadFeedback.Invalid;

        // live machine state, mirroring TryEjectVendorItem guard. A mid-eject,
        // broken, or unpowered machine silently swallows the code instead of
        // playing success feedback for a vend the server will drop.
        if (EntMan.TryGetComponent(Owner, out VendingMachineEjectComponent? eject) && eject.Ejecting)
            return VendingMachineKeypadFeedback.None;

        if (EntMan.TryGetComponent(Owner, out VendingMachineComponent? vend) && vend.Broken)
            return VendingMachineKeypadFeedback.None;

        if (!EntMan.System<SharedPowerReceiverSystem>().IsPowered(Owner))
            return VendingMachineKeypadFeedback.None;

        // check access, mirroring SharedVendingMachineSystem.IsAuthorized:
        // no reader means public, and an emag bypasses the reader.
        if (_playerManager.LocalSession?.AttachedEntity is { } player &&
            EntMan.TryGetComponent(Owner, out AccessReaderComponent? accessReader) &&
            !EntMan.System<AccessReaderSystem>().IsAllowed(player, Owner, accessReader) &&
            !EntMan.HasComponent<EmaggedComponent>(Owner))
        {
            return VendingMachineKeypadFeedback.Denied;
        }

        // optimistic
        _menu?.PlayVendAnimation(slotIndex);

        SendPredictedMessage(new VendingMachineEjectMessage(selectedItem.Type, selectedItem.ID));
        return VendingMachineKeypadFeedback.Success;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (_menu == null)
            return;

        _menu.OnCodeEntered -= OnCodeEntered;
        _menu.OnAudioPlayed -= OnAudioPlayed;
        _menu.OnClose -= Close;
        _menu.Close();
    }
}
