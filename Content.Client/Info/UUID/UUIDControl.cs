using Content.Shared.CCVar;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client.Info.UUID;

// ReSharper disable once InconsistentNaming
public sealed class UUIDControl : BoxContainer
{
    private UUIDWindow? _uuidWindow;

    public UUIDControl()
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();

        var buttons = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
        };
        AddChild(buttons);

        if (!cfg.GetCVar(CCVars.ShowUUIDButton))
            return;

        var uuidButton = new Button
        {
            // ReSharper disable once StringLiteralTypo
            Text = "Pokaż UUID",
            TextAlign = Label.AlignMode.Center,
            ClipText = false,
            VerticalExpand = false,
            Margin = new Thickness(3, 3, 3, 3),
        };

        uuidButton.OnPressed += _ => ToggleUUIDWindow();

        buttons.AddChild(uuidButton);
    }

    // ReSharper disable once InconsistentNaming
    private void ToggleUUIDWindow()
    {
        if (_uuidWindow == null)
        {
            _uuidWindow = new UUIDWindow();
            _uuidWindow.OnClose += () => _uuidWindow = null;
        }

        if (_uuidWindow.IsOpen)
        {
            _uuidWindow.Close();
        }
        else
        {
            _uuidWindow.OpenCentered();
        }
    }
}
