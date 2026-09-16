using System.Linq;
using Content.Server.Ame.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Wires;
using Content.Shared._Polonium.Tutorial.Actions;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Doors.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Lock;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Wires;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Tutorial;

public sealed partial class TutorialActionExecutor : EntitySystem
{
    [Dependency] private SharedAccessSystem _access = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private TutorialMentorSystem _mentor = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private SharedPoweredLightSystem _poweredLights = default!;
    [Dependency] private SharedPointLightSystem _pointLights = default!;
    [Dependency] private SharedEntityStorageSystem _storage = default!;
    [Dependency] private SharedStorageSystem _itemStorage = default!;
    [Dependency] private SharedWiresSystem _wires = default!;
    [Dependency] private WiresSystem _wiresServer = default!;
    [Dependency] private AmeControllerSystem _ame = default!;
    [Dependency] private MobStateSystem _mobs = default!;
    [Dependency] private TutorialNpcSystem _npcs = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private RotateToFaceSystem _rotateToFace = default!;
    [Dependency] private TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> EmagImmuneTag = "EmagImmune";

    public void ExecuteAll(EntityUid player, IReadOnlyList<TutorialAction> actions, bool instant = false)
    {
        foreach (var action in actions)
            Execute(player, action, instant);
    }

    public void Execute(EntityUid player, TutorialAction action, bool instant = false)
    {
        switch (action)
        {
            case GrantAccessAction grant:
                GrantAccess(player, grant.Tags);
                break;

            case SealAnchorAirlockAction seal:
                SealAirlock(player, seal.AnchorId);
                break;
            case UnlockAnchorAirlockAction unlock:
                SetBolt(player, unlock.AnchorId, false);
                break;
            case OpenAnchorAirlockAction open:
                OpenAirlock(player, open.AnchorId);
                break;

            case FaceDirectionAction face:
                _rotateToFace.TryFaceAngle(player, face.Direction.ToAngle());
                break;

            case SetLightsAction lights:
                SetLights(player, lights.AnchorId, lights.On);
                break;

            case PowerDeviceAction power:
                if (instant || power.Delay <= 0f)
                    SetPower(player, power.AnchorId, power.Powered);
                else
                    RunMaybeDelayed(power.Delay, () => SetPower(player, power.AnchorId, power.Powered));
                break;

            case SetPacifiedAction pacify:
                SetPacified(player, pacify.Pacified);
                break;

            case SpeakHolopadAction speak:
                if (!instant)
                    _mentor.Enqueue(player, speak.Lines);
                break;

            case SpawnAtAnchorAction spawn:
                SpawnAt(player, spawn);
                break;

            case ClaimNearbyMobAction claim:
                ClaimNearby(player, claim);
                break;

            case MeteorWindowAction meteor:
                if (instant)
                    BreakWindow(player, meteor.WindowAnchor, meteor.Damage);
                else
                    StartMeteor(player, meteor);
                break;

            case ClampAmeInjectionAction clamp:
                ClampAme(player, clamp.AnchorId, clamp.Clamp);
                break;

            case OpenStorageAction open:
                if (!open.RecoveryOnly)
                    OpenStorage(player, open.AnchorId);
                break;

            case SetAnchorAccessAction setAccess:
                SetAnchorAccess(player, setAccess);
                break;

            case SetDisarmProneAction prone:
                SetDisarmProne(player, prone);
                break;

            case BonkNpcAction bonk:
                StartBonk(player, bonk, instant);
                break;

            case DrainAnchorSolutionAction drain:
                DrainSolution(player, drain);
                break;

            case DrainPrototypeSolutionAction drainProto:
                DrainPrototypeSolution(player, drainProto);
                break;

            default:
                Log.Warning($"Tutorial: no handler for action type {action.GetType().Name}");
                break;
        }
    }

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

    public void Teleport(EntityUid player, string anchorId)
    {
        if (!TryGetAnchor(player, anchorId, out var target))
        {
            Log.Warning($"Tutorial: teleport — anchor '{anchorId}' missing");
            return;
        }

        _transform.SetCoordinates(player, Transform(target).Coordinates);
    }

