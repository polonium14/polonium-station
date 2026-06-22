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
}
