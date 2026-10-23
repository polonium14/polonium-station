using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Modules.Modules;

public sealed partial class ToggleableModuleSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedModuleSystem _module = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleableModuleComponent, ModuleRemovedContainerEvent>(OnRemoved);
        SubscribeLocalEvent<ToggleableModuleComponent, ModuleEnabledEvent>(OnEnabled);
        SubscribeLocalEvent<ToggleableModuleComponent, ModuleDisabledEvent>(OnDisabled);
    }

    private void OnRemoved(Entity<ToggleableModuleComponent> ent, ref ModuleRemovedContainerEvent args)
    {
        RaiseToggleEvents(ent!, false, null);
    }

    private void OnEnabled(Entity<ToggleableModuleComponent> ent, ref ModuleEnabledEvent args)
    {
        ent.Comp.Toggled = true;
        Dirty(ent);
    }

    private void OnDisabled(Entity<ToggleableModuleComponent> ent, ref ModuleDisabledEvent args)
    {
        ent.Comp.Toggled = false;
        Dirty(ent);
    }

    [PublicAPI]
    public bool IsToggled(Entity<ToggleableModuleComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return false;

        return ent.Comp.Toggled;
    }

    [PublicAPI]
    public void Toggle(Entity<ToggleableModuleComponent?> ent, EntityUid? user)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        Toggle(ent, !ent.Comp.Toggled, user);
    }

    [PublicAPI]
    public void Toggle(Entity<ToggleableModuleComponent?> ent, bool toggle, EntityUid? user)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        if (ent.Comp.Toggled == toggle)
            return;

        if (!_module.TryGetContainer(ent.Owner, out var container))
            return;

        var beforeEv = new ModuleToggleAttemptEvent(toggle, container.Value, user);
        RaiseLocalEvent(ent.Owner, beforeEv);

        if (beforeEv.Cancelled)
        {
            if (user == null || beforeEv.Reason == null)
                return;

            if (ent.Comp.NextMessage > _timing.CurTime)
                return;

            _popup.PopupEntity(Loc.GetString(beforeEv.Reason), ent.Owner, user.Value);
            ent.Comp.NextMessage = _timing.CurTime + ent.Comp.MessageDelay;
            return;
        }

        RaiseToggleEvents(ent, toggle, null);
    }

    [PublicAPI]
    public void RaiseToggleEvents(Entity<ToggleableModuleComponent?> ent, bool toggled, EntityUid? user)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        if (!_module.TryGetContainer(ent.Owner, out var container))
            return;

        if (toggled)
        {
            var ev = new ModuleEnabledEvent(container.Value, user);
            RaiseLocalEvent(ent.Owner, ev);
        }
        else
        {

            var ev = new ModuleDisabledEvent(container.Value, user);
            RaiseLocalEvent(ent.Owner, ev);
        }
    }
}
