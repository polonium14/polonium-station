using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;

namespace Content.Shared._Polonium.Modules.Modules;

public sealed partial class ToggleableComponentUserModuleSystem : EntitySystem
{
    [Dependency] private SharedModuleSystem _module = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleableComponentUserModuleComponent, ModuleEnabledEvent>(OnEnabled);
        SubscribeLocalEvent<ToggleableComponentUserModuleComponent, ModuleDisabledEvent>(OnDisabled);
    }

    private void OnEnabled(Entity<ToggleableComponentUserModuleComponent> ent, ref ModuleEnabledEvent args)
    {
        if (!_module.TryGetUser(ent.Owner, out var user))
            return;

        EntityManager.AddComponents(user.Value, ent.Comp.Components);
    }

    private void OnDisabled(Entity<ToggleableComponentUserModuleComponent> ent, ref ModuleDisabledEvent args)
    {
        if (!_module.TryGetUser(ent.Owner, out var user))
            return;

        EntityManager.RemoveComponents(user.Value, ent.Comp.Components);
    }
}
