using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Multiplier for the cost of cargo orders. Read when a station is created, so changes apply from the next round.
    /// </summary>
    public static readonly CVarDef<float> CargoBuyPriceMultiplier =
        CVarDef.Create("cargo.buy_price_multiplier", 1f, CVar.SERVERONLY);

    /// <summary>
    /// Multiplier for the money received from selling goods on cargo pallets. Read when a station is created, so changes apply from the next round.
    /// </summary>
    public static readonly CVarDef<float> CargoSellPriceMultiplier =
        CVarDef.Create("cargo.sell_price_multiplier", 1f, CVar.SERVERONLY);
}
