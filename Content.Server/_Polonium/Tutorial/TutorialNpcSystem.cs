using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Physics.Controllers;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Buckle;
using Content.Shared.Climbing.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Content.Shared.SSDIndicator;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Tutorial;

public sealed partial class TutorialNpcSystem : EntitySystem
{
    // a stock monkey fluffs one table vault in four, this one fluffs every single time
    private static readonly EntProtoId ClumsyEffect = "StatusEffectClumsyGuaranteed";

    // how far past the death threshold he goes. one zap takes 40 asphyxiation off, which leaves him
    // 35 under it: enough headroom that crit suffocation does not kill him again before the medipen,
    // and little enough that one medipen of epinephrine lifts him out of crit
    private const float LethalMargin = 5f;

    /// <summary>Asphyxiation a second, on top of what his lungs manage on their own.</summary>
    private const float CatchBreathRate = 5f;

    private const float ArriveDistance = 0.2f;
    private static readonly TimeSpan WalkTimeout = TimeSpan.FromSeconds(15);
    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    private static readonly SatiationValue Overfed = "Overfed";
    private static readonly SatiationValue Overhydrated = "Overhydrated";

    // a beat to stop before he climbs - arriving at full speed breaks the climb do-after on move
    private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(0.6);
    private static readonly TimeSpan ClimbTimeout = TimeSpan.FromSeconds(4);

    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MoverController _mover = default!;
    [Dependency] private ClimbSystem _climb = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private PullingSystem _pulling = default!;

    private static readonly ProtoId<HTNCompoundPrototype> IdleTask = "IdleCompound";
    private static readonly ProtoId<HTNCompoundPrototype> RuminantTask = "RuminantCompound";
    private static readonly ProtoId<HTNCompoundPrototype> RuminantHostileTask = "RuminantHostileCompound";

    public override void Initialize()
    {
        // input has to be down before the mover reads it this tick
        UpdatesBefore.Add(typeof(SharedPhysicsSystem));

        SubscribeLocalEvent<TutorialNpcComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<TutorialInedibleComponent, IngestibleEvent>(OnIngestible);
    }

