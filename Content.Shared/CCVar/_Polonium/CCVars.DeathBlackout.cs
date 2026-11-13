using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether the screen cuts to black and the audio goes silent when a player dies from critical state.
    /// </summary>
    public static readonly CVarDef<bool> DeathBlackoutEnabled =
        CVarDef.Create("deathblackout.enabled", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Whether critical state gradually darkens the screen and lowers the audio as the player gets closer to death.
    /// </summary>
    public static readonly CVarDef<bool> DeathBlackoutCritFade =
        CVarDef.Create("deathblackout.crit_fade", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Seconds of black screen after death. Ghosting is blocked for this long.
    /// </summary>
    public static readonly CVarDef<float> DeathBlackoutDuration =
        CVarDef.Create("deathblackout.duration", 7.5f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Seconds the black screen takes to fade out once the death blackout ends.
    /// </summary>
    public static readonly CVarDef<float> DeathBlackoutFadeOut =
        CVarDef.Create("deathblackout.fade_out", 1.5f, CVar.SERVER | CVar.REPLICATED);
}
