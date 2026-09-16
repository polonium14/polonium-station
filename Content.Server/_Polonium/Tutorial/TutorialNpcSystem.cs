using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Physics.Controllers;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Nutrition;
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

    private const float ArriveDistance = 0.2f;
    private static readonly TimeSpan WalkTimeout = TimeSpan.FromSeconds(15);

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

    public override void Initialize()
    {
        // input has to be down before the mover reads it this tick
        UpdatesBefore.Add(typeof(SharedPhysicsSystem));

        SubscribeLocalEvent<TutorialNpcComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<TutorialInedibleComponent, IngestibleEvent>(OnIngestible);
    }

    public override void Update(float frameTime)
    {
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

    /// <summary>
    /// Puts the patient just past dead. Most of it is asphyxiation, worked out from what he already
    /// carries, which is the one thing a defibrillator zap takes away.
    /// </summary>
    public void Kill(EntityUid uid, float blunt)
    {
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

        if (!_thresholds.TryGetThresholdForState(ent, MobState.Dead, out var deadAt) || deadAt is null)
            return;

        if (!TryComp<DamageableComponent>(ent.Owner, out var dmg))
            return;

        if (_damageable.GetPositiveDamage((ent.Owner, dmg)).GetTotal() + args.Damage.GetTotal() < deadAt.Value)
            return;

        args.Cancelled = true;
    }

    private void OnIngestible(Entity<TutorialInedibleComponent> ent, ref IngestibleEvent args)
    {
        args.Cancelled = true;
    }
}
