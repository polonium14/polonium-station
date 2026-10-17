using Content.Shared.Emp;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Network;

namespace Content.Shared._Polonium.Replicator;

public sealed partial class ReplicatorEmpSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ReplicatorModuleSystem _modules = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorEmpVulnerableComponent, EmpPulseEvent>(OnEmpPulse);
    }

    private void OnEmpPulse(Entity<ReplicatorEmpVulnerableComponent> ent, ref EmpPulseEvent args)
    {
        if (!_mobState.IsAlive(ent))
            return;

        args.Affected = true;
        _modules.Reveal(ent);

        if (_stun.TryUpdateParalyzeDuration(ent, ent.Comp.StunTime) && _net.IsServer)
            _popup.PopupEntity(Loc.GetString(ent.Comp.Popup), ent, ent, PopupType.LargeCaution);
    }
}
