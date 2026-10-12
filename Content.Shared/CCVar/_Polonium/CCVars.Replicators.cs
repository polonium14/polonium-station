using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether the hive size limit stops scaling with the number of connected players.
    /// When set, a hive may always grow up to its maximum size.
    /// </summary>
    public static readonly CVarDef<bool> ReplicatorIgnorePlayerCount =
        CVarDef.Create("replicator.ignore_player_count", false, CVar.SERVERONLY);
}
