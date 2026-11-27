using Content.Shared._Polonium.Disposal;
using Content.Shared.Disposal.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Disposal.UI;

[UsedImplicitly]
public sealed class DisposalDeviceBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private DisposalUnitDeviceWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DisposalUnitDeviceWindow>();
        _window.OpenCenteredRight();
        _window.SetEntity(Owner);
        _window.OnButton += button => SendPredictedMessage(new DisposalUnitUiButtonPressedMessage(button));
        _window.OnMouth += () => SendPredictedMessage(new DisposalDeviceMouthMessage());
        _window.OnRoute += () => SendPredictedMessage(new DisposalTaggerOpenUiMessage());
    }
}

[UsedImplicitly]
public sealed class MailingDeviceBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private MailingUnitDeviceWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MailingUnitDeviceWindow>();
        _window.OpenCenteredRight();
        _window.SetEntity(Owner);
        _window.OnButton += button => SendPredictedMessage(new DisposalUnitUiButtonPressedMessage(button));
        _window.OnMouth += () => SendPredictedMessage(new DisposalDeviceMouthMessage());
        _window.OnTarget += target => SendPredictedMessage(new TargetSelectedMessage(target));
    }
}
