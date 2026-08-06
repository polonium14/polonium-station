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
}
