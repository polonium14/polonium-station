using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Polonium.Body;
using Content.Shared.Atmos;
using Content.Shared.StatusEffectNew;

namespace Content.Server._Polonium.Body;

/// <summary>
/// While a body breathes liquid it takes no breath from the air around it, so toxic gas and vacuum do not reach the
/// lungs. Oxygen comes from the reagent instead.
/// </summary>
public sealed partial class LiquidBreathingSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _status = default!;

    public override void Initialize()
    {
        base.Initialize();

        // After internals, so a gas tank does not put the air back.
        SubscribeLocalEvent<RespiratorComponent, InhaleLocationEvent>(OnInhaleLocation, after: [typeof(InternalsSystem)]);
    }

    private void OnInhaleLocation(Entity<RespiratorComponent> ent, ref InhaleLocationEvent args)
    {
        if (!_status.HasEffectComp<LiquidBreathingStatusEffectComponent>(ent))
            return;

        args.Gas = new GasMixture();
    }
}
