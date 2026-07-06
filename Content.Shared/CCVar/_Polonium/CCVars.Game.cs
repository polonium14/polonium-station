// SPDX-FileCopyrightText: 2024 nikitosych <admin@ss14.pl>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
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
