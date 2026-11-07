using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Spawn a test chamber console next to the first player that joins each round.
    /// Turned on in the development build preset, never meant for a live server.
    /// </summary>
    public static readonly CVarDef<bool> TestChamberConsole =
        CVarDef.Create("game.test_chamber_console", false, CVar.SERVERONLY);
}
