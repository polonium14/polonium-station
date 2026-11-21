using Content.Shared._Polonium.Kitchen;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Kitchen.UI;

[UsedImplicitly]
public sealed class GrinderDeviceBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private GrinderDeviceWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GrinderDeviceWindow>();
        _window.SetEntity(Owner);
        _window.OnStart += program => SendPredictedMessage(new ReagentGrinderStartMessage(program));
        _window.OnAutoSelected += mode => SendPredictedMessage(new GrinderSetAutoModeMessage(mode));
        _window.OnEjectAll += () => SendPredictedMessage(new ReagentGrinderEjectChamberAllMessage());
        _window.OnEject += uid => SendPredictedMessage(new ReagentGrinderEjectChamberContentMessage(EntMan.GetNetEntity(uid)));
        _window.OnVesselPressed += insert => SendPredictedMessage(new ItemSlotButtonPressedEvent(ReagentGrinderComponent.BeakerSlotId, tryEject: !insert, tryInsert: insert));
    }
}
