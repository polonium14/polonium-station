// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._RMC14.CCVar;

[CVarDefs]
public sealed partial class RMCCVars : CVars
{
    public static readonly CVarDef<bool> RMCGunPrediction =
        CVarDef.Create("rmc.gun_prediction", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> RMCGunPredictionPreventCollision =
        CVarDef.Create("rmc.gun_prediction_prevent_collision", false, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> RMCGunPredictionLogHits =
        CVarDef.Create("rmc.gun_prediction_log_hits", false, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> RMCGunPredictionCoordinateDeviation =
        CVarDef.Create("rmc.gun_prediction_coordinate_deviation", 3f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> RMCGunPredictionLowestCoordinateDeviation =
        CVarDef.Create("rmc.gun_prediction_lowest_coordinate_deviation", 3f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> RMCGunPredictionAabbEnlargement =
        CVarDef.Create("rmc.gun_prediction_aabb_enlargement", 1.5f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> RMCLagCompensationMilliseconds =
        CVarDef.Create("rmc.lag_compensation_milliseconds", 750, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> RMCLagCompensationMarginTiles =
        CVarDef.Create("rmc.lag_compensation_margin_tiles", 0.25f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> RMCXenoDamageDealtMultiplier =
        CVarDef.Create("rmc.xeno_damage_dealt_multiplier", 1f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> RMCXenoDamageReceivedMultiplier =
        CVarDef.Create("rmc.xeno_damage_received_multiplier", 1f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> RMCXenoSpeedMultiplier =
        CVarDef.Create("rmc.xeno_speed_multiplier", 1f, CVar.REPLICATED | CVar.SERVER);

    // 999 = always accumulate during a normal round
    public static readonly CVarDef<float> RMCEvolutionPointsAccumulateBeforeMinutes =
        CVarDef.Create("rmc.evolution_points_accumulate_before_minutes", 999f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> RMCEvolutionPointsRate =
        CVarDef.Create("rmc.evolution_points_rate", 0.2f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<int> RMCPlaytimeBronzeMedalTimeHours =
        CVarDef.Create("rmc.playtime_bronze_medal_time_hours", 10, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<int> RMCPlaytimeSilverMedalTimeHours =
        CVarDef.Create("rmc.playtime_silver_medal_time_hours", 25, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<int> RMCPlaytimeGoldMedalTimeHours =
        CVarDef.Create("rmc.playtime_gold_medal_time_hours", 70, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<int> RMCPlaytimePlatinumMedalTimeHours =
        CVarDef.Create("rmc.playtime_platinum_medal_time_hours", 175, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<int> RMCPlaytimeRubyMedalTimeHours =
        CVarDef.Create("rmc.playtime_ruby_medal_time_hours", 350, CVar.REPLICATED | CVar.SERVER);
}
