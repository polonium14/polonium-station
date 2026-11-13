using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Atmos;
using Content.Shared.EntityEffects.Effects.Body;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.EntityTable;
using Content.Shared.FixedPoint;
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
    private Dictionary<ProtoId<MetabolismStagePrototype>, MetabolismSolutionEntry> _stages = default!;
    private TimeSpan _cycle;

    public override string Name => "chemistry";

    public override object Export()
    {
        _solutions = EntitySystems.GetEntitySystem<SharedSolutionContainerSystem>();
        var metabolizer = Factory.GetComponent<MetabolizerComponent>();
        _stages = metabolizer.Solutions;
        _cycle = metabolizer.UpdateInterval;
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
        var effects = proto.Metabolisms?.Metabolisms
            .Select(m => MakeMetabolism(proto, m.Key, m.Value))
            .Where(m => m.Lines.Length > 0)
            .ToList();

        return new WikiReagent(
            proto.LocalizedName,
            proto.LocalizedDescription,
            proto.SubstanceColor.ToHex(),
            proto.Group,
            effects?.Count > 0 ? effects : null);
    }

    private WikiMetabolism MakeMetabolism(ReagentPrototype proto, ProtoId<MetabolismStagePrototype> stage, ReagentEffectsEntry entry)
    {
        var all = _stages.TryGetValue(stage, out var solution) && solution.MetabolizeAll;
        var lines = entry.Effects
            .Select(effect => EffectLine(proto, effect, entry.MetabolismRate))
            .OfType<string>()
            .ToArray();

        return new WikiMetabolism(stage, entry.MetabolismRate.Double() / _cycle.TotalSeconds, all, lines);
    }

    /// <summary>
    /// The guidebook line of one effect. The game does not say how strong some effects are,
    /// so their part of the line is swapped for a wiki text with the amount.
    /// </summary>
    private string? EffectLine(ReagentPrototype proto, EntityEffect effect, FixedPoint2 rate)
    {
        var line = proto.GuidebookReagentEffectDescription(Proto, EntitySystems, effect, rate);
        if (line == null
            || AmountText(effect) is not { } amount
            || effect.EntityEffectGuidebookText(Proto, EntitySystems) is not { } text)
            return line;

        return line.Replace(text, amount);
    }

    private static string? AmountText(EntityEffect effect)
    {
        return effect switch
        {
            Jitter jitter => Loc.GetString("wiki-effect-jittering",
                ("chance", jitter.Probability), ("time", jitter.Time), ("refresh", jitter.Refresh)),
            Drunk drunk => Loc.GetString("wiki-effect-drunk",
                ("chance", drunk.Probability), ("time", drunk.BoozePower.TotalSeconds)),
            ModifyBleed bleed => Loc.GetString("wiki-effect-modify-bleed-amount",
                ("chance", bleed.Probability), ("deltasign", MathF.Sign(bleed.Amount)), ("amount", MathF.Abs(bleed.Amount))),
            ModifyBloodLevel blood => Loc.GetString("wiki-effect-modify-blood-level",
                ("chance", blood.Probability), ("deltasign", MathF.Sign(blood.Amount.Float())), ("amount", MathF.Abs(blood.Amount.Float()))),
            Flammable flammable => Loc.GetString("wiki-effect-flammable-reaction",
                ("chance", flammable.Probability), ("amount", flammable.Multiplier)),
            _ => null,
        };
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

    private sealed record WikiData(
        Dictionary<string, string> Stages,
        Dictionary<string, string> Mixers,
        List<string> Dispenser,
        Dictionary<string, WikiReagent> Reagents,
        List<WikiReaction> Reactions,
        List<WikiSource> Sources);

    private sealed record WikiReagent(string Name, string Desc, string Color, string Group, List<WikiMetabolism>? Effects);

    /// <param name="Rate">Units metabolized per second.</param>
    /// <param name="All">The organ metabolizes the whole amount every cycle, like the lungs, so Rate does not apply.</param>
    private sealed record WikiMetabolism(string Stage, double Rate, bool All, string[] Lines);

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

    /// <param name="From">Reagent this one is made from in the body, for metabolites.</param>
    /// <param name="Via">Plants whose mutation gives this one, when its seeds are not sold anywhere.</param>
    private sealed record WikiSource(
        string Kind,
        string Name,
        Dictionary<string, double> Out,
        string? From = null,
        List<string>? Via = null);
}