    public override void Update(float frameTime)
    {
        CatchBreath(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<TutorialNpcBonkComponent>();
        while (query.MoveNext(out var uid, out var bonk))
        {
            if (TerminatingOrDeleted(bonk.Table))
            {
                SetWalk(uid, Vector2.Zero);
                RemCompDeferred<TutorialNpcBonkComponent>(uid);
                continue;
            }

            switch (bonk.Stage)
            {
                case TutorialBonkStage.Waiting:
                    if (now < bonk.NextAt)
                        break;

                    // a trainee who tucked him into a bed would otherwise keep him strapped there
                    _buckle.Unbuckle(uid, null);
                    bonk.Target = BesideTable(uid, bonk.Table);
                    bonk.Stage = TutorialBonkStage.Walking;
                    bonk.NextAt = now + WalkTimeout;
                    break;

                case TutorialBonkStage.Walking:
                    Walk(uid, bonk, now);
                    break;

                case TutorialBonkStage.Settling:
                    if (now < bonk.NextAt)
                        break;

                    // the stock climb, do-after and all. the clumsy status turns its finish into the bonk
                    _statusEffects.TrySetStatusEffectDuration(uid, ClumsyEffect, ClimbTimeout * 2);
                    if (!_climb.TryClimb(uid, uid, bonk.Table, out _))
                        Bonk(uid, bonk);
                    else
                    {
                        bonk.Stage = TutorialBonkStage.Climbing;
                        bonk.NextAt = now + ClimbTimeout;
                    }
                    break;

                case TutorialBonkStage.Climbing:
                    // the bonk paralyses him, that is the cue. a climb that got interrupted still ends in the table
                    if (!HasComp<KnockedDownComponent>(uid) && now < bonk.NextAt)
                        break;

                    Bonk(uid, bonk);
                    break;
            }
        }
    }

    /// <summary>
    /// Stock lungs wash out one point of asphyxiation every two seconds. A patient dragged back from
    /// the far side of the death threshold carries over a hundred of it, and waiting that off is many
    /// minutes of standing around. Once he is out of crit and breathing on his own it goes ten times
    /// faster - while he is still under, the clock runs at the stock rate and he can be lost again.
    /// </summary>
    private void CatchBreath(float frameTime)
    {
        var heal = new DamageSpecifier();
        var query = EntityQueryEnumerator<TutorialNpcComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out _, out var damageable))
        {
            if (!_mobState.IsAlive(uid))
                continue;

            if (_damageable.GetPositiveDamage((uid, damageable)).DamageDict.GetValueOrDefault(Asphyxiation) <= 0)
                continue;

            heal.DamageDict[Asphyxiation] = -CatchBreathRate * frameTime;
            _damageable.ChangeDamage(uid, heal, ignoreResistances: true);
        }
    }

    /// <summary>
    /// Steps him along on his own legs, the same way NPC steering does: movement input every tick,
    /// no pathfinding. The table is a few tiles off across open floor, and pathfinding on the tutorial
    /// grid never produced a path for him.
    /// </summary>
    private void Walk(EntityUid uid, TutorialNpcBonkComponent bonk, TimeSpan now)
    {
        var offset = _transform.ToMapCoordinates(bonk.Target).Position - _transform.GetWorldPosition(uid);
        var distance = offset.Length();

        if (distance > ArriveDistance && now < bonk.NextAt)
        {
            SetWalk(uid, offset / distance);
            return;
        }

        SetWalk(uid, Vector2.Zero);
        bonk.Stage = TutorialBonkStage.Settling;
        bonk.NextAt = now + SettleTime;
    }

    private void SetWalk(EntityUid uid, Vector2 worldDirection)
    {
        // walking off breaks a grip, the same as it does for anyone who steps away on their own.
        // he drives the mover directly, so the stock move input never gets to do it for him
        if (worldDirection != Vector2.Zero
            && TryComp<PullableComponent>(uid, out var pullable)
            && pullable.BeingPulled)
        {
            _pulling.TryStopPull(uid, pullable, uid);
        }

        if (!TryComp<InputMoverComponent>(uid, out var mover))
            return;

        mover.CurTickSprintMovement = (-_mover.GetParentGridAngle(mover)).RotateVec(worldDirection);
        mover.LastInputTick = _timing.CurTick;
        mover.LastInputSubTick = ushort.MaxValue;

        var ev = new SpriteMoveEvent(worldDirection != Vector2.Zero);
        RaiseLocalEvent(uid, ref ev);
    }

    private void Bonk(EntityUid uid, TutorialNpcBonkComponent bonk)
    {
        AllowDeath(uid);

        if (!HasComp<KnockedDownComponent>(uid))
            _climb.Bonk(bonk.Table, uid);

        _statusEffects.TryRemoveStatusEffect(uid, ClumsyEffect);
        _chat.TryEmoteWithChat(uid, "Scream", ignoreActionBlocker: true);
        Kill(uid, bonk.Blunt);
        RemCompDeferred<TutorialNpcBonkComponent>(uid);
    }

    /// <summary>
    /// Mapped mobs have no player, so stock SSD handling puts them to sleep ten minutes after the map
    /// loads - long before a trainee gets to them. A sleeping patient does not walk or climb anything.
    /// </summary>
    public void KeepAwake(EntityUid uid)
    {
        RemComp<SSDIndicatorComponent>(uid);
        _statusEffects.TryRemoveStatusEffect(uid, SSDIndicatorSystem.StatusEffectSSDSleeping);
    }

    public void StopRot(EntityUid uid)
    {
        RemComp<RottingComponent>(uid);
        RemComp<PerishableComponent>(uid);
    }

    public void AllowDeath(EntityUid uid)
    {
        if (TryComp<TutorialNpcComponent>(uid, out var npc))
            npc.PreventDeath = false;
    }

    public void SatiateAndIdle(EntityUid uid)
    {
        if (TryComp<SatiationComponent>(uid, out var satiation))
        {
            var ent = (uid, satiation);
            _satiation.SetValue(ent, SatiationSystem.Hunger, Overfed);
            _satiation.SetValue(ent, SatiationSystem.Thirst, Overhydrated);
        }

        if (!TryComp<HTNComponent>(uid, out var htn))
            return;

        if (htn.RootTask.Task != RuminantTask && htn.RootTask.Task != RuminantHostileTask)
            return;

        _npc.SleepNPC(uid, htn);
        htn.RootTask = new HTNCompoundTask { Task = IdleTask };
        _npc.WakeNPC(uid, htn);
    }

    /// <summary>
    /// Puts the patient just past dead. Most of it is asphyxiation, worked out from what he already
    /// carries, which is the one thing a defibrillator zap takes away.
    /// </summary>
    public void Kill(EntityUid uid, float blunt)
    {
        AllowDeath(uid);

        if (!_thresholds.TryGetThresholdForState(uid, MobState.Dead, out var deadAt) || deadAt is null)
            return;

        var spec = new DamageSpecifier();
        if (blunt > 0f)
            spec.DamageDict["Blunt"] = blunt;

        var asphyxiation = deadAt.Value.Float() + LethalMargin - _damageable.GetTotalDamage(uid).Float() - blunt;
        if (asphyxiation > 0f)
            spec.DamageDict["Asphyxiation"] = asphyxiation;

        if (spec.DamageDict.Count > 0)
            _damageable.ChangeDamage(uid, spec, ignoreResistances: true);
    }

    /// <summary>The tile next to the table on the side he is standing, so he walks up rather than into it.</summary>
    private EntityCoordinates BesideTable(EntityUid npc, EntityUid table)
    {
        var tableXform = Transform(table);
        var offset = _transform.GetWorldPosition(npc) - _transform.GetWorldPosition(tableXform);
        var side = MathF.Abs(offset.X) >= MathF.Abs(offset.Y)
            ? new Vector2(MathF.Sign(offset.X), 0f)
            : new Vector2(0f, MathF.Sign(offset.Y));

        if (side == Vector2.Zero)
            side = new Vector2(0f, -1f);

        return tableXform.Coordinates.Offset(side);
    }

    private void OnBeforeDamage(Entity<TutorialNpcComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!ent.Comp.PreventDeath)
            return;

        if (!args.Damage.AnyPositive())
            return;

        if (!_thresholds.TryGetThresholdForState(ent, MobState.Critical, out var stopAt) || stopAt is null)
        {
            if (!_thresholds.TryGetThresholdForState(ent, MobState.Dead, out stopAt) || stopAt is null)
                return;
        }

        if (!TryComp<DamageableComponent>(ent.Owner, out var dmg))
            return;

        if (_damageable.GetPositiveDamage((ent.Owner, dmg)).GetTotal() + args.Damage.GetTotal() < stopAt.Value)
            return;

        args.Cancelled = true;
    }

    private void OnIngestible(Entity<TutorialInedibleComponent> ent, ref IngestibleEvent args)
    {
        args.Cancelled = true;
    }
}
