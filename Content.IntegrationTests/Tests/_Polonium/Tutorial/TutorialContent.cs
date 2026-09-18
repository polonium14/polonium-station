#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Shared._Polonium.Tutorial.Actions;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Tutorial;

/// <summary>
/// Reads a flow the way the tutorial itself does: every condition, action and watcher a step can
/// reach, and the anchors and lines they name. Tests go through here so that a condition type
/// written tomorrow is covered the day it is written, not the day someone remembers to list it.
/// </summary>
internal static class TutorialContent
{
    public const string BasicFlow = "TutorialBasic";

    /// <summary>Everything under here is a data definition the walk may descend into.</summary>
    private const string DataNamespace = "Content.Shared._Polonium.Tutorial";

    /// <summary>Fields holding an anchor id without saying so in their name.</summary>
    private static readonly HashSet<string> AnchorFields = new()
    {
        $"{nameof(TutorialTileHighlight)}.{nameof(TutorialTileHighlight.To)}",
        $"{nameof(TutorialLatheGuide)}.{nameof(TutorialLatheGuide.Lathe)}",
        $"{nameof(TutorialLatheGuide)}.{nameof(TutorialLatheGuide.CountAt)}",
        $"{nameof(ConfineAnchorAction)}.{nameof(ConfineAnchorAction.Doorways)}",
    };

    public static TutorialFlowPrototype Flow(IPrototypeManager proto, string id = BasicFlow)
    {
        return proto.Index<TutorialFlowPrototype>(id);
    }

    public static List<TutorialStepPrototype> Steps(IPrototypeManager proto, TutorialFlowPrototype flow)
    {
        return flow.Steps.Select(id => proto.Index(id)).ToList();
    }

    /// <summary>The step plus every piece of tutorial data hanging off it, watcher sets included.</summary>
    public static List<object> Nodes(IPrototypeManager proto, TutorialStepPrototype step)
    {
        var nodes = new List<object> { step };
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);

        foreach (var value in Members(step))
            Collect(value, nodes, seen);

        foreach (var setId in step.WatcherSets)
        {
            if (proto.TryIndex(setId, out var set))
                Collect(set.Watchers, nodes, seen);
        }

        return nodes;
    }

    /// <summary>Anchor ids a single piece of tutorial data names.</summary>
    public static IEnumerable<string> Anchors(object node)
    {
        var type = node.GetType();
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!NamesAnchor(type, field.Name))
                continue;

            foreach (var id in Ids(field.GetValue(node)))
                yield return id;
        }
    }

    /// <summary>Anchors this piece of data brings into being rather than expects to find.</summary>
    public static IEnumerable<string> AssignedAnchors(object node)
    {
        var type = node.GetType();
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.Name != "AssignAnchorId")
                continue;

            foreach (var id in Ids(field.GetValue(node)))
                yield return id;
        }
    }

    /// <summary>Localisation ids a single piece of tutorial data names.</summary>
    public static IEnumerable<string> LocIds(object node)
    {
        foreach (var value in Members(node))
        {
            switch (value)
            {
                case LocId locId when !string.IsNullOrEmpty(locId.Id):
                    yield return locId.Id;
                    break;
                case IEnumerable<LocId> list:
                    foreach (var item in list)
                    {
                        if (!string.IsNullOrEmpty(item.Id))
                            yield return item.Id;
                    }

                    break;
            }
        }
    }

    private static void Collect(object? value, List<object> nodes, HashSet<object> seen)
    {
        switch (value)
        {
            case null or string:
                return;
            case IEnumerable list:
                foreach (var item in list)
                    Collect(item, nodes, seen);

                return;
        }

        if (!IsTutorialData(value) || !seen.Add(value))
            return;

        nodes.Add(value);

        foreach (var member in Members(value))
            Collect(member, nodes, seen);
    }

    private static bool IsTutorialData(object value)
    {
        var type = value.GetType();
        return !type.IsPrimitive
               && !type.IsEnum
               && type.Namespace?.StartsWith(DataNamespace, StringComparison.Ordinal) == true;
    }

    private static bool NamesAnchor(Type type, string name)
    {
        return name.Contains("Anchor", StringComparison.Ordinal)
               || AnchorFields.Contains($"{type.Name}.{name}");
    }

    private static IEnumerable<string> Ids(object? value)
    {
        switch (value)
        {
            case string single when !string.IsNullOrWhiteSpace(single):
                yield return single;
                break;
            case IEnumerable<string> many:
                foreach (var id in many)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                        yield return id;
                }

                break;
        }
    }

    private static IEnumerable<object?> Members(object node)
    {
        var type = node.GetType();

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            yield return field.GetValue(node);

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.GetIndexParameters().Length == 0)
                yield return property.GetValue(node);
        }
    }
}
