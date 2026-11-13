using Content.Server._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Components;
using Content.Shared.Audio;

namespace Content.Server._Polonium.Power.Generation.FissionGenerator;

public sealed partial class ReactorHumSystem : EntitySystem
{
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;

    [Dependency] private EntityQuery<NuclearReactorComponent> _reactorQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReactorHumComponent, AtmosDeviceUpdateEvent>(OnUpdate, after: [typeof(NuclearReactorSystem)]);
        SubscribeLocalEvent<ReactorHumComponent, AtmosDeviceDisabledEvent>(OnDisabled);
    }

    private void OnUpdate(Entity<ReactorHumComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        if (!_reactorQuery.TryComp(ent, out var reactor))
            return;

        _ambient.SetAmbience(ent, !reactor.Melted && reactor.ThermalPower >= ent.Comp.MinThermalPower);
    }

    private void OnDisabled(Entity<ReactorHumComponent> ent, ref AtmosDeviceDisabledEvent args)
    {
        _ambient.SetAmbience(ent, false);
    }
}
