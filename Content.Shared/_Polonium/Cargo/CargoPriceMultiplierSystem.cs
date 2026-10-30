using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Shared._Polonium.Cargo;

/// <summary>
/// Applies the per-round cargo price multipliers from <see cref="CargoPriceMultiplierComponent"/>.
/// </summary>
public sealed class CargoPriceMultiplierSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationBankAccountComponent, MapInitEvent>(OnMapInit);
    }

    // The CVars are read only once, when the station is created, so changing them mid-round
    // doesn't change prices that players have already seen and only applies from the next round.
    private void OnMapInit(Entity<StationBankAccountComponent> ent, ref MapInitEvent args)
    {
        // The CVars are server-only, the client gets the values from the networked component.
        if (_net.IsClient)
            return;

        var multiplier = EnsureComp<CargoPriceMultiplierComponent>(ent);

        // Negative multipliers would make orders give money or sales take it.
        multiplier.BuyMultiplier = Math.Max(0f, _cfg.GetCVar(CCVars.CargoBuyPriceMultiplier));
        multiplier.SellMultiplier = Math.Max(0f, _cfg.GetCVar(CCVars.CargoSellPriceMultiplier));

        Dirty(ent, multiplier);
    }

    /// <summary>
    /// Returns the cost of a single unit of the product on the given station.
    /// Without a station or multiplier the base cost is returned.
    /// </summary>
    public int GetBuyCost(EntityUid? station, CargoProductPrototype product)
    {
        if (!TryComp<CargoPriceMultiplierComponent>(station, out var multiplier))
            return product.Cost;

        return (int) Math.Round(product.Cost * multiplier.BuyMultiplier);
    }

    /// <summary>
    /// Returns how much the station receives for goods with the given base price.
    /// Without a station or multiplier the base price is returned.
    /// </summary>
    public double GetSellPrice(EntityUid? station, double price)
    {
        if (!TryComp<CargoPriceMultiplierComponent>(station, out var multiplier))
            return price;

        return price * multiplier.SellMultiplier;
    }
}