    public void TryRecover(EntityUid player, TutorialStepPrototype step)
    {
        foreach (var action in step.OnEnter)
        {
            switch (action)
            {
                case ClaimNearbyMobAction claim:
                    ClaimNearby(player, claim);
                    break;
                case SpawnAtAnchorAction spawn:
                    SpawnAt(player, spawn);
                    break;
                case OpenStorageAction open:
                    OpenStorage(player, open.AnchorId);
                    break;
                case MeteorWindowAction meteor:
                    BreakWindow(player, meteor.WindowAnchor, meteor.Damage);
                    break;
            }
        }
    }

    private void SpawnAt(EntityUid player, SpawnAtAnchorAction spawn)
    {
        if (!string.IsNullOrWhiteSpace(spawn.AssignAnchorId)
            && TryGetAnchor(player, spawn.AssignAnchorId, out var existing)
            && !Deleted(existing))
            return;
        if (!TryGetAnchor(player, spawn.AnchorId, out var at))
        {
            Log.Warning($"Tutorial: spawn — anchor '{spawn.AnchorId}' missing");
            return;
        }

        var spawned = Spawn(spawn.Prototype, Transform(at).Coordinates);

        if (spawn.IntoStorage && !TryPutInside(spawned, at))
            Log.Warning($"Tutorial: spawn - could not put '{spawn.Prototype}' inside anchor '{spawn.AnchorId}'");

        BindSpawned(player, spawned, spawn.AssignAnchorId, spawn.TutorialNpc, spawn.PreventDeath);
    }

    private void ClaimNearby(EntityUid player, ClaimNearbyMobAction claim)
    {
        var assign = claim.AssignAnchorId ?? claim.AnchorId;
        if (TryGetAnchor(player, assign, out var already)
            && HasComp<MobStateComponent>(already)
            && !Deleted(already)
            && (!claim.MarkDeadPatient || _mobs.IsDead(already)))
            return;

        if (!TryGetAnchor(player, claim.AnchorId, out var at))
        {
            Log.Warning($"Tutorial: claim — anchor '{claim.AnchorId}' missing");
            return;
        }

        if (!TryPickClaimTarget(player, at, claim, out var mob))
        {
            Log.Warning($"Tutorial: claim — no mob near '{claim.AnchorId}'");
            return;
        }

        BindSpawned(player, mob, assign, tutorialNpc: true, claim.PreventDeath, claim.MarkDeadPatient);
    }

