using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared._Polonium.Modules.ModSuit.Events;
using Content.Shared._Polonium.Modules.ModSuit.UI;

namespace Content.Shared._Polonium.Modules.ModSuit;

public partial class SharedModSuitSystem
{
    private void InitializeComplexity()
    {
        SubscribeLocalEvent<ModSuitComplexityLimitComponent, ModuleContainerModuleAddedEvent>(OnComplexityModuleAdded);
        SubscribeLocalEvent<ModSuitComplexityLimitComponent, ModuleContainerModuleRemovedEvent>(OnComplexityModuleRemoved);
        SubscribeLocalEvent<ModSuitComplexityLimitComponent, ModuleContainerModuleAddingAttemptEvent>(OnComplexityModuleAttempt);

        SubscribeLocalEvent<ModSuitComplexityLimitComponent, ModSuitGetUiEntriesEvent>(OnGetModSuitUiState);
    }

    private void OnComplexityModuleAdded(Entity<ModSuitComplexityLimitComponent> ent, ref ModuleContainerModuleAddedEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!TryComp<ModSuitModuleComplexityComponent>(args.Module, out var moduleComp))
            return;

        ent.Comp.Complexity += moduleComp.Complexity;
        Dirty(ent);
    }

    private void OnComplexityModuleRemoved(Entity<ModSuitComplexityLimitComponent> ent, ref ModuleContainerModuleRemovedEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!TryComp<ModSuitModuleComplexityComponent>(args.Module, out var moduleComp))
            return;

        ent.Comp.Complexity -= moduleComp.Complexity;
        Dirty(ent);
    }

    private void OnComplexityModuleAttempt(Entity<ModSuitComplexityLimitComponent> ent, ref ModuleContainerModuleAddingAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<ModSuitModuleComplexityComponent>(args.Module, out var moduleComp))
            return;

        if (ent.Comp.Complexity + moduleComp.Complexity > GetMaxComplexity(ent))
            args.Cancel();
    }

    private void OnGetModSuitUiState(Entity<ModSuitComplexityLimitComponent> ent, ref ModSuitGetUiEntriesEvent args)
    {
        var toAdd = (ent.Comp.Complexity, GetMaxComplexity(ent));
        ModSuitComplexityBuiEntry? foundEntry = null;

        foreach (var entry in args.Entries)
        {
            if (entry is not ModSuitComplexityBuiEntry complexityEntry)
                continue;

            foundEntry = complexityEntry;
            break;
        }

        if (foundEntry == null)
        {
            var newEntry = new ModSuitComplexityBuiEntry(toAdd);
            args.Entries.Add(newEntry);
            return;
        }

        foundEntry.Complexity = toAdd;
    }
}
