using System.Collections.Generic;
using System.Linq;
using Content.Server.Chemistry.Components;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects.Effects.Solution;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Kitchen.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.VendingMachines;
using Content.Shared.VendingMachines.Components;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.WikiExporter.Chemistry;

public sealed partial class ChemistryExporter
{
    private const string PillSolution = "food";

    /// <summary>
    /// Everything outside chemical reactions that yields a reagent: tanks, grinding and juicing, plants,
    /// gases, vending machines, prefilled containers, blood, venom and metabolites made in the body.
    /// </summary>
    private List<WikiSource> Sources()
    {
        var sources = new List<WikiSource>();
        sources.AddRange(GasSources());
        sources.AddRange(PlantSources());
        sources.AddRange(EntitySources());
        sources.AddRange(VendingSources());
        sources.AddRange(SpeciesBloodSources());
        sources.AddRange(MetaboliteSources());
        return sources;
    }

    private IEnumerable<WikiSource> GasSources()
    {
        foreach (var gas in Proto.EnumeratePrototypes<GasPrototype>().OrderBy(g => g.ID))
        {
            if (gas.Reagent is { } reagent)
                yield return new WikiSource("gas", Loc.GetString(gas.Name), new() { [reagent.Id] = 1 });
        }
    }

    /// <summary>
    /// Harvested produce gets its chemicals from the plant, not from the produce prototype.
    /// </summary>
    private IEnumerable<WikiSource> PlantSources()
    {
        var sold = Proto.EnumeratePrototypes<VendingMachineInventoryPrototype>()
            .SelectMany(Stock)
            .ToHashSet();

        var plants = new List<(EntityPrototype Proto, PlantDataComponent Data)>();
        foreach (var proto in Proto.EnumeratePrototypes<EntityPrototype>().OrderBy(p => p.ID))
        {
            if (!proto.Abstract && proto.TryComp<PlantDataComponent>(out var data, Factory))
                plants.Add((proto, data));
        }

        foreach (var (proto, data) in plants)
        {
            if (!proto.TryComp<PlantChemicalsComponent>(out var chemicals, Factory) || chemicals.Chemicals.Count == 0)
                continue;

            var amounts = chemicals.Chemicals.ToDictionary(c => c.Key.Id, c => c.Value.Max.Double());
            var via = sold.Contains(data.PacketPrototype)
                ? null
                : plants.Where(p => p.Data.MutationPrototypes.Contains(proto.ID))
                    .SelectMany(p => p.Data.ProductPrototypes)
                    .Select(id => Proto.Index(id).Name)
                    .Distinct()
                    .ToList();

            foreach (var product in data.ProductPrototypes)
            {
                yield return new WikiSource("plant", Proto.Index(product).Name, amounts, Via: via?.Count > 0 ? via : null);
            }
        }
    }

    private List<WikiSource> EntitySources()
    {
        var found = new List<WikiSource>();
        var species = Proto.EnumeratePrototypes<SpeciesPrototype>().Select(s => s.Prototype.Id).ToHashSet();
        var seen = new HashSet<(string, string)>();

        foreach (var proto in Proto.EnumeratePrototypes<EntityPrototype>().OrderBy(p => p.ID))
        {
            if (proto.Abstract || proto.HideSpawnMenu)
                continue;

            if (proto.HasComp<ReagentTankComponent>(Factory))
                Add("tank", Contents(proto));

            if (IsContainer(proto))
                Add("item", ContainerContents(proto));

            if (proto.HasComp<MobStateComponent>(Factory) && !InheritsFrom(proto, species))
            {
                if (proto.TryComp<BloodstreamComponent>(out var blood, Factory))
                    Add("blood", blood.BloodReferenceSolution.Contents);

                if (proto.TryComp<MeleeChemicalInjectorComponent>(out var venom, Factory) &&
                    _solutions.TryGetSolution(proto, venom.Solution, out var venomSolution))
                {
                    Add("venom", venomSolution.Contents);
                }
            }

            if (!proto.TryComp<ExtractableComponent>(out var extractable, Factory) ||
                proto.HasComp<OrganComponent>(Factory) ||
                proto.HasComp<PillComponent>(Factory))
            {
                continue;
            }

            if (extractable.JuiceSolution is { } juice)
                Add("juice", juice.Contents);

            // Produce is refilled from its plant when harvested, see PlantSources.
            if (!proto.HasComp<ProduceComponent>(Factory) &&
                extractable.GrindableSolutionName is { } grind &&
                _solutions.TryGetSolution(proto, grind, out var ground))
            {
                Add("grind", ground.Contents);
            }

            void Add(string kind, IEnumerable<ReagentQuantity> contents)
            {
                if (Sum(contents) is not { Count: > 0 } amounts || !seen.Add((kind, proto.Name)))
                    return;

                found.Add(new WikiSource(kind, proto.Name, amounts));
            }
        }

        return found;
    }

