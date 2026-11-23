using System.Numerics;
using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Chemistry.UI;

[UsedImplicitly]
public sealed class BarDispenserBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private BarDispenserWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BarDispenserWindow>();
        _window.SetEntity(Owner);

        // The body is as tall as its rows of keys, which the window learns only now, after the engine has centred it.
        if (!EntMan.System<UserInterfaceSystem>().TryGetPosition(Owner, UiKey, out _))
        {
            _window.Measure(Vector2Helpers.Infinity);
            _window.RecenterWindow(new Vector2(0.5f, 0.5f));
        }

        _window.OnDispense += location => SendMessage(new ReagentDispenserDispenseReagentMessage(location));
        _window.OnEject += location => SendMessage(new ReagentDispenserEjectContainerMessage(location));
        _window.OnAmountSelected += amount => SendMessage(new ReagentDispenserSetDispenseAmountMessage(amount));
        _window.OnClear += () => SendMessage(new ReagentDispenserClearContainerSolutionMessage());
        _window.OnVesselPressed += PressVessel;
    }

    private void PressVessel(bool insert)
    {
        var press = new ItemSlotButtonPressedEvent(SharedReagentDispenser.OutputSlotName, tryEject: !insert, tryInsert: insert);

        // The slot leaves its eject sound to the user's client and plays its insert sound for everyone, the user included.
        if (insert)
            SendMessage(press);
        else
            SendPredictedMessage(press);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ReagentDispenserBoundUserInterfaceState dispenser)
            _window?.UpdateState(dispenser);
    }
}
