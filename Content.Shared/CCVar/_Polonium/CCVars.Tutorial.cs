// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

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
    /// None, Main or Tutorial. Default is None. Main is the regular station. Tutorial is the training box.
    /// </summary>
    public static readonly CVarDef<string> TutorialMode =
        CVarDef.Create("tutorial.mode", "None", CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Skip the lobby introduction.
    /// </summary>
    public static readonly CVarDef<bool> TutorialSkipLobbyDebug =
        CVarDef.Create("tutorial.skip_lobby_debug", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Jump the practical tutorial to this room marker on start. Empty is off.
    /// Accepts <c>room5</c>, <c>5</c> or <c>r5</c>. Earlier rooms still apply their
    /// enter/complete actions so doors, access and spawned bits are already in place.
    /// </summary>
    public static readonly CVarDef<string> TutorialDebugStartRoom =
        CVarDef.Create("tutorial.debug_start_room", string.Empty, CVar.SERVER);

    /// <summary>
    /// Seconds a trainee's map is kept after they disconnect or go back to the lobby.
    /// 0 keeps it until they come back or the round restarts.
    /// </summary>
    public static readonly CVarDef<float> TutorialAwayCleanup =
        CVarDef.Create("tutorial.away_cleanup", 1200f, CVar.SERVER);

    public static readonly CVarDef<string> TutorialSolitaryServerConnectionString =
        CVarDef.Create("tutorial.solitary_server_con_string", string.Empty, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<string> TutorialReturnServerConnectionString =
        CVarDef.Create("tutorial.return_server_con_string", string.Empty, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Player said no to the lobby training offer. Do not ask again on this client.</summary>
    public static readonly CVarDef<bool> TutorialDeclined =
        CVarDef.Create("tutorial.declined", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Client-side copy of a finished run. The db is the source of truth, this just covers a
    /// hop back to a main box that does not share that db.
    /// </summary>
    public static readonly CVarDef<bool> TutorialCompleted =
        CVarDef.Create("tutorial.completed", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Tutorial bubble size the player picked in the lobby, as a multiplier on the size worked out
    /// from how much screen there is. Applies to lobby and in-round bubbles alike.
    /// </summary>
    public static readonly CVarDef<float> TutorialBubbleScale =
        CVarDef.Create("tutorial.bubble_scale", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
