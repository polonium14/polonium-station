using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Alert;
using Content.Shared.Body;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;

public sealed partial class TraumaSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private WoundSystem _wound = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private ConsciousnessSystem _consciousness = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedVirtualItemSystem _virtual = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private AlertsSystem _alert = default!;

    private string _brokenBonesAlertId = "BrokenBones";
    private string _legsCollapsedAlertId = "LegsCollapsed";
    public override void Initialize()
    {
        base.Initialize();
        InitProcess();
        InitBones();
        InitOrgans();
        InitializeCleanup();
    }
}
