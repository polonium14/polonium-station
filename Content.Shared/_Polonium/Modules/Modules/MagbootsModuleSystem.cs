using System.Diagnostics.CodeAnalysis;
using Content.Shared.Clothing;
using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit;

namespace Content.Shared._Polonium.Modules.Modules;

public sealed partial class MagbootsModuleSystem : EntitySystem
{
    [Dependency] private SharedMagbootsSystem _magboots = default!;
    [Dependency] private SharedModSuitSystem _modSuit = default!;
    [Dependency] private SharedModuleSystem _module = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MagbootsModuleComponent, ModuleEnabledEvent>(OnEnabled, after: [typeof(ToggleableComponentModSuitPartModuleSystem)]);
        SubscribeLocalEvent<MagbootsModuleComponent, ModuleDisabledEvent>(OnDisabled, after: [typeof(ToggleableComponentModSuitPartModuleSystem)]);
    }

    private void OnEnabled(Entity<MagbootsModuleComponent> ent, ref ModuleEnabledEvent args)
    {
        UpdateMagboots(ent, args.Container, true);
    }

    private void OnDisabled(Entity<MagbootsModuleComponent> ent, ref ModuleDisabledEvent args)
    {
        UpdateMagboots(ent, args.Container, false);
    }

    private void UpdateMagboots(Entity<MagbootsModuleComponent> module, EntityUid container, bool state)
    {
        if (!TryGetMagboots(module, container, out var magboots))
            return;

        if (_module.TryGetUser(module.Owner, out var user))
            _magboots.UpdateMagbootEffects(user.Value, magboots.Value, state);
    }

    private bool TryGetMagboots(Entity<MagbootsModuleComponent> module, EntityUid container, [NotNullWhen(true)] out Entity<MagbootsComponent>? boots)
    {
        if (TerminatingOrDeleted(module) || TerminatingOrDeleted(container))
        {
            boots = null;
            return false;
        }

        if (module.Comp.ModSuitPart is { } partType)
        {
            if (_modSuit.TryGetDeployedPart(container, partType, out var part))
            {
                boots = (part.Value, EnsureComp<MagbootsComponent>(part.Value));
                return true;
            }
        }

        boots = (container, EnsureComp<MagbootsComponent>(container));
        return true;
    }
}
