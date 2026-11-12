using System.Collections.Generic;
using System.Linq;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers;
using Content.Shared.EntityTable;
using Content.Shared.Kitchen.Components;
using Content.Shared.Metabolism;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.WikiExporter.Chemistry;

/// <summary>
/// Reagents, reactions and reagent sources for the chemistry catalog on wiki based on Wiki.JS.
/// </summary>
public sealed partial class ChemistryExporter : WikiExporter
{
    private static readonly EntProtoId Dispenser = "ChemDispenser";
    private static readonly ProtoId<MixingCategoryPrototype> DefaultMix = "DummyMix";

    private SharedSolutionContainerSystem _solutions = default!;

    public override string Name => "chemistry";

    public override object Export()
    {
        _solutions = EntitySystems.GetEntitySystem<SharedSolutionContainerSystem>();
        return new WikiData(
            Proto.EnumeratePrototypes<MetabolismStagePrototype>().ToDictionary(p => p.ID, p => p.LocalizedName),
            Proto.EnumeratePrototypes<MixingCategoryPrototype>().ToDictionary(p => p.ID, p => Loc.GetString(p.VerbText)),
            DispenserReagents(),
            Proto.EnumeratePrototypes<ReagentPrototype>().Where(p => !p.Abstract).ToDictionary(p => p.ID, MakeReagent),
            Proto.EnumeratePrototypes<ReactionPrototype>().OrderBy(p => p.ID).Select(MakeReaction).ToList(),
            Sources());
    }

    private WikiReagent MakeReagent(ReagentPrototype proto)
    {
        var effects = new ReagentGuideEntry(proto, Proto, EntitySystems).GuideEntries?
            .Where(e => e.Value.EffectDescriptions.Length > 0)
            .Select(e => new WikiMetabolism(e.Key, e.Value.EffectDescriptions))
            .ToList();

        return new WikiReagent(
            proto.LocalizedName,
            proto.LocalizedDescription,
            proto.SubstanceColor.ToHex(),
            proto.Group,
            effects?.Count > 0 ? effects : null);
    }

    private WikiReaction MakeReaction(ReactionPrototype proto)
    {
        return new WikiReaction(
            proto.Reactants.Where(r => !r.Value.Catalyst).ToDictionary(r => r.Key.Id, r => r.Value.Amount.Double()),
            proto.Reactants.Where(r => r.Value.Catalyst).ToDictionary(r => r.Key.Id, r => r.Value.Amount.Double()),
            proto.Products.ToDictionary(p => p.Key.Id, p => p.Value.Double()),
            (proto.MixingCategories ?? [DefaultMix]).Select(c => c.Id).ToList(),
            proto.MinimumTemperature,
            float.IsFinite(proto.MaximumTemperature) ? proto.MaximumTemperature : null,
            proto.Quantized,
            proto.Priority,
            proto.Source,
            proto.Effects.Select(e => e.EntityEffectGuidebookText(Proto, EntitySystems)).OfType<string>().ToList());
    }

    private List<string> DispenserReagents()
    {
        var found = new SortedSet<string>();
        if (!Proto.Index(Dispenser).TryComp<EntityTableContainerFillComponent>(out var fill, Factory))
            return [];

        var tables = EntitySystems.GetEntitySystem<EntityTableSystem>();
        foreach (var table in fill.Containers.Values)
        {
            foreach (var item in tables.GetSpawns(table))
            {
                foreach (var quantity in Contents(Proto.Index(item)))
                {
                    found.Add(quantity.Reagent.Prototype);
                }
            }
        }

        return found.ToList();
    }

    private IEnumerable<ReagentQuantity> Contents(EntityPrototype proto)
    {
        if (proto.TryComp<SolutionComponent>(out var own, Factory))
        {
            foreach (var quantity in own.Solution.Contents)
            {
                yield return quantity;
            }
        }

        foreach (var (_, solution) in _solutions.EnumerateSolutions(proto))
        {
            foreach (var quantity in solution.Contents)
            {
                yield return quantity;
            }
        }
    }

    private List<WikiSource> Sources()
    {
        var sources = new List<WikiSource>();

        foreach (var gas in Proto.EnumeratePrototypes<GasPrototype>().OrderBy(g => g.ID))
        {
            if (gas.Reagent is { } reagent)
                sources.Add(new WikiSource("gas", Loc.GetString(gas.Name), new() { [reagent.Id] = 1 }));
        }

        var names = new HashSet<string>();
        foreach (var proto in Proto.EnumeratePrototypes<EntityPrototype>().OrderBy(p => p.ID))
        {
            if (proto.Abstract || names.Contains(proto.Name))
                continue;

            if (!proto.HideSpawnMenu && proto.HasComp<ReagentTankComponent>(Factory))
                Add("tank", Contents(proto));

            if (!proto.TryComp<ExtractableComponent>(out var extractable, Factory) ||
                proto.HasComp<OrganComponent>(Factory) ||
                proto.HasComp<PillComponent>(Factory))
            {
                continue;
            }

            if (extractable.JuiceSolution is { } juice)
                Add("juice", juice.Contents);

            if (extractable.GrindableSolutionName is { } grind && _solutions.TryGetSolution(proto, grind, out var ground))
                Add("grind", ground.Contents);

            void Add(string kind, IEnumerable<ReagentQuantity> contents)
            {
                var amounts = new Dictionary<string, double>();
                foreach (var quantity in contents)
                {
                    amounts[quantity.Reagent.Prototype] = amounts.GetValueOrDefault(quantity.Reagent.Prototype) + quantity.Quantity.Double();
                }

                if (amounts.Count == 0)
                    return;

                sources.Add(new WikiSource(kind, proto.Name, amounts));
                names.Add(proto.Name);
            }
        }

        return sources;
    }

    private sealed record WikiData(
        Dictionary<string, string> Stages,
        Dictionary<string, string> Mixers,
        List<string> Dispenser,
        Dictionary<string, WikiReagent> Reagents,
        List<WikiReaction> Reactions,
        List<WikiSource> Sources);

    private sealed record WikiReagent(string Name, string Desc, string Color, string Group, List<WikiMetabolism>? Effects);

    private sealed record WikiMetabolism(string Stage, string[] Lines);

    private sealed record WikiReaction(
        Dictionary<string, double> In,
        Dictionary<string, double> Catalysts,
        Dictionary<string, double> Out,
        List<string> Mix,
        float MinTemp,
        float? MaxTemp,
        bool Quantized,
        int Priority,
        bool Source,
        List<string> Effects);

    private sealed record WikiSource(string Kind, string Name, Dictionary<string, double> Out);
}
