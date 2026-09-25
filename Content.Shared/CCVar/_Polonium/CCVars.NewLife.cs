using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether dead players may go back to the lobby and join the round again as a new character.
    /// Toggled by admins at runtime with the <c>togglenewlife</c> command.
    /// </summary>
    public static readonly CVarDef<bool> NewLifeEnabled =
        CVarDef.Create("newlife.enabled", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Seconds a player has to spend as a ghost before the return to the lobby opens up.
    /// </summary>
    public static readonly CVarDef<float> NewLifeDelay =
        CVarDef.Create("newlife.delay", 1200f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// How many times a player may go back to the lobby for a new life in one round. 0 or less means no limit.
    /// </summary>
    public static readonly CVarDef<int> NewLifeMaxNewLives =
        CVarDef.Create("newlife.max_new_lifes", 1, CVar.SERVER | CVar.REPLICATED);
}
