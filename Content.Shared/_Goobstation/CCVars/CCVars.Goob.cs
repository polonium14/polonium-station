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

    #region Mechs

    /// <summary>
    ///     Whether or not players can use mech guns outside of mechs.
    /// </summary>
    public static readonly CVarDef<bool> MechGunOutsideMech =
        CVarDef.Create("mech.gun_outside_mech", true, CVar.SERVER | CVar.REPLICATED);

    #endregion

    #region Station Events

    /// <summary>
    /// Makes station event schedulers behave as if time is sped up by this much.
    /// Supported for secret, secret+, and game director.
    /// </summary>
    public static readonly CVarDef<float> StationEventSpeedup =
        CVarDef.Create("stationevents.debug_speedup", 1f, CVar.SERVERONLY);

    /// <summary>
    /// Makes station event schedulers consider the server to have this many extra living players.
    /// Supported for secret+ and game director.
    /// </summary>
    public static readonly CVarDef<int> StationEventPlayerBias =
        CVarDef.Create("stationevents.debug_player_bias", 0, CVar.SERVERONLY);

    #region Game Director

    // also used by secret+
    public static readonly CVarDef<float> MinimumTimeUntilFirstEvent =
        CVarDef.Create("gamedirector.minimumtimeuntilfirstevent", 300f, CVar.SERVERONLY);

    // used by secret+
    public static readonly CVarDef<float> RoundstartChaosScoreMultiplier =
        CVarDef.Create("gamedirector.roundstart_chaos_score_multiplier", 1f, CVar.SERVERONLY);

    #endregion

    #endregion

}
