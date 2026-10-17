using System.Linq;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// Runs the tray's own reagent pass for trays that have nothing planted yet, so watering and
/// fertilising an empty tray moves the gauges and puts the warning lights out before the seeds
/// go in. Everything else about the tray is left to the botany code.
/// </summary>
public sealed partial class TutorialSoilSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private PlantTraySystem _tray = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialSoilComponent, TrayUpdateEvent>(OnTrayUpdate);
    }

    private void OnTrayUpdate(Entity<TutorialSoilComponent> ent, ref TrayUpdateEvent args)
    {
        if (!TryComp<PlantTrayComponent>(ent, out var tray))
            return;

        // with something growing in there the botany pass already did this
        if (_tray.TryGetPlant((ent.Owner, tray), out _))
            return;

        if (!_solution.TryGetSolution(ent.Owner, tray.SoilSolutionName, out var soil, out var solution)
            || soil is not { } soilSoln
            || solution.Volume <= 0)
        {
            return;
        }

        foreach (var entry in solution.Contents.ToArray())
        {
            var reagent = _proto.Index<ReagentPrototype>(entry.Reagent.Prototype);
            _effects.ApplyEffects(ent.Owner, [.. reagent.PlantMetabolisms], entry.Quantity.Float());
        }

        _solution.RemoveEachReagent(soilSoln, FixedPoint2.New(1));
    }
}
