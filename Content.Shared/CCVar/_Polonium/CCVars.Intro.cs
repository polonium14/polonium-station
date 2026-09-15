// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

/// <summary>
/// Role of this server in the training flow. Int backing because cvars demand it.
/// </summary>
public enum IntroMode : int
{
    /// <summary>No lobby offer, no tutorial button. Guidebook may auto-open for new players.</summary>
    Off = 0,

    /// <summary>Regular station. Offer unfinished players a hop to the training box.</summary>
    Main = 1,

    /// <summary>This box is the training server. Skip the lobby and start the map tutorial.</summary>
    Tutorial = 2,
}

public sealed partial class CCVars
{

// ░░░░░░░░░░░░░░░██████╗░██╗░░░██╗
// ░░░░░░░░░░░░░░░██╔══██╗╚██╗░██╔╝
// ░░░░░░░░░░░░░░░██████╦╝░╚████╔╝░
// ░░░░░░░░░░░░░░░██╔══██╗░░╚██╔╝░░
// ░░░░░░░░░░░░░░░██████╦╝░░░██║░░░
// ░░░░░░░░░░░░░░░╚═════╝░░░░╚═╝░░░

// ██████╗░░█████╗░██╗░░░░░░█████╗░
// ██╔══██╗██╔══██╗██║░░░░░██╔══██╗
// ██████╔╝██║░░██║██║░░░░░██║░░██║
// ██╔═══╝░██║░░██║██║░░░░░██║░░██║
// ██║░░░░░╚█████╔╝███████╗╚█████╔╝
// ╚═╝░░░░░░╚════╝░╚══════╝░╚════╝░

// ███╗░░██╗██╗██╗░░░██╗███╗░░░███╗
// ████╗░██║██║██║░░░██║████╗░████║
// ██╔██╗██║██║██║░░░██║██╔████╔██║
// ██║╚████║██║██║░░░██║██║╚██╔╝██║
// ██║░╚███║██║╚██████╔╝██║░╚═╝░██║
// ╚═╝░░╚══╝╚═╝░╚═════╝░╚═╝░░░░░╚═╝

    /// <summary>
    /// Off, Main or Tutorial. Main is the regular station. Tutorial is the training box.
    /// </summary>
    public static readonly CVarDef<IntroMode> IntroServerMode =
        CVarDef.Create("intro.mode", IntroMode.Main, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///   Enables or disables the introduction system in debug mode.
    /// </summary>
    public static readonly CVarDef<bool> IntroInDebug = CVarDef.Create("intro.in_debug", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> SkipLobbyIntroDebug =
        CVarDef.Create("intro.skip_lobby_intro_debug", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Jump the practical tutorial to this room marker on start. Empty is off.
    /// Accepts <c>room5</c>, <c>5</c> or <c>r5</c>. Earlier rooms still apply their
    /// enter/complete actions so doors, access and spawned bits are already in place.
    /// </summary>
    public static readonly CVarDef<string> TutorialDebugStartRoom =
        CVarDef.Create("tutorial.debug_start_room", string.Empty, CVar.SERVER);

    public static readonly CVarDef<string> IntroSolitaryServerConnectionString =
        CVarDef.Create("intro.solitary_server_con_string", string.Empty, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<string> IntroReturnServerConnectionString =
        CVarDef.Create("intro.return_server_con_string", string.Empty, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Player said no to the lobby training offer. Do not ask again on this client.</summary>
    public static readonly CVarDef<bool> IntroDeclined =
        CVarDef.Create("intro.declined", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Client-side copy of a finished run. The db is the source of truth, this just covers a
    /// hop back to a main box that does not share that db.
    /// </summary>
    public static readonly CVarDef<bool> IntroCompleted =
        CVarDef.Create("intro.completed", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
