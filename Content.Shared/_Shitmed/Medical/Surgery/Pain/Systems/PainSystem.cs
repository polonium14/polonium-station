using System.Linq;
using Content.Shared._Shitmed.CCVar;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Shitmed.Medical.Surgery.Pain.Systems;

public sealed partial class PainSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IRobustRandom _random = default!;

    [Dependency] private SharedAudioSystem _IHaveNoMouthAndIMustScream = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    [Dependency] private MobStateSystem _mobState = default!;

    [Dependency] private StandingStateSystem _standing = default!;

    [Dependency] private WoundSystem _wound = default!;
    [Dependency] private ConsciousnessSystem _consciousness = default!;
    [Dependency] private TraumaSystem _trauma = default!;

    private bool _screamsEnabled = false;
    private float _screamChance = 0.20f;

    private readonly HashSet<EntityUid> _queuedPainEntities = new();
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NerveComponent, OrganGotInsertedEvent>(OnOrganInserted);
        SubscribeLocalEvent<NerveComponent, OrganGotRemovedEvent>(OnOrganRemoved);

        SubscribeLocalEvent<NerveSystemComponent, EntityTerminatingEvent>(OnNerveSystemTerminating);

        _screamsEnabled = _cfg.GetCVar(SurgeryCVars.PainScreams);
        _screamChance = _cfg.GetCVar(SurgeryCVars.PainScreamChance);

        InitAffliction();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Server-only: pain is authoritative derived state, same rule as Wounds/Consciousness.
        if (!_net.IsServer)
            return;

        _painJobQueue.Process();

        if (!_timing.IsFirstTimePredicted)
            return;

        var decayEntities = new List<(EntityUid Uid, PainDecayComponent Decay, NerveSystemComponent Nerve)>();
        var decayQuery = EntityQueryEnumerator<PainDecayComponent, NerveSystemComponent>();
        while (decayQuery.MoveNext(out var uid, out var decay, out var nerveSystem))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            decayEntities.Add((uid, decay, nerveSystem));
        }

        foreach (var (uid, decay, nerveSystem) in decayEntities)
        {
            UpdatePainDecay(uid, decay, nerveSystem);
        }

        // Process regular pain updates
        using var query = EntityQueryEnumerator<NerveSystemComponent>();
        while (query.MoveNext(out var ent, out var nerveSystem))
        {
            if (TerminatingOrDeleted(ent) || !_queuedPainEntities.Add(ent))
                continue;

            _painJobQueue.EnqueueJob(new PainTimerJob(this, (ent, nerveSystem), PainJobTime));
        }
    }

    private void OnOrganInserted(Entity<NerveComponent> ent, ref OrganGotInsertedEvent args)
    {
        var body = args.Target;

        if (!_consciousness.TryGetNerveSystem(body, out var brainUid) || TerminatingOrDeleted(brainUid.Value))
            return;

        UpdateNerveSystemNerves(brainUid.Value, body, Comp<NerveSystemComponent>(brainUid.Value));
    }

    private void OnOrganRemoved(Entity<NerveComponent> ent, ref OrganGotRemovedEvent args)
    {
        var body = args.Target;
        var uid = ent.Owner;

        if (!_consciousness.TryGetNerveSystem(body, out var brainUid) || TerminatingOrDeleted(brainUid.Value))
            return;

        foreach (var modifier in brainUid.Value.Comp.Modifiers
                     .Where(modifier => modifier.Key.Item1 == uid))
            brainUid.Value.Comp.Modifiers.Remove((modifier.Key.Item1, modifier.Key.Item2));

        UpdateNerveSystemNerves(brainUid.Value, body, Comp<NerveSystemComponent>(brainUid.Value));
    }

    private void OnNerveSystemTerminating(EntityUid uid, NerveSystemComponent component, ref EntityTerminatingEvent args)
    {
        var query = EntityQueryEnumerator<NerveComponent>();
        while (query.MoveNext(out var nerveUid, out var nerve))
        {
            if (TerminatingOrDeleted(nerveUid) || nerve.ParentedNerveSystem != uid)
                continue;

            nerve.ParentedNerveSystem = default;
            Dirty(nerveUid, nerve);
        }
    }

    public void HandleMobStateChanged(Entity<NerveSystemComponent> nerveSysEnt, MobStateChangedEvent args)
    {
        var uid = nerveSysEnt.Owner;
        var nerveSys = nerveSysEnt.Comp;

        switch (args.NewMobState)
        {
            case MobState.Critical:
                var sex = Sex.Unsexed;
                if (TryComp<HumanoidProfileComponent>(args.Target, out var humanoid))
                    sex = humanoid.Sex;

                PlayPainSoundWithCleanup(args.Target, nerveSys, nerveSys.CritWhimpers[sex], AudioParams.Default.WithVolume(-12f));
                nerveSys.NextCritScream = _timing.CurTime + _random.Next(nerveSys.CritScreamsIntervalMin, nerveSys.CritScreamsIntervalMax);
                break;

            case MobState.Dead:
                CleanupSounds(nerveSys);
                break;
        }

        // Leaving Dead (revived) - UpdatePainThreshold's effect side skips entirely
        // while IsDead, and healing that happens on a still-Dead mob can leave stale
        // per-woundable pain modifiers behind (their owning wound is gone, but nothing
        // ever re-scanned to remove the modifier once the mob could act on it again). Reconcile
        // every woundable's modifier against its actual current wounds so pain can't stay stuck
        // above zero forever on an otherwise fully-healed revived mob.
        if (args.OldMobState == MobState.Dead && args.NewMobState != MobState.Dead)
            ReconcileWoundPainModifiers(uid, args.Target, nerveSys);
    }

    private void ReconcileWoundPainModifiers(EntityUid uid, EntityUid body, NerveSystemComponent nerveSys)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Organs is null)
            return;

        foreach (var organ in bodyComp.Organs.ContainedEntities)
        {
            var woundPain = FixedPoint2.Zero;
            var traumaticPain = FixedPoint2.Zero;

            foreach (var (woundId, _) in _wound.GetWoundableWounds(organ))
            {
                if (!TryComp<PainInflicterComponent>(woundId, out var painInflicter))
                    continue;

                switch (painInflicter.PainType)
                {
                    case PainDamageTypes.TraumaticPain:
                        traumaticPain += painInflicter.Pain;
                        break;
                    default:
                        woundPain += painInflicter.Pain;
                        break;
                }
            }

            if (woundPain <= 0)
                TryRemovePainModifier(uid, organ, PainModifierIdentifier, nerveSys);
            else
                TryChangePainModifier(uid, organ, PainModifierIdentifier, woundPain, nerveSys);

            if (traumaticPain <= 0)
                TryRemovePainModifier(uid, organ, PainTraumaticModifierIdentifier, nerveSys);
            else
                TryChangePainModifier(uid, organ, PainTraumaticModifierIdentifier, traumaticPain, nerveSys);
        }
    }

    private void UpdateNerveSystemNerves(EntityUid uid, EntityUid body, NerveSystemComponent component)
    {
        component.Nerves.Clear();

        if (TryComp<BodyComponent>(body, out var bodyComp) && bodyComp.Organs is not null)
        {
            foreach (var organ in bodyComp.Organs.ContainedEntities)
            {
                if (!TryComp<NerveComponent>(organ, out var nerve))
                    continue;

                component.Nerves.Add(organ, nerve);

                nerve.ParentedNerveSystem = uid;
                Dirty(organ, nerve);
            }
        }

        Dirty(uid, component);
    }

    #region Pain Decay

    /// <summary>
    /// Starts pain decay for a nerve system
    /// </summary>

    public void StartPainDecay(EntityUid uid, FixedPoint2 initialPain, TimeSpan decayDuration, NerveSystemComponent? nerveSystem = null)
    {
        if (!Resolve(uid, ref nerveSystem, false))
            return;

        // Remove any existing decay
        if (TryComp<PainDecayComponent>(uid, out var existingDecay))
        {
            // If the new decay would be longer than remaining time, keep the existing one
            var remainingTime = (existingDecay.StartTime + existingDecay.DecayDuration) - _timing.CurTime;
            if (remainingTime > decayDuration)
                return;

            RemComp<PainDecayComponent>(uid);
        }

        var decay = EnsureComp<PainDecayComponent>(uid);
        decay.InitialPain = initialPain;
        decay.StartTime = _timing.CurTime;
        decay.DecayDuration = decayDuration;
        decay.NerveSystemUid = uid;
        Dirty(uid, decay);
    }

    // Stops any active pain decay for an entity
    public void StopPainDecay(EntityUid uid)
    {
        if (HasComp<PainDecayComponent>(uid))
            RemComp<PainDecayComponent>(uid);
    }

    // Updates the pain value based on decay progress
    private void UpdatePainDecay(EntityUid uid, PainDecayComponent decay, NerveSystemComponent nerveSystem)
    {
        var elapsed = _timing.CurTime - decay.StartTime;

        // If decay duration has passed, set pain to 0 and remove decay component
        if (elapsed >= decay.DecayDuration)
        {
            nerveSystem.Pain = FixedPoint2.Zero;
            Dirty(uid, nerveSystem);
            RemComp<PainDecayComponent>(uid);
            return;
        }

        // Calculate current pain based on decay progress
        var progress = (float)(elapsed.TotalSeconds / decay.DecayDuration.TotalSeconds);
        var currentPain = decay.InitialPain * (1 - progress);

        // Only update if pain would decrease
        if (currentPain < nerveSystem.Pain)
        {
            nerveSystem.Pain = currentPain;
            Dirty(uid, nerveSystem);
        }
    }

    #endregion
}
