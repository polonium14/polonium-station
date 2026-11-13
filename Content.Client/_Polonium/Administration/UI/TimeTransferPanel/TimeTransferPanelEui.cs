using System.Linq;
using Content.Client.Eui;
using Content.Shared._Polonium.Administration;
using Content.Shared.Eui;

namespace Content.Client._Polonium.Administration.UI.TimeTransferPanel;

public sealed class TimeTransferPanelEui : BaseEui
{
    private readonly TimeTransferPanel _panel = new();

    public TimeTransferPanelEui()
    {
        _panel.OnTransferMessageSend += (playerId, transferList, overwrite) =>
            SendMessage(new TimeTransferEuiMessage(playerId, transferList, overwrite));
        _panel.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _panel.OpenCentered();
    }

    public override void Closed()
    {
        _panel.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not TimeTransferPanelEuiState cast)
            return;

        _panel.UpdateFlag(cast.HasFlag);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not TimeTransferWarningEuiMessage warning)
            return;

        var args = warning.Args.Select(arg => (arg.Key, (object) arg.Value)).ToArray();
        _panel.SetBusy(false);
        _panel.UpdateWarning(Loc.GetString(warning.LocKey, args), warning.WarningColor);
    }
}
