using Content.Client.UserInterface.Fragments;
using Content.Shared._Polonium.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.CartridgeLoader.Cartridges;

public sealed partial class LogiAppUi : UIFragment
{
    private LogiAppUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new LogiAppUiFragment();
        _fragment.OnRefresh += () => SendMessage(LogiAppUiAction.Refresh, userInterface);
        _fragment.OnClearFinished += () => SendMessage(LogiAppUiAction.ClearFinished, userInterface);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not LogiAppUiState logiAppState)
            return;

        _fragment?.UpdateState(logiAppState.Parcels);
    }

    private static void SendMessage(LogiAppUiAction action, BoundUserInterface userInterface)
    {
        var message = new CartridgeUiMessage(new LogiAppUiMessageEvent(action));
        userInterface.SendMessage(message);
    }
}
