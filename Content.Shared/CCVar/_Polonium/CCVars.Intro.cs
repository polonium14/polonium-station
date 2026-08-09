// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{

// /$$$$$$$$ /$$$$$$  /$$$$$$$   /$$$$$$
// |__  $$__//$$__  $$| $$__  $$ /$$__  $$
//    | $$  | $$  \ $$| $$  \ $$| $$  \ $$
//    | $$  | $$  | $$| $$  | $$| $$  | $$
//    | $$  | $$  | $$| $$  | $$| $$  | $$
//    | $$  | $$  | $$| $$  | $$| $$  | $$
//    | $$  |  $$$$$$/| $$$$$$$/|  $$$$$$/
//    |__/   \______/ |_______/  \______/

    /// <summary>
    ///  Enables or disables the introduction system for new players.
    /// </summary>
    public static readonly CVarDef<bool> IntroEnabled =
        CVarDef.Create("intro.enabled", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///   Maximum playtime (in minutes) a player can have before being ineligible for the newbie introduction sequence.
    /// </summary>
    public static readonly CVarDef<int> IntroMaxPlaytime =
        CVarDef.Create("intro.max_playtime", int.MaxValue, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///   Enables or disables the introduction system in debug mode.
    /// </summary>
    public static readonly CVarDef<bool> IntroInDebug = CVarDef.Create("intro.in_debug", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<string> IntroSolitaryServerConnectionString =
        CVarDef.Create("intro.solitary_server_con_string", string.Empty, CVar.SERVER | CVar.REPLICATED);
}
