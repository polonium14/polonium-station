using Content.Shared._Polonium.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Kitchen.EntitySystems;
using Content.Shared.Power.EntitySystems;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Kitchen.UI;

[UsedImplicitly]
public sealed partial class MicrowaveDeviceBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private MicrowaveSystem _microwave = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;

    [ViewVariables]
    private MicrowaveDeviceWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MicrowaveDeviceWindow>();
        _window.StateSource = GetState;
        _window.OnStart += () => SendPredictedMessage(new MicrowaveStartCookMessage());
        _window.OnEjectAll += () => SendPredictedMessage(new MicrowaveEjectMessage());
        _window.OnEject += uid => SendPredictedMessage(new MicrowaveEjectSolidIndexedMessage(EntMan.GetNetEntity(uid)));
        _window.OnTimeSelected += index =>
            SendPredictedMessage(new MicrowaveSelectCookTimeMessage(index, (uint) (index * MicrowaveDeviceWindow.TimeStep)));
    }

    private MicrowaveDeviceState GetState()
    {
        if (!EntMan.TryGetComponent(Owner, out MicrowaveComponent? microwave))
            return MicrowaveDeviceState.Off;

        EntMan.TryGetComponent(Owner, out ActiveMicrowaveComponent? active);
        EntMan.TryGetComponent(Owner, out MicrowaveReactionVisualsComponent? visuals);
        return new MicrowaveDeviceState(
            _microwave.GetMicrowaveContents((Owner, microwave)),
            microwave.Capacity,
            microwave.CurrentCookTimeButtonIndex,
            active != null,
            active?.CookTimeEnd ?? TimeSpan.Zero,
            microwave.CookTimeMultiplier,
            _power.IsPowered(Owner),
            microwave.Broken,
            active?.Malfunctioning ?? false,
            new MicrowaveReactions(microwave.MalfunctionWhenCookedWhitelist, microwave.BurnWhenCookedWhitelist, visuals?.Sparking, microwave.FailureResult));
    }
}
