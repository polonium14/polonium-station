using Content.Server.Cargo.Components;
using Content.Shared.Body;
using Content.Shared.Cargo;

namespace Content.Server._Polonium.Body;

/// <summary>
/// Prices organs that came out of a body. See <see cref="OrganPriceComponent"/>.
/// </summary>
public sealed class OrganPriceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganPriceComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<OrganPriceComponent, PriceCalculationEvent>(OnPriceCalculation);
    }

    private void OnInserted(Entity<OrganPriceComponent> ent, ref OrganGotInsertedEvent args)
    {
        ent.Comp.Harvested = true;
    }

    private void OnPriceCalculation(Entity<OrganPriceComponent> ent, ref PriceCalculationEvent args)
    {
        // Organs still in a body are part of the mob's own price. Ones with a static price, like prostheses, keep just that.
        if (args.Handled
            || !ent.Comp.Harvested
            || HasComp<StaticPriceComponent>(ent)
            || CompOrNull<OrganComponent>(ent)?.Body != null)
            return;

        args.Price += ent.Comp.Price;
    }
}
