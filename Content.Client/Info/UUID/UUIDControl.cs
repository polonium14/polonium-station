using Content.Shared.CCVar;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client.Info.UUID;

// ReSharper disable once InconsistentNaming
public sealed class UUIDControl : BoxContainer
{
    private UUIDWindow? _uuidWindow;
    private readonly IConfigurationManager _cfg;
    private readonly Button _uuidButton;

    public UUIDControl()
    {
        _cfg = IoCManager.Resolve<IConfigurationManager>();

        var buttons = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
        };
        AddChild(buttons);

        _uuidButton = new Button
        {
            // ReSharper disable once StringLiteralTypo
            Text = "Pokaż UUID",
            TextAlign = Label.AlignMode.Center,
            ClipText = false,
            VerticalExpand = false,
            Margin = new Thickness(3, 3, 3, 3),
            Visible = false,
        };

        _uuidButton.OnPressed += _ => ToggleUUIDWindow();
        buttons.AddChild(_uuidButton);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _cfg.OnValueChanged(CCVars.ShowUUIDButton, OnShowUUIDChanged, invokeImmediately: true);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _cfg.UnsubValueChanged(CCVars.ShowUUIDButton, OnShowUUIDChanged);
    }

    private void OnShowUUIDChanged(bool show)
    {
        _uuidButton.Visible = show;
        Visible = show;
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
