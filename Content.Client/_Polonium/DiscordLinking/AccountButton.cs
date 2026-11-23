using Content.Shared.CCVar;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Localization;

namespace Content.Client._Polonium.DiscordLinking;

/// <summary>
/// Lobby button that opens the account window with the UUID and, when it is on, the Discord link.
/// </summary>
public sealed partial class AccountButton : Button
{
    [Dependency] private IConfigurationManager _cfg = default!;

    public AccountButton()
    {
        IoCManager.InjectDependencies(this);

        Visible = false;

        OnPressed += _ => UserInterfaceManager.GetUIController<DiscordLinkUIController>().ToggleWindow();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _cfg.OnValueChanged(CCVars.ShowUUIDButton, OnCVarChanged);
        _cfg.OnValueChanged(CCVars.DiscordLinkEnabled, OnCVarChanged, invokeImmediately: true);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _cfg.UnsubValueChanged(CCVars.ShowUUIDButton, OnCVarChanged);
        _cfg.UnsubValueChanged(CCVars.DiscordLinkEnabled, OnCVarChanged);
    }

    // The same window shows the UUID and links Discord, so the button is there when either of them is on.
    private void OnCVarChanged(bool _)
    {
        var discordLink = _cfg.GetCVar(CCVars.DiscordLinkEnabled);
        Visible = discordLink || _cfg.GetCVar(CCVars.ShowUUIDButton);
        Text = Loc.GetString(discordLink ? "discord-link-lobby-button" : "server-info-uuid-button");
    }
}