    private IEnumerable<WikiSource> VendingSources()
    {
        var seen = new HashSet<string>();
        foreach (var proto in Proto.EnumeratePrototypes<EntityPrototype>().OrderBy(p => p.ID))
        {
            if (proto.Abstract || proto.HideSpawnMenu ||
                !proto.TryComp<VendingMachineComponent>(out var vending, Factory) ||
                !Proto.TryIndex(vending.PackPrototypeId, out var inventory))
            {
                continue;
            }

            var amounts = Sum(Stock(inventory).Select(Proto.Index).Where(IsContainer).SelectMany(ContainerContents));
            if (amounts.Count > 0 && seen.Add(proto.Name))
                yield return new WikiSource("vend", proto.Name, amounts);
        }
    }

    private IEnumerable<WikiSource> SpeciesBloodSources()
    {
        foreach (var species in Proto.EnumeratePrototypes<SpeciesPrototype>().OrderBy(s => s.ID))
        {
            if (!species.RoundStart ||
                !Proto.Index(species.Prototype).TryComp<BloodstreamComponent>(out var blood, Factory) ||
                Sum(blood.BloodReferenceSolution.Contents) is not { Count: > 0 } amounts)
            {
                continue;
            }

            yield return new WikiSource("blood", Loc.GetString(species.Name), amounts);
        }
    }

    /// <summary>
    /// Reagents the body makes out of another reagent while metabolizing it.
    /// </summary>
    private IEnumerable<WikiSource> MetaboliteSources()
    {
        foreach (var reagent in Proto.EnumeratePrototypes<ReagentPrototype>().OrderBy(r => r.ID))
        {
            if (reagent.Abstract || reagent.Metabolisms is not { } metabolisms)
                continue;

            var amounts = new Dictionary<string, double>();
            foreach (var entry in metabolisms.Metabolisms.Values)
            {
                foreach (var (metabolite, ratio) in entry.Metabolites ?? [])
                {
                    amounts[metabolite.Id] = amounts.GetValueOrDefault(metabolite.Id) + ratio.Double();
                }

                foreach (var effect in entry.Effects.OfType<AdjustReagent>())
                {
                    if (effect.Amount > 0 && effect.Reagent != reagent.ID)
                        amounts[effect.Reagent.Id] = amounts.GetValueOrDefault(effect.Reagent.Id) + effect.Amount.Double();
                }
            }

            amounts.Remove(reagent.ID);
            if (amounts.Count > 0)
                yield return new WikiSource("metabolite", reagent.LocalizedName, amounts, From: reagent.ID);
        }
    }

    /// <summary>
    /// Bottles, jugs, cans, syringes, injectors and pills, whose contents are the point of the item.
    /// </summary>
    private bool IsContainer(EntityPrototype proto)
    {
        return proto.HasComp<DrainableSolutionComponent>(Factory) ||
               proto.HasComp<InjectorComponent>(Factory) ||
               proto.HasComp<PillComponent>(Factory);
    }

    /// <summary>
    /// What the container holds, without the solution its material grinds into.
    /// </summary>
    private IEnumerable<ReagentQuantity> ContainerContents(EntityPrototype proto)
    {
        if (proto.TryComp<SolutionComponent>(out var own, Factory))
            return own.Solution.Contents;

        var name = proto.TryComp<DrainableSolutionComponent>(out var drainable, Factory) ? drainable.Solution
            : proto.TryComp<InjectorComponent>(out var injector, Factory) ? injector.SolutionName
            : PillSolution;

        return _solutions.TryGetSolution(proto, name, out var solution) ? solution.Contents : [];
    }

    private static IEnumerable<EntProtoId> Stock(VendingMachineInventoryPrototype inventory)
    {
        return inventory.StartingInventory.Keys
            .Concat(inventory.ContrabandInventory?.Keys ?? Enumerable.Empty<EntProtoId>())
            .Concat(inventory.EmaggedInventory?.Keys ?? Enumerable.Empty<EntProtoId>());
    }

    private static Dictionary<string, double> Sum(IEnumerable<ReagentQuantity> contents)
    {
        var amounts = new Dictionary<string, double>();
        foreach (var quantity in contents)
        {
            amounts[quantity.Reagent.Prototype] = amounts.GetValueOrDefault(quantity.Reagent.Prototype) + quantity.Quantity.Double();
        }

        return amounts;
    }

    private bool InheritsFrom(EntityPrototype proto, HashSet<string> ancestors)
    {
        return Proto.EnumerateAllParents<EntityPrototype>(proto.ID, true).Any(parent => ancestors.Contains(parent.id));
    }
}