    private bool TryPickClaimTarget(EntityUid player, EntityUid at, ClaimNearbyMobAction claim, out EntityUid mob)
    {
        mob = default;
        if (!TryComp(player, out TransformComponent? px) || px.GridUid is not { } grid)
            return false;

        var atPos = _transform.GetWorldPosition(at);
        var rangeSq = claim.Range * claim.Range;
        EntityUid? best = null;
        var bestScore = float.MaxValue;

        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var tx))
        {
            if (uid == player || uid == at || tx.GridUid != grid || Deleted(uid))
                continue;

            if (HasComp<TutorialSessionComponent>(uid) || HasComp<ActorComponent>(uid))
                continue;

            var dist = (_transform.GetWorldPosition(tx) - atPos).LengthSquared();
            var score = dist;

            if (claim.MarkDeadPatient)
                score *= _mobs.IsDead(uid) ? 0.01f : 8f;

            if (score >= bestScore)
                continue;

            // prefer in-range, but keep a grid-wide fallback
            if (dist > rangeSq && best is not null && bestScore <= rangeSq)
                continue;

            best = uid;
            bestScore = score;
        }

        if (best is not { } found)
            return false;

        mob = found;
        return true;
    }

    private void BindSpawned(EntityUid player, EntityUid spawned, string? assignId, bool tutorialNpc, bool preventDeath, bool markDeadPatient = false)
    {
        if (tutorialNpc)
        {
            var npc = EnsureComp<TutorialNpcComponent>(spawned);
            npc.PreventDeath = preventDeath;
            _npcs.KeepAwake(spawned);
        }

        if (markDeadPatient)
        {
            var patient = EnsureComp<TutorialPatientComponent>(spawned);
            patient.SpawnedDead = true;
        }

        if (string.IsNullOrWhiteSpace(assignId))
            return;

        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return;

        var anchor = EnsureComp<TutorialAnchorComponent>(spawned);
        anchor.AnchorId = assignId;
        Dirty(spawned, anchor);
        session.Anchors[assignId] = spawned;
    }

    private void BreakWindow(EntityUid player, string anchorId, float amount)
    {
        if (!TryGetAnchor(player, anchorId, out var uid))
            return;

        var coords = Transform(uid).Coordinates;
        Spawn("EffectSparks", coords);
        Spawn("EffectTeslaSparks", coords);

        // a small bang sells the impact far better than the window quietly falling apart.
        // no tile breaking - the hull stays, only the glass is supposed to go
        _explosion.QueueExplosion(
            _transform.GetMapCoordinates(uid),
            "Default",
            totalIntensity: 10f,
            slope: 8f,
            maxTileIntensity: 3f,
            cause: player,
            maxTileBreak: 0,
            canCreateVacuum: false);

        var spec = new DamageSpecifier
        {
            DamageDict = { ["Structural"] = amount, ["Blunt"] = amount }
        };

        _damageable.ChangeDamage(uid, spec, ignoreResistances: true);
    }

    private void StartMeteor(EntityUid player, MeteorWindowAction meteor)
    {
        if (!TryComp<TutorialSessionComponent>(player, out var session) || session.CurrentStep is not { } step)
            return;

        for (var i = meteor.Countdown; i >= 1; i--)
        {
            var n = i;
            Timer.Spawn(TimeSpan.FromSeconds(meteor.Countdown - i), () =>
            {
                if (!Exists(player) || !TryComp<TutorialSessionComponent>(player, out var live))
                    return;

                if (live.CurrentStep != step)
                    return;

                _mentor.SpeakNow(player, Loc.GetString("tutorial-holopad-countdown", ("n", n)));
            });
        }

        Timer.Spawn(TimeSpan.FromSeconds(meteor.Countdown), () =>
        {
            if (!Exists(player) || !TryComp<TutorialSessionComponent>(player, out var live))
                return;

            if (live.CurrentStep != step)
                return;

            _mentor.Enqueue(player, new LocId[] { "tutorial-holopad-meteor-impact" });
            BreakWindow(player, meteor.WindowAnchor, meteor.Damage);
        });
    }

    private void ClampAme(EntityUid player, string anchorId, bool clamp)
    {
        if (!TryGetAnchor(player, anchorId, out var uid))
            return;

        EnsureComp<TutorialAmeLimitComponent>(uid);
        if (clamp)
            _ame.ClampInjectionToSafeLimit(uid);
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

    private void SetDisarmProne(EntityUid player, SetDisarmProneAction prone)
    {
        foreach (var uid in AnchorsNamed(player, prone.AnchorId))
        {
            if (prone.Prone)
                EnsureComp<DisarmProneComponent>(uid);
            else
                RemComp<DisarmProneComponent>(uid);
        }
    }

    /// <summary>
    /// Hands the patient over to <see cref="TutorialNpcSystem"/>, which walks him to the table and lets
    /// the climb go wrong. Replaying a finished room skips the show and just leaves him down.
    /// </summary>
    private void StartBonk(EntityUid player, BonkNpcAction bonk, bool instant)
    {
        if (!TryGetAnchor(player, bonk.NpcAnchorId, out var npc)
            || !TryGetAnchor(player, bonk.TableAnchorId, out var table))
            return;

        if (instant)
        {
            _npcs.Kill(npc, bonk.Blunt);
            return;
        }

        var run = EnsureComp<TutorialNpcBonkComponent>(npc);
        run.Table = table;
        run.Blunt = bonk.Blunt;
        run.Stage = TutorialBonkStage.Waiting;
        run.NextAt = _timing.CurTime + TimeSpan.FromSeconds(bonk.Delay);
    }

    /// <summary>Tips the excess back out of something the trainee overfilled.</summary>
    private void DrainSolution(EntityUid player, DrainAnchorSolutionAction drain)
    {
        var keep = FixedPoint2.New(MathF.Max(drain.Keep, 0f));

        foreach (var uid in AnchorsNamed(player, drain.AnchorId))
        {
            foreach (var soln in SolutionsOf(uid, drain.Solution))
            {
                // RemoveReagent swaps entries around, so work off a snapshot
                foreach (var entry in soln.Comp.Solution.Contents.ToArray())
                {
                    if (entry.Quantity > keep)
                        _solution.RemoveReagent(soln, entry.Reagent, entry.Quantity - keep);
                }
            }
        }
    }

    /// <summary>Same rescue for something that came out of a vending machine and has no anchor.</summary>
    private void DrainPrototypeSolution(EntityUid player, DrainPrototypeSolutionAction drain)
    {
        var keep = FixedPoint2.New(MathF.Max(drain.Keep, 0f));

        foreach (var uid in Reachable(player, drain.Range))
        {
            if (MetaData(uid).EntityPrototype?.ID != drain.Prototype.Id)
                continue;

            foreach (var soln in SolutionsOf(uid, drain.Solution))
            {
                foreach (var entry in soln.Comp.Solution.Contents.ToArray())
                {
                    if (drain.Reagent is { } only && entry.Reagent.Prototype != only.Id)
                        continue;

                    if (entry.Quantity > keep)
                        _solution.RemoveReagent(soln, entry.Reagent, entry.Quantity - keep);
                }
            }
        }
    }

    private HashSet<EntityUid> Reachable(EntityUid player, float range)
    {
        var seen = new HashSet<EntityUid>();

        foreach (var uid in _lookup.GetEntitiesInRange(Transform(player).Coordinates, range))
            CollectInto(uid, seen);

        foreach (var held in _hands.EnumerateHeld(player))
            CollectInto(held, seen);

        var slots = _inventory.GetSlotEnumerator(player);
        while (slots.NextItem(out var item))
            CollectInto(item, seen);

        return seen;
    }

    private void CollectInto(EntityUid uid, HashSet<EntityUid> seen)
    {
        if (!seen.Add(uid) || !TryComp<ContainerManagerComponent>(uid, out var containers))
            return;

        foreach (var container in _containers.GetAllContainers(uid, containers))
        {
            foreach (var child in container.ContainedEntities)
                CollectInto(child, seen);
        }
    }

    private IEnumerable<Entity<SolutionComponent>> SolutionsOf(EntityUid uid, string? name)
    {
        if (name is { } wanted)
        {
            if (_solution.TryGetSolution(uid, wanted, out var single, out _) && single is { } found)
                yield return found;

            yield break;
        }

        foreach (var (_, soln) in _solution.EnumerateSolutions((uid, null)))
            yield return soln;
    }

    private bool TryGetAnchor(EntityUid player, string anchorId, out EntityUid uid)
    {
        uid = default;
        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return false;

        if (session.Anchors.TryGetValue(anchorId, out uid) && !Deleted(uid))
            return true;

        foreach (var other in AnchorsNamed(player, anchorId))
        {
            uid = other;
            return true;
        }

        return false;
    }

    private IEnumerable<(string Id, EntityUid Uid)> AnchorsOf(EntityUid player)
    {
        if (!TryComp<TutorialSessionComponent>(player, out var session))
            yield break;

        foreach (var pair in session.Anchors)
            yield return (pair.Key, pair.Value);
    }

    private IEnumerable<EntityUid> AnchorsNamed(EntityUid player, string anchorId)
    {
        if (!TryComp(player, out TransformComponent? xform) || xform.GridUid is not { } grid)
            yield break;

        var query = EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anchor, out var ax))
        {
            if (anchor.AnchorId == anchorId && ax.GridUid == grid && !Deleted(uid))
                yield return uid;
        }
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

    // closets and crates are EntityStorage, folders and boxes are plain item Storage
    private bool TryPutInside(EntityUid item, EntityUid container)
    {
        if (HasComp<EntityStorageComponent>(container))
            return _storage.Insert(item, container);

        return _itemStorage.Insert(container, item, out _, playSound: false);
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

    private void SetPacified(EntityUid player, bool pacified)
    {
        if (pacified)
            EnsureComp<PacifiedComponent>(player);
        else
            RemComp<PacifiedComponent>(player);
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
