using System.Linq;
using Content.Server.Power.Components;
using Content.Server.Wires;
using Content.Shared._Polonium.Tutorial.Actions;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Lock;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage.Components;
using Content.Shared.Tag;
using Content.Shared.Wires;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// Doors, locks, lights, power and who is allowed through what. A room is opened one machine
/// at a time, so a trainee can never walk into a lesson that has not been set up yet.
/// </summary>
public sealed partial class TutorialActionExecutor
{
    [Dependency] private SharedAccessSystem _access = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private SharedPoweredLightSystem _poweredLights = default!;
    [Dependency] private SharedPointLightSystem _pointLights = default!;
    [Dependency] private SharedWiresSystem _wires = default!;
    [Dependency] private WiresSystem _wiresServer = default!;
    [Dependency] private TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> EmagImmuneTag = "EmagImmune";

    /// <summary>
    /// Anchors that must keep running off the grid, because a step is about wiring them up.
    /// </summary>
    private static readonly HashSet<string> UnpoweredOnPurpose = new()
    {
        "room15_build_bulb",
        // the example branch is lit by the engine the trainee just started, not for free
        "room16_example_consumer_bulb",
    };

    /// <summary>
    /// The linear map has one debug APC and almost no cable under the machines, so microwaves,
    /// vendors, the disposal unit and the stasis bed would all sit dead. Cut them loose from the
    /// grid instead of wiring the whole station - a step that wants something dead still can,
    /// PowerDisabled beats NeedsPower.
    /// </summary>
    public void PowerAllDevices(EntityUid player)
    {
        foreach (var (id, uid) in AnchorsOf(player))
        {
            if (UnpoweredOnPurpose.Contains(id))
                continue;

            if (!TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
                continue;

            _power.SetNeedsPower(uid, false, receiver);
        }
    }

    public void BoltAllAirlocks(EntityUid player)
    {
        foreach (var (id, uid) in AnchorsOf(player))
        {
            if (!id.Contains("airlock", StringComparison.OrdinalIgnoreCase))
                continue;

            // APC starts empty and spawn is before the first power tick
            if (TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
            {
                _power.SetNeedsPower(uid, false, receiver);
                _power.SetPowerDisabled(uid, false, receiver);
            }

            // SnapClosed first. Bolting a door that is still Closing cancels the close.
            if (TryComp<DoorComponent>(uid, out var door))
                _door.SnapClosed(uid, door, playSound: false);

            SetBolt(player, id, true);
        }
    }

    private void OpenStorage(EntityUid player, string anchorId)
    {
        foreach (var uid in AnchorsNamed(player, anchorId))
        {
            if (TryComp<LockComponent>(uid, out var lockComp) && lockComp.Locked)
                _lock.Unlock(uid, player, lockComp);

            _storage.TryOpenStorage(player, uid);
        }
    }

    private void SetAnchorAccess(EntityUid player, SetAnchorAccessAction set)
    {
        var any = false;
        foreach (var uid in AnchorsNamed(player, set.AnchorId))
        {
            // airlocks check the door electronics inside them, their own reader is ignored
            if (!_accessReader.GetMainAccessReader(uid, out var reader))
                continue;

            any = true;
            _accessReader.TrySetAccesses(reader.Value, set.Access);

            // a crate the map saved unlocked would never ask for the access at all
            if (TryComp<LockComponent>(uid, out var lockComp)
                && !lockComp.Locked
                && !(TryComp<EntityStorageComponent>(uid, out var storage) && storage.Open))
                _lock.Lock(uid, null, lockComp);
        }

        if (!any)
            Log.Warning($"Tutorial: access - anchor '{set.AnchorId}' has no access reader");
    }

    /// <summary>
    /// Point of no return. Snap shut first, then bolt. Bolting a still-closing door
    /// cancels the close and leaves it bolted open.
    /// </summary>
    private void SealAirlock(EntityUid player, string anchorId)
    {
        var any = false;
        foreach (var doorUid in AnchorsNamed(player, anchorId))
        {
            any = true;

            _power.SetNeedsPower(doorUid, false);
            _power.SetPowerDisabled(doorUid, false);

            if (TryComp<DoorComponent>(doorUid, out var door))
            {
                door.CanPry = false;
                _door.SnapClosed(doorUid, door);
                Dirty(doorUid, door);
            }

            if (TryComp<DoorBoltComponent>(doorUid, out var doorBolt))
                _door.SetBoltsDown((doorUid, doorBolt), true, force: true);

            if (TryComp<WiresPanelComponent>(doorUid, out var panel))
                _wires.TogglePanel(doorUid, panel, false);

            var security = EnsureComp<WiresPanelSecurityComponent>(doorUid);
            _wiresServer.SetWiresPanelSecurity(doorUid, security, new WiresPanelSecurityEvent(null, false));

            EnsureComp<TutorialSealedComponent>(doorUid);
            _tags.AddTag(doorUid, EmagImmuneTag);
        }

        if (!any)
            Log.Warning($"Tutorial: seal - anchor '{anchorId}' not resolved");
    }

    private void SetLights(EntityUid player, string anchorId, bool on)
    {
        var any = false;
        foreach (var uid in AnchorsNamed(player, anchorId))
        {
            // these fixtures are ordinary lamps standing in a room with no apc of its own, so
            // their power is ours to cut - nothing here depends on a cable being intact
            if (TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
            {
                _power.SetNeedsPower(uid, false, receiver);
                _power.SetPowerDisabled(uid, !on, receiver);
                any = true;
            }

            if (TryComp<PoweredLightComponent>(uid, out var powered))
            {
                _poweredLights.SetState(uid, on, powered);
                any = true;
            }

            // bare sprite-plus-point-light fixtures have no receiver at all, killing the light
            // is the only way to darken those
            if (TryComp<PointLightComponent>(uid, out var point))
            {
                _pointLights.SetEnabled(uid, on, point);
                any = true;
            }
        }

        if (!any)
            Log.Warning($"Tutorial: lights - anchor '{anchorId}' resolved nothing to switch");
    }

    private void SetBolt(EntityUid player, string anchorId, bool bolt)
    {
        var any = false;
        foreach (var doorUid in AnchorsNamed(player, anchorId))
        {
            if (!TryComp<DoorBoltComponent>(doorUid, out var doorBolt))
                continue;

            _door.SetBoltsDown((doorUid, doorBolt), bolt, force: true);
            any = true;
        }

        if (!any)
            Log.Warning($"Tutorial: bolt — anchor '{anchorId}' not resolved");
    }

    private void OpenAirlock(EntityUid player, string anchorId)
    {
        SetBolt(player, anchorId, false);

        foreach (var doorUid in AnchorsNamed(player, anchorId))
        {
            // straight to opening, TryOpen would ask the power and the access this is meant to bypass
            if (TryComp<DoorComponent>(doorUid, out var door) && door.State is not (DoorState.Open or DoorState.Opening))
                _door.StartOpening(doorUid, door);
        }
    }

    private void SetPower(EntityUid player, string anchorId, bool powered)
    {
        if (!TryGetAnchor(player, anchorId, out var deviceUid))
        {
            Log.Warning($"Tutorial: PowerDeviceAction — anchor '{anchorId}' not resolved");
            return;
        }

        if (!TryComp<ApcPowerReceiverComponent>(deviceUid, out var receiver))
        {
            Log.Warning($"Tutorial: PowerDeviceAction — {ToPrettyString(deviceUid)} has no ApcPowerReceiver");
            return;
        }

        // the map runs off one debug apc that is empty on the first tick, so powering something
        // on has to mean "stop caring about the grid". powering off still works through
        // PowerDisabled, which wins over NeedsPower - the crowbar door depends on that
        if (powered)
            _power.SetNeedsPower(deviceUid, false, receiver);

        _power.SetPowerDisabled(deviceUid, !powered, receiver);
    }

    private void GrantAccess(EntityUid player, IReadOnlySet<ProtoId<AccessLevelPrototype>> tags)
    {
        if (tags.Count == 0)
            return;

        // stamp the mob itself, so doors keep working even if the pda gets dropped.
        var self = EnsureComp<AccessComponent>(player);
        var selfTags = self.Tags.ToHashSet();
        selfTags.UnionWith(tags);
        _access.TrySetTags(player, selfTags, self);

        var sources = CollectAccessSources(player);
        sources.Remove(player);

        foreach (var source in sources)
        {
            if (!TryComp<AccessComponent>(source, out var access))
                continue;

            var current = access.Tags.ToHashSet();
            current.UnionWith(tags);
            _access.TrySetTags(source, current, access);
        }
    }

    private HashSet<EntityUid> CollectAccessSources(EntityUid player)
    {
        var result = new HashSet<EntityUid>();

        if (_accessReader.FindAccessItemsInventory(player, out var invItems))
        {
            foreach (var item in invItems)
            {
                if (HasComp<AccessComponent>(item))
                    result.Add(item);
            }
        }

        if (result.Count > 0)
            return result;

        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return result;

        foreach (var (_, uid) in session.Anchors)
        {
            if (HasComp<AccessComponent>(uid))
                result.Add(uid);
        }

        if (result.Count > 0)
            Log.Debug($"Tutorial: no access in inventory, using {result.Count} anchor(s) as fallback");

        return result;
    }
}
