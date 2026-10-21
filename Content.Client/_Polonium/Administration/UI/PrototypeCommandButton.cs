using Content.Client.Administration.UI.CustomControls;

namespace Content.Client._Polonium.Administration.UI;

/// <summary>
/// Command button that first asks the admin to pick a prototype of <see cref="PrototypeKind"/>,
/// then runs <see cref="CommandFormat"/> with the chosen ID in place of <c>{0}</c>.
/// <see cref="CommandButton.Command"/> is only used for the permission check.
/// </summary>
public sealed class PrototypeCommandButton : CommandButton
{
    public string PrototypeKind { get; set; } = string.Empty;
    public string CommandFormat { get; set; } = string.Empty;

    private PrototypeCommandWindow? _window;

    protected override void Execute(ButtonEventArgs obj)
    {
        if (_window is { Disposed: false })
        {
            _window.MoveToFront();
            return;
        }

        _window = new PrototypeCommandWindow(Text ?? string.Empty, PrototypeKind, CommandFormat);
        _window.OpenCentered();
    }
}
