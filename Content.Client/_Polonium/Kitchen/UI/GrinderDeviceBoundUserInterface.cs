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
        _window.OnAutoSelected += SelectAutoMode;
        _window.OnEjectAll += () => SendPredictedMessage(new ReagentGrinderEjectChamberAllMessage());
        _window.OnEject += uid => SendPredictedMessage(new ReagentGrinderEjectChamberContentMessage(EntMan.GetNetEntity(uid)));
        _window.OnVesselPressed += () => SendPredictedMessage(new ItemSlotButtonPressedEvent(ReagentGrinderComponent.BeakerSlotId));
    }

    /// <summary>
    /// The game only knows how to step to the next automatic mode, so the knob is turned one step at a time until it shows the wanted one.
    /// </summary>
    private void SelectAutoMode(GrinderAutoMode mode)
    {
        if (!EntMan.TryGetComponent(Owner, out ReagentGrinderComponent? grinder))
            return;

        var modes = Enum.GetValues<GrinderAutoMode>().Length;
        var steps = ((int) mode - (int) grinder.AutoMode + modes) % modes;
        for (var i = 0; i < steps; i++)
        {
            SendPredictedMessage(new ReagentGrinderToggleAutoModeMessage());
        }
    }
}
