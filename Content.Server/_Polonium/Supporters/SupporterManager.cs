using Content.Server._Polonium.PlayerLists;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._Polonium.Supporters;

/// <summary>
/// Keeps the list of active supporters with a linked game account.
/// Their names in chat get the color from <see cref="CCVars.SupportersNameColor"/>.
/// </summary>
public sealed partial class SupporterManager : PlayerListManager
{
    protected override string Name => "supporters";

    protected override CVarDef<bool> EnabledCVar => CCVars.SupportersEnabled;

    protected override CVarDef<string> ApiUrlCVar => CCVars.SupportersApiUrl;

    protected override CVarDef<string> ApiTokenCVar => CCVars.SupportersApiToken;

    protected override CVarDef<int> FetchCooldownCVar => CCVars.SupportersFetchCooldownSeconds;

    protected override CVarDef<int> RetryDelayCVar => CCVars.SupportersRetryDelaySeconds;

    protected override TimeSpan? RefreshInterval =>
        Cfg.GetCVar(CCVars.SupportersRefreshMinutes) is var minutes and > 0 ? TimeSpan.FromMinutes(minutes) : null;

    protected override string? NameColor => Cfg.GetCVar(CCVars.SupportersNameColor);
}
