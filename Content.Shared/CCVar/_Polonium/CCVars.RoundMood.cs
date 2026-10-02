using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Always show the secret preset in the lobby, the status API and the round end summary, whatever is really running.
    /// Preset and mood votes stop showing their counts too.
    /// </summary>
    public static readonly CVarDef<bool> GamePresetHidden =
        CVarDef.Create("game.preset_hidden", false, CVar.SERVERONLY);

    /// <summary>
    /// The preset vote asks for the mood of the next round instead of a preset.
    /// Players never get to see how it went.
    /// </summary>
    public static readonly CVarDef<bool> VotePresetMood =
        CVarDef.Create("vote.preset_mood", false, CVar.SERVERONLY);
}
