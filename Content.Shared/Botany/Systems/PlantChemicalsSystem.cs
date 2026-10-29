using JetBrains.Annotations;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.FixedPoint;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Botany.Systems;

/// <summary>
/// Handles the chemicals of a plant.
/// </summary>
public sealed partial class PlantChemicalsSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private BotanySystem _botany = default!;
    [Dependency] private PlantMutationSystem _mutation = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;

    [SubscribeLocalEvent]
    private void OnCrossPollinate(Entity<PlantChemicalsComponent> ent, ref PlantCrossPollinateEvent args)
    {
        if (!_botany.TryGetPlantComponent<PlantChemicalsComponent>(args.PollenData, args.PollenProtoId, out var pollenData))
            return;

        _mutation.CrossChemicals(ent, ref ent.Comp.Chemicals, pollenData.Chemicals);
        Dirty(ent);
    }

    /// <summary>
    /// Adds a random chemical to the plant chemicals.
    /// </summary>
    [PublicAPI]
    public void MutateRandomChemical(Entity<PlantChemicalsComponent?> ent, WeightedRandomFillSolutionPrototype randomChems)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        // TODO: No prediction until we have a better way to handle predicted random.
        if (!_net.IsServer)
            return;

        var (chemicalId, quantity) = randomChems.Pick(_random);
        var amount = FixedPoint2.Max(_random.NextFloat(0f, 1f) * quantity, FixedPoint2.Epsilon);
        var seedChemQuantity = new PlantChemQuantity();
        if (ent.Comp.Chemicals.TryGetValue(chemicalId, out var value))
        {
            seedChemQuantity.Min = value.Min;
            seedChemQuantity.Max = value.Max + amount;
        }
        else
        {
            //Set the minimum to a fifth of the quantity to give some level of bad luck protection
            seedChemQuantity.Min = FixedPoint2.Clamp(quantity / 5f, FixedPoint2.Epsilon, 1f);
            seedChemQuantity.Max = seedChemQuantity.Min + amount;
            seedChemQuantity.Inherent = false;
        }

        var potencyDivisor = 100f / seedChemQuantity.Max;
        seedChemQuantity.PotencyDivisor = (float)potencyDivisor;
        ent.Comp.Chemicals[chemicalId] = seedChemQuantity;
        Dirty(ent);
    }
}
