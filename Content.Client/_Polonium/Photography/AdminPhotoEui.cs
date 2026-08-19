using Content.Client.Eui;
using Content.Shared._Polonium.Photography;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Polonium.Photography;

/// <summary>Client half of the admin photo viewer. Named to match the server EUI.</summary>
[UsedImplicitly]
public sealed class AdminPhotoEui : BaseEui
{
    private readonly AdminPhotoWindow _window = new();

    public AdminPhotoEui()
    {
        _window.OnSelect += id => SendMessage(new AdminPhotoSelectMessage(id));
        _window.OnDelete += id => SendMessage(new AdminPhotoDeleteMessage(id));
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is AdminPhotoEuiState cast)
            _window.Populate(cast);
    }
}
