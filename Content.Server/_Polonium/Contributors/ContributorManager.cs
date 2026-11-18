using Content.Server._Polonium.PlayerLists;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._Polonium.Contributors;

/// <summary>
/// Keeps the list of contributors: players whose game account is linked to a Discord account with the contributor role.
/// Their names in chat get the color of the role.
/// </summary>
public sealed partial class ContributorManager : PlayerListManager
{
    private const int MinRefreshMinutes = 1;

    protected override string Name => "contributors";

    protected override CVarDef<bool> EnabledCVar => CCVars.ContributorsEnabled;

    protected override CVarDef<string> ApiUrlCVar => CCVars.ContributorsApiUrl;

    // The same bot as for Discord links, so the same token.
    protected override CVarDef<string> ApiTokenCVar => CCVars.DiscordLinkApiToken;

    protected override CVarDef<int> FetchCooldownCVar => CCVars.ContributorsFetchCooldownSeconds;

    protected override CVarDef<int> RetryDelayCVar => CCVars.ContributorsRetryDelaySeconds;

    protected override TimeSpan? RefreshInterval =>
        TimeSpan.FromMinutes(Math.Max(Cfg.GetCVar(CCVars.ContributorsRefreshMinutes), MinRefreshMinutes));

    protected override string? NameColor => BotColor;
}
