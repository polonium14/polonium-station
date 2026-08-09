// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Power.Components;
using Content.Shared._Polonium.Tutorial.Actions;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Tutorial;

/// <summary>Handles side-effects for tutorial steps — access changes, doors, power, etc.</summary>
public sealed class TutorialActionExecutor : EntitySystem
{
    [Dependency] private readonly SharedAccessSystem _access = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly SharedAirlockSystem _airlock = default!;

    public void ExecuteAll(EntityUid player, IReadOnlyList<TutorialAction> actions)
    {
        foreach (var action in actions)
            Execute(player, action);
    }

    public void Execute(EntityUid player, TutorialAction action)
    {
        switch (action)
        {
            case GrantAccessAction grant:
                ModifyAccess(player, grant.Tags, addNotRemove: true);
                break;

            case RevokeAccessAction revoke:
                ModifyAccess(player, revoke.Tags, addNotRemove: false);
                break;

            case BoltDoorAction bolt:
                RunMaybeDelayed(bolt.Delay, () => SetBolt(player, bolt.AnchorId, bolt.Bolt));
                break;

            case CloseDoorAction close:
                ForceCloseDoor(player, close.AnchorId);
                break;

            case PowerDeviceAction power:
                RunMaybeDelayed(power.Delay, () => SetPower(player, power.AnchorId, power.Powered));
                break;

            default:
                Log.Warning($"Tutorial: no handler for action type {action.GetType().Name}");
                break;
        }
    }

    private bool TryGetAnchor(EntityUid player, string anchorId, out EntityUid uid)
    {
        uid = default;
        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return false;

        return session.Anchors.TryGetValue(anchorId, out uid);
    }

    private static void RunMaybeDelayed(float delaySeconds, Action action)
    {
        if (delaySeconds <= 0f)
        {
            action();
            return;
        }

        Timer.Spawn(TimeSpan.FromSeconds(delaySeconds), action);
    }

    private void SetBolt(EntityUid player, string anchorId, bool bolt)
    {
        if (!TryGetAnchor(player, anchorId, out var doorUid))
        {
            Log.Warning($"Tutorial: BoltDoorAction — anchor '{anchorId}' not resolved");
            return;
        }

        if (!TryComp<DoorBoltComponent>(doorUid, out var doorBolt))
        {
            Log.Warning($"Tutorial: BoltDoorAction — {ToPrettyString(doorUid)} has no DoorBoltComponent");
            return;
        }

        _door.SetBoltsDown((doorUid, doorBolt), bolt);
    }

    private void ForceCloseDoor(EntityUid player, string anchorId)
    {
        if (!TryGetAnchor(player, anchorId, out var doorUid))
        {
            Log.Warning($"Tutorial: CloseDoorAction — anchor '{anchorId}' not resolved");
            return;
        }

        // Kill the safety bumper for the close call so the door shuts even with someone in the doorway.
        var hadSafety = TryComp<AirlockComponent>(doorUid, out var airlock) && airlock.Safety;
        if (hadSafety)
            _airlock.SetSafety(airlock!, false);

        _door.TryClose(doorUid);

        if (hadSafety)
            _airlock.SetSafety(airlock!, true);
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

        receiver.PowerDisabled = !powered;
    }

    private void ModifyAccess(EntityUid player, IReadOnlySet<ProtoId<AccessLevelPrototype>> tags, bool addNotRemove)
    {
        if (tags.Count == 0)
            return;

        var sources = CollectAccessSources(player);

        if (sources.Count == 0)
        {
            Log.Warning($"Tutorial: {ToPrettyString(player)} has no access sources anywhere, " +
                        $"can't {(addNotRemove ? "grant" : "revoke")} [{string.Join(", ", tags)}]");
            return;
        }

        var modified = 0;
        foreach (var source in sources)
        {
            if (!TryComp<AccessComponent>(source, out var access))
                continue;

            var current = access.Tags.ToHashSet();
            if (addNotRemove)
                current.UnionWith(tags);
            else
                current.ExceptWith(tags);

            _access.TrySetTags(source, current, access);
            modified++;
        }

        Log.Debug($"Tutorial: {(addNotRemove ? "granted" : "revoked")} [{string.Join(", ", tags)}] " +
                  $"on {modified} source(s) for {ToPrettyString(player)}");
    }

    /// <summary>
    /// Inventory first. If the player has nothing with AccessComponent on them
    /// (e.g. dropped their PDA), fall back to anchored entities from the session that have AccessComponent
    /// </summary>
    private HashSet<EntityUid> CollectAccessSources(EntityUid player)
    {
        var result = new HashSet<EntityUid>();

        // normal path — whatever the player is holding / wearing
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

        // fallback - scan tutorial anchors for something with AccessComponent
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
