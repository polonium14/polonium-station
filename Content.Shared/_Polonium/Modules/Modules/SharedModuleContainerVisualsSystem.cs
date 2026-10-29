using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using JetBrains.Annotations;

namespace Content.Shared._Polonium.Modules.Modules;

public abstract partial class SharedModuleContainerVisualsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedModuleSystem _module = default!;
    [Dependency] protected ToggleableModuleSystem ToggleableModule = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModuleContainerVisualsComponent, ModuleEnabledEvent>(OnEnabled);
        SubscribeLocalEvent<ModuleContainerVisualsComponent, ModuleDisabledEvent>(OnDisabled);
    }

    private void OnEnabled(Entity<ModuleContainerVisualsComponent> ent, ref ModuleEnabledEvent args)
    {
        UpdateVisuals(ent.AsNullable());
    }

    private void OnDisabled(Entity<ModuleContainerVisualsComponent> ent, ref ModuleDisabledEvent args)
    {
        UpdateVisuals(ent.AsNullable());
    }

    [PublicAPI]
    public void UpdateVisuals(Entity<ModuleContainerVisualsComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        if (GetVisualEntity(ent.Owner) is not { } visual)
            return;

        _appearance.SetData(visual, ModuleContainerVisualState.Toggled, ToggleableModule.IsToggled(ent.Owner));
    }

    [PublicAPI]
    public EntityUid? GetVisualEntity(EntityUid uid)
    {
        var ev = new ModuleContainerVisualsGetVisualEntityEvent();
        RaiseLocalEvent(uid, ev);

        return ev.Entity ?? _module.GetContainer(uid);
    }
}
