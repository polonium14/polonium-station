using Content.Shared._Polonium.DiscordLinking;
using Content.Shared.CCVar;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client._Polonium.DiscordLinking;

/// <summary>
/// The account window with the UUID and the Discord link, opened from the lobby and the escape menu.
/// Opens the linking page in the browser when the server gives one.
/// </summary>
[UsedImplicitly]
public sealed partial class DiscordLinkUIController : UIController, IOnSystemChanged<DiscordLinkSystem>
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IUriOpener _uri = default!;
    [Dependency] private IClipboardManager _clipboard = default!;

    private DiscordLinkSystem? _system;
    private AccountWindow? _window;
    private Button? _escapeButton;
    private string? _openedUrl;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(CCVars.DiscordLinkEnabled, _ => UpdateEscapeButton());
    }

    public void OnSystemLoaded(DiscordLinkSystem system)
    {
        _system = system;
        system.StateChanged += OnStateChanged;
        UpdateEscapeButton();
    }

    public void OnSystemUnloaded(DiscordLinkSystem system)
    {
        system.StateChanged -= OnStateChanged;
        _system = null;
        _openedUrl = null;
        _window?.Close();
        _window = null;
    }

    public void ToggleWindow()
    {
        if (_window == null)
        {
            _window = new AccountWindow();
            _window.OnClose += () => _window = null;

            if (_system != null && _cfg.GetCVar(CCVars.DiscordLinkEnabled))
                _window.DiscordLinkContainer.AddChild(new DiscordLinkPanel(_system, _clipboard));
        }

        if (_window.IsOpen)
            _window.Close();
        else
            _window.OpenCentered();
    }

    /// <summary>
    /// Sets the escape menu button that opens the window. It is hidden once the account is linked,
    /// there is nothing left to do there.
    /// </summary>
    public void SetEscapeButton(Button? button)
    {
        _escapeButton = button;
        UpdateEscapeButton();
    }

    private void OnStateChanged(DiscordLinkStateEvent state)
    {
        UpdateEscapeButton();

        // Opened once for every new page, the window keeps the link for when the browser did not open.
        if (state is { Status: DiscordLinkStatus.Started, Url: { } url } && url != _openedUrl)
        {
            _openedUrl = url;
            _uri.OpenUri(url);
        }
    }

    private void UpdateEscapeButton()
    {
        if (_escapeButton == null)
            return;

        _escapeButton.Visible = _cfg.GetCVar(CCVars.DiscordLinkEnabled)
                                && _system?.State?.Status != DiscordLinkStatus.Linked;
    }
}
