using System.Linq;
using Content.Server.Ame.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Wires;
using Content.Shared._Polonium.Tutorial.Actions;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Administration.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// Props and the people playing them: spawning what a step needs, handing a mob its part and
/// putting back whatever the previous trainee broke, ate or dropped down a disposal.
/// </summary>
public sealed partial class TutorialActionExecutor
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedStorageSystem _itemStorage = default!;
    [Dependency] private AmeControllerSystem _ame = default!;
    [Dependency] private MobStateSystem _mobs = default!;
    [Dependency] private TutorialNpcSystem _npcs = default!;
    [Dependency] private TutorialConfinementSystem _confinement = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ExplosionSystem _explosion = default!;

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
                case ConfineAnchorAction confine:
                    Confine(player, confine);
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
            _npcs.SatiateAndIdle(spawned);

            // a patient meant to be brought back must not start decomposing while the trainee reads
            // the holopad - a rotten body refuses the defibrillator for good
            if (!markDeadPatient)
                RemComp<PerishableComponent>(spawned);
        }

        if (markDeadPatient)
        {
            var patient = EnsureComp<TutorialPatientComponent>(spawned);
            patient.SpawnedDead = true;
            Dirty(spawned, patient);
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

    private void Confine(EntityUid player, ConfineAnchorAction confine)
    {
        var doorways = new List<EntityUid>();
        foreach (var id in confine.Doorways)
        {
            doorways.AddRange(AnchorsNamed(player, id));
        }

        // the map spawner shares the patient's anchor and stays put anyway
        foreach (var uid in AnchorsNamed(player, confine.AnchorId).Where(HasComp<MobStateComponent>))
        {
            _confinement.Confine(uid, doorways, confine.Popup);
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

    // closets and crates are EntityStorage, folders and boxes are plain item Storage
    private bool TryPutInside(EntityUid item, EntityUid container)
    {
        if (HasComp<EntityStorageComponent>(container))
            return _storage.Insert(item, container);

        return _itemStorage.Insert(container, item, out _, playSound: false);
    }

    private void SetPacified(EntityUid player, bool pacified)
    {
        if (pacified)
            EnsureComp<PacifiedComponent>(player);
        else
            RemComp<PacifiedComponent>(player);
    }
}
