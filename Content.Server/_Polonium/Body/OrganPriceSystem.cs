using Content.Server.Cargo.Components;
using Content.Shared.Body;
using Content.Shared.Cargo;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Robust.Shared.Random;

namespace Content.Server._Polonium.Body;

/// <summary>
/// Prices organs that came out of a body. See <see cref="OrganPriceComponent"/>.
/// </summary>
public sealed partial class OrganPriceSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganPriceComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<OrganPriceComponent, PriceCalculationEvent>(OnPriceCalculation);
    }

    private void OnInserted(Entity<OrganPriceComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (ent.Comp.Harvested)
            return;

        ent.Comp.Harvested = true;
        if (ent.Comp.MaxPrice is { } max)
            ent.Comp.Price = Math.Round(_random.NextDouble(ent.Comp.Price, max));
    }

    private void OnPriceCalculation(Entity<OrganPriceComponent> ent, ref PriceCalculationEvent args)
    {
        // Organs still in a body are part of the mob's own price. Ones with a static price, like prostheses, keep just that.
        if (args.Handled
            || !ent.Comp.Harvested
            || HasComp<StaticPriceComponent>(ent)
            || CompOrNull<OrganComponent>(ent)?.Body != null)
            return;

        var price = ent.Comp.Price;
        if (TryComp<OrganIntegrityComponent>(ent, out var integrity) && integrity.IntegrityCap > 0)
            price *= Math.Clamp((integrity.OrganIntegrity / integrity.IntegrityCap).Double(), 0, 1);

        args.Price += Math.Round(price);
    }
}
