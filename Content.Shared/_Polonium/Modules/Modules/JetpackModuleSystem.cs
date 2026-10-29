using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.PowerCell;
using Robust.Shared.Network;

namespace Content.Shared._Polonium.Modules.Modules;

public sealed partial class JetpackModuleSystem : EntitySystem
{
    [Dependency] private SharedJetpackSystem _jetpack = default!;
    [Dependency] private SharedModuleSystem _module = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JetpackModuleComponent, ModuleEnabledEvent>(OnEnabled);
        SubscribeLocalEvent<JetpackModuleComponent, ModuleDisabledEvent>(OnDisabled);

        SubscribeLocalEvent<JetpackModuleComponent, ModuleToggleAttemptEvent>(OnToggleAttempt);
    }

    private void OnEnabled(Entity<JetpackModuleComponent> ent, ref ModuleEnabledEvent args)
    {
        if (!CanEnable(ent))
            return;

        if (!_jetpack.CanEnableOnGrid(Transform(args.Container).GridUid))
            return;

        EnsureComp<JetpackComponent>(args.Container, out var jetpack);
        _jetpack.SetEnabled(args.Container, jetpack, true, args.User);
    }

    private void OnDisabled(Entity<JetpackModuleComponent> ent, ref ModuleDisabledEvent args)
    {
        if (!TryComp<JetpackComponent>(args.Container, out var jetpack))
            return;

        // Without the component the suit has no jetpack action of its own, so it only flies through the module.
        _jetpack.SetEnabled(args.Container, jetpack, false, args.User);

        if (_net.IsServer && jetpack.ToggleActionEntity is { } action)
            QueueDel(action);

        RemComp<JetpackComponent>(args.Container);
    }

    private void OnToggleAttempt(Entity<JetpackModuleComponent> ent, ref ModuleToggleAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (CanEnable(ent))
            return;

        // jetpack component shows its own warning
        args.Reason = null;
        args.Cancel();
    }

    internal bool CanEnable(Entity<JetpackModuleComponent> ent)
    {
        // we never intended to draw power so abort
        if (!HasComp<PowerDrainModuleComponent>(ent.Owner))
            return true;

        if (!_module.TryGetContainer(ent.Owner, out var container))
            return false;

        return _powerCell.HasDrawCharge(container.Value);
    }
}
