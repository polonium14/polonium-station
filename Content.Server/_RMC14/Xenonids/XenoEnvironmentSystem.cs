using Content.Server.Atmos.Components;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Atmos.Components;
using Content.Shared.Temperature.Components;

namespace Content.Server._RMC14.Xenonids;

public sealed class XenoEnvironmentSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<XenoEnvironmentVulnerabilityComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<XenoEnvironmentVulnerabilityComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<FlammableComponent>(ent, out var flammable)
            && !MathHelper.CloseTo(ent.Comp.Fire, 1f))
        {
            flammable.Damage *= ent.Comp.Fire;
            Dirty(ent, flammable);
        }

        if (TryComp<TemperatureDamageComponent>(ent, out var temperature))
        {
            if (!MathHelper.CloseTo(ent.Comp.Fire, 1f))
                temperature.HeatDamage *= ent.Comp.Fire;

            if (!MathHelper.CloseTo(ent.Comp.Space, 1f))
                temperature.ColdDamage *= ent.Comp.Space;
        }

        if (TryComp<BarotraumaComponent>(ent, out var barotrauma)
            && !MathHelper.CloseTo(ent.Comp.Space, 1f))
        {
            barotrauma.Damage *= ent.Comp.Space;
        }
    }
}
