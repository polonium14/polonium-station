using Content.Client.Eui;
using Content.Shared._Polonium.RoundEnd;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Polonium.RoundEnd;

[UsedImplicitly]
public sealed class AdminRoundEndTextEui : BaseEui
{
    private readonly AdminRoundEndTextWindow _window;

    public AdminRoundEndTextEui()
    {
        _window = new AdminRoundEndTextWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnSave += text => SendMessage(new AdminRoundEndTextEuiMsg.Save(text));
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is AdminRoundEndTextEuiState textState)
            _window.SetState(textState);
    }
}
