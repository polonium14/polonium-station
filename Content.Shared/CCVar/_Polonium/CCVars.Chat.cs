// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether a ping sound plays when one of your chat highlights matches an incoming message.
    /// </summary>
    public static readonly CVarDef<bool> ChatHighlightSound =
        CVarDef.Create("chat.highlight_sound", true, CVar.CLIENTONLY | CVar.ARCHIVE, "Toggles playing a sound when a chat highlight is matched.");

    /// <summary>
    /// The volume of the chat highlight ping sound, from 0 to 1.
    /// </summary>
    public static readonly CVarDef<float> ChatHighlightVolume =
        CVarDef.Create("chat.highlight_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE, "The volume of the chat highlight ping sound.");

    /// <summary>
    /// How many recent distinct chat messages a repeated message can be merged into, shown with an "xN" counter.
    /// 0 disables chat stacking.
    /// </summary>
    public static readonly CVarDef<int> ChatRepeatHistory =
        CVarDef.Create("chat.repeat_history", 4, CVar.SERVER | CVar.REPLICATED, "How many recent distinct chat messages a repeated message can be merged into. 0 disables it.");
}
