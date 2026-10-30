using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared._Polonium.Modules.ModSuit.Events;
using System.Linq;

namespace Content.Shared._Polonium.Modules.Modules;

public sealed partial class RequireSealedModuleSystem : EntitySystem
{
    [Dependency] private ToggleableModuleSystem _toggleableModule = default!;
    [Dependency] private SharedModSuitSystem _modSuit = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RequireSealedModuleComponent, ModuleToggleAttemptEvent>(OnToggleAttempt);

        SubscribeLocalEvent<RequireSealedModuleComponent, ModuleAddedContainerEvent>(OnAdded);
        SubscribeLocalEvent<RequireSealedModuleComponent, ModuleRemovedContainerEvent>(OnRemoved);

        SubscribeLocalEvent<RequireSealedModuleComponent, ModuleRelayedEvent<ModSuitContainerPartSealedEvent>>(OnSealed);
        SubscribeLocalEvent<RequireSealedModuleComponent, ModuleRelayedEvent<ModSuitContainerPartUnsealedEvent>>(OnUnsealed);
    }

    private void OnToggleAttempt(Entity<RequireSealedModuleComponent> ent, ref ModuleToggleAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (CanEnable(ent, args.Container))
            return;

        args.Cancel();
        args.Reason = "module-toggle-failed-sealed";
    }

    private void OnAdded(Entity<RequireSealedModuleComponent> ent, ref ModuleAddedContainerEvent args)
    {
        if (!ent.Comp.EnableOnSealed)
            return;

        if (!CanEnable(ent, args.Container))
            return;

        _toggleableModule.RaiseToggleEvents(ent.Owner, true, null);
    }

    private void OnRemoved(Entity<RequireSealedModuleComponent> ent, ref ModuleRemovedContainerEvent args)
    {
        if (CanEnable(ent, args.Container))
            return;

        _toggleableModule.RaiseToggleEvents(ent.Owner, false, null);
    }

    private void OnSealed(Entity<RequireSealedModuleComponent> ent, ref ModuleRelayedEvent<ModSuitContainerPartSealedEvent> args)
    {
        if (!ent.Comp.EnableOnSealed)
            return;

        if (!CanEnable(ent, args.ModuleOwner))
            return;

        _toggleableModule.RaiseToggleEvents(ent.Owner, true, null);
    }

    private void OnUnsealed(Entity<RequireSealedModuleComponent> ent, ref ModuleRelayedEvent<ModSuitContainerPartUnsealedEvent> args)
    {
        if (CanEnable(ent, args.ModuleOwner))
            return;

        _toggleableModule.RaiseToggleEvents(ent.Owner, false, null);
    }

    private bool CanEnable(Entity<RequireSealedModuleComponent> ent, EntityUid container)
    {
        var parts = _modSuit.GetDeployedParts(container);
        HashSet<ModSuitPartType> relevantParts = [];

        if (IsEnabled(container, ent.Comp.Parts, out var type))
            relevantParts.Add(type);

        foreach (var part in parts)
        {
            if (IsEnabled(part, ent.Comp.Parts, out type))
                relevantParts.Add(type);
        }

        if (ent.Comp.RequireAll)
            return relevantParts.Count == ent.Comp.Parts.Count;
        else
            return relevantParts.Any();
    }

    internal bool IsEnabled(EntityUid part, HashSet<ModSuitPartType> requiredParts, out ModSuitPartType type)
    {
        type = CompOrNull<ModSuitPartTypeComponent>(part)?.Type ?? ModSuitPartType.Other;
        if (!requiredParts.Contains(type))
            return false;

        return _modSuit.IsSealed(part);
    }
}
