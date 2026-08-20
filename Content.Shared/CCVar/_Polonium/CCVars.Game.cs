// SPDX-FileCopyrightText: 2024 nikitosych <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Period (seconds) over which custom objective-summary submissions are rate limited per player.
    /// </summary>
    public static readonly CVarDef<float> ObjectiveSummaryRateLimitPeriod =
        CVarDef.Create("objectives.summary_rate_limit_period", 2f, CVar.SERVERONLY);

    /// <summary>
    /// How many custom objective-summary submissions a player may make within a single rate limit period.
    /// </summary>
    public static readonly CVarDef<int> ObjectiveSummaryRateLimitCount =
        CVarDef.Create("objectives.summary_rate_limit_count", 5, CVar.SERVERONLY);

    /// <summary>
    /// Whether the round should end when revolutionaries achieve victory.
    /// </summary>
    public static readonly CVarDef<bool> ShouldEndRoundOnRevVictory =
        CVarDef.Create("game.should_end_round_on_rev_victory", true, CVar.SERVERONLY);

    /// <summary>
    /// Whether CentComm calls are automatically declined when no admins.
    /// </summary>
    public static readonly CVarDef<bool> CentCommCallDeclineWhenNoAdmins =
        CVarDef.Create("game.centcomm_call_decline_when_no_admins", false, CVar.SERVERONLY);
}
