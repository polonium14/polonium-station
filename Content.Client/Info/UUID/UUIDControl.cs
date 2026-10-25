using Content.Shared.CCVar;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Localization;

namespace Content.Client.Info.UUID;

// ReSharper disable once InconsistentNaming
public sealed class UUIDControl : Button
{
    private UUIDWindow? _uuidWindow;
    private readonly IConfigurationManager _cfg;

    public UUIDControl()
    {
        _cfg = IoCManager.Resolve<IConfigurationManager>();

        Text = Loc.GetString("server-info-uuid-button");
        Visible = false;

        OnPressed += _ => ToggleUUIDWindow();
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
