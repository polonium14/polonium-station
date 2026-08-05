// SPDX-FileCopyrightText: 2024 John Space <bigdumb421@gmail.com>
// SPDX-FileCopyrightText: 2024 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 misghast <51974455+misterghast@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
// SPDX-FileCopyrightText: 2026 Damian Zieliński <zientasek.pl@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Robust.Shared.Configuration;

namespace Content.Shared._Goobstation.CCVar;

[CVarDefs]
public sealed partial class GoobCVars
{
    #region Station Events

    /// <summary>
    /// Makes station event schedulers behave as if time is sped up by this much.
    /// Supported for secret+.
    /// </summary>
    public static readonly CVarDef<float> StationEventSpeedup =
        CVarDef.Create("stationevents.debug_speedup", 1f, CVar.SERVERONLY);

    /// <summary>
    /// Makes station event schedulers consider the server to have this many extra living players.
    /// Supported for secret+.
    /// </summary>
    public static readonly CVarDef<int> StationEventPlayerBias =
        CVarDef.Create("stationevents.debug_player_bias", 0, CVar.SERVERONLY);

    /// <summary>
    /// Also used by secret+.
    /// </summary>
    public static readonly CVarDef<float> MinimumTimeUntilFirstEvent =
        CVarDef.Create("gamedirector.minimumtimeuntilfirstevent", 300f, CVar.SERVERONLY);

    /// <summary>
    /// Used by secret+.
    /// </summary>
    public static readonly CVarDef<float> RoundstartChaosScoreMultiplier =
        CVarDef.Create("gamedirector.roundstart_chaos_score_multiplier", 1f, CVar.SERVERONLY);

    #endregion

    #region Medical

    /// <summary>
    /// A multiplier for bloodloss damage and heal.
    /// </summary>
    public static readonly CVarDef<float> BleedMultiplier =
        CVarDef.Create("medical.bloodloss_multiplier", 4.0f, CVar.SERVER);

    #endregion

    #region Chat

    /// <summary>
    /// Whether or not to log actions (popups) in the chat.
    /// </summary>
    public static readonly CVarDef<bool> LogInChat =
        CVarDef.Create("chat.log_in_chat", true, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);

    #endregion
}
