using Content.Server._Polonium.Medical.Cpr;
using Content.Server._Polonium.Medical.Dialysis;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Polonium.Body;
using Content.Shared._Polonium.Medical.Dialysis;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Body;

/// <summary>
/// Keeps the body of a humanoid in line with the state of its vital organs:
/// <list type="bullet">
/// <item>No working heart stops the circulation. Oxygen no longer reaches the body, it passes out and suffocates
/// at the usual rate whatever it breathes, and a defibrillator cannot bring it back until a working heart is in.</item>
/// <item>A failing heart or failing lungs limit how much oxygen the body keeps.</item>
/// <item>Losing the brain kills the body outright, and it cannot be defibrillated until the brain is back.</item>
/// <item>Sick, dead or missing kidneys slow down chemistry and thirst; a dead or missing liver or kidneys poison the body.</item>
/// <item>Poison damage wears down the kidneys and the liver.</item>
/// </list>
/// Animals and other bodies without a character profile are left alone.
/// </summary>
public sealed partial class CirculationSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private OrganHealingSystem _healing = default!;
    [Dependency] private RespiratorSystem _respirator = default!;
    [Dependency] private SensesSystem _senses = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly ProtoId<OrganCategoryPrototype> Heart = "Heart";
    private static readonly ProtoId<OrganCategoryPrototype> Brain = "Brain";
    private static readonly ProtoId<OrganCategoryPrototype> Liver = "Liver";
    private static readonly ProtoId<OrganCategoryPrototype> Spleen = "Spleen";
    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";
    private static readonly LocId NoBrainReason = "defibrillator-no-brain";
    private static readonly LocId NoHeartReason = "defibrillator-no-heart";

    /// <summary>
    /// Out cold without being asleep, so there is no snoring popup and a shove does not wake the body up.
    /// </summary>
    public static readonly EntProtoId Unconscious = "StatusEffectUnconscious";

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long a body without oxygen stays out after each update. Longer than the interval, so it never wakes in between.
    /// </summary>
    private static readonly TimeSpan PassOutTime = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Pace of chemistry and thirst with sick kidneys, and with dead or missing ones.
    /// </summary>
    private const float SickKidneyRate = 0.75f;
    private const float DeadKidneyRate = 0.5f;

    /// <summary>
    /// Share of the oxygen a body keeps when one lung of the pair does not work.
    /// </summary>
    private const float OneLungCap = 0.6f;

    /// <summary>
    /// Damage types that wear an organ down, and the share of the damage the organ loses when it does.
    /// Poison strains the kidneys and liver, electricity the heart, radiation the blood-making spleen.
    /// </summary>
    private static readonly (ProtoId<DamageTypePrototype> Type, ProtoId<OrganCategoryPrototype> Organ, float Share)[] StrainedOrgans =
    [
        ("Poison", PairedOrgans.Kidneys, 0.1f),
        ("Poison", "Liver", 0.04f),
        ("Shock", "Heart", 0.2f),
        ("Radiation", "Spleen", 0.1f),
    ];

    /// <summary>
    /// How often breathing bodies are checked for a due saturation sample.
    /// </summary>
    private static readonly TimeSpan SpO2SampleInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// After this many respirator updates in a row below the suffocation threshold the reading drops at once,
    /// instead of waiting for the last good breath to leave the window.
    /// </summary>
    private const int SpO2SuffocationCycles = 2;

    private TimeSpan _nextSpO2Sample;
    private readonly List<EntityUid> _untrackedBreathers = [];

    private float _saturationLoss;
    private float _deadOrganPoison;
    private float _toxinOrganChance;

    /// <summary>
    /// Set while organs poison the body themselves, so that poison does not wear the kidneys down in a loop.
    /// </summary>
    private bool _organPoison;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidProfileComponent, OrganInsertedIntoEvent>(OnOrganInserted);
        SubscribeLocalEvent<HumanoidProfileComponent, OrganRemovedFromEvent>(OnOrganRemoved);
        SubscribeLocalEvent<HumanoidProfileComponent, DamageDealtEvent>(OnDamageDealt);
        SubscribeLocalEvent<OxygenLimitComponent, BeforeEmoteEvent>(OnBeforeEmote);

        Subs.CVar(_cfg, CCVars.OrgansNoHeartSaturationLoss, value => _saturationLoss = value, true);
        Subs.CVar(_cfg, CCVars.OrgansDeadOrganPoison, value => _deadOrganPoison = value, true);
        Subs.CVar(_cfg, CCVars.OrgansToxinOrganDamageChance, value => _toxinOrganChance = value, true);
    }

    private void OnOrganInserted(Entity<HumanoidProfileComponent> ent, ref OrganInsertedIntoEvent args)
    {
        Refresh(ent);
    }

    private void OnOrganRemoved(Entity<HumanoidProfileComponent> ent, ref OrganRemovedFromEvent args)
    {
        // Shitmed marks the body for death when the brain leaves, but the code that acted on it is
        // commented out in ConsciousnessSystem, so the body is killed here instead.
        // Character appearance dummies have a profile and organs, but are not alive to begin with.
        if (!TerminatingOrDeleted(ent)
            && TryComp<MobStateComponent>(ent, out var mobState)
            && TryComp<ConsciousnessRequiredComponent>(args.Organ, out var required)
            && required.CausesDeath
            && !_mobState.IsDead(ent, mobState))
            _mobState.ChangeMobState(ent, MobState.Dead, mobState);

        Refresh(ent);
    }

    private void OnDamageDealt(Entity<HumanoidProfileComponent> ent, ref DamageDealtEvent args)
    {
        if (_organPoison)
            return;

        foreach (var (type, organ, share) in StrainedOrgans)
        {
            if (args.Damage.DamageDict.TryGetValue(type, out var damage) && damage > FixedPoint2.Zero)
                HurtOrgan(ent, organ, damage.Float() * share);
        }
    }

    private void HurtOrgan(EntityUid body, ProtoId<OrganCategoryPrototype> category, float share)
    {
        if (!_random.Prob(_toxinOrganChance))
            return;

        // FixedPoint2 keeps hundredths, so a tick's share (often 0.004) would round to nothing.
        // Rounding up by the leftover's chance keeps the average right.
        var hundredths = share * 100f;
        var whole = MathF.Floor(hundredths);
        if (_random.Prob(hundredths - whole))
            whole++;

        var amount = FixedPoint2.New(whole / 100f);
        if (amount <= FixedPoint2.Zero)
            return;

        // One organ, so a pair of kidneys wears down as fast as a single one and rarely fails at once.
        var organs = _healing.GetLivingOrgans(body, [category], []);
        if (organs.Count > 0)
            _healing.DamageOrgan(_random.Pick(organs), amount);
    }

    private void OnBeforeEmote(Entity<OxygenLimitComponent> ent, ref BeforeEmoteEvent args)
    {
        // The respirator makes a suffocating body gasp no matter what; an unconscious one does not.
        if (_statusEffects.HasStatusEffect(ent, Unconscious))
            args.Cancel();
    }

    /// <summary>
    /// Whether the body has no working heart.
    /// </summary>
    public bool IsStopped(EntityUid body)
    {
        return HasComp<StoppedCirculationComponent>(body);
    }

    /// <summary>
    /// Deals damage that comes from the body's own organs, which does not wear the kidneys or liver down.
    /// </summary>
    public void DealOrganDamage(EntityUid body, DamageSpecifier damage)
    {
        _organPoison = true;
        try
        {
            _damageable.ChangeDamage(body, damage, ignoreResistances: true, interruptsDoAfters: false);
        }
        finally
        {
            _organPoison = false;
        }
    }

    /// <summary>
    /// Brings the body in line with its organs. Call it whenever an organ dies, leaves, falls sick or fails.
    /// </summary>
    public void Refresh(EntityUid body)
    {
        // Organs also leave a body that is being deleted.
        if (TerminatingOrDeleted(body)
            || !HasComp<HumanoidProfileComponent>(body)
            || !TryComp<BodyComponent>(body, out var bodyComp)
            || bodyComp.Organs is not { } organs)
            return;

        _senses.MarkDirty(body);

        var heart = false;
        var brain = false;
        var liver = false;
        var spleen = false;
        // How much of the lungs and kidneys work: a side counts half, a single organ whole.
        var lungs = 0f;
        var kidneys = 0f;
        var sickKidneys = false;
        var loss = 0f;
        var cap = 1f;
        foreach (var organ in organs.ContainedEntities)
        {
            var category = CompOrNull<OrganComponent>(organ)?.Category;
            var alive = !HasComp<DeadOrganComponent>(organ);
            heart |= category == Heart && alive;
            brain |= category == Brain;
            liver |= category == Liver && alive;
            spleen |= category == Spleen && alive;

            if (alive && PairedOrgans.Matches(category, PairedOrgans.Lungs))
                lungs += PairedOrgans.Share(category);

            if (alive && PairedOrgans.Matches(category, PairedOrgans.Kidneys))
            {
                kidneys += PairedOrgans.Share(category);
                sickKidneys |= CompOrNull<OrganIntegrityComponent>(organ)?.OrganSeverity is not null and not OrganSeverity.Normal;
            }

            if (category != Heart && !PairedOrgans.Matches(category, PairedOrgans.Lungs) || !alive || GetStage(organ) is not { } stage)
                continue;

            loss = MathF.Max(loss, stage.SaturationLoss);
            cap = MathF.Min(cap, stage.SaturationCap);
        }

        // A body that never had an organ, like a diona without a heart, has nothing to lose.
        // Enumerated, because BodyComponent only grants other systems read access to the set.
        var expectsHeart = false;
        var expectsBrain = false;
        var expectsLiver = false;
        var expectsSpleen = false;
        var expectsLungs = false;
        var expectsKidneys = false;
        foreach (var category in bodyComp.ExpectedOrgans)
        {
            expectsHeart |= category == Heart;
            expectsBrain |= category == Brain;
            expectsLiver |= category == Liver;
            expectsSpleen |= category == Spleen;
            expectsLungs |= PairedOrgans.Matches(category, PairedOrgans.Lungs);
            expectsKidneys |= PairedOrgans.Matches(category, PairedOrgans.Kidneys);
        }

        // The body debug window can stop a working heart, for testing CPR without killing the organ.
        var stopped = expectsHeart && (!heart || HasComp<CirculationArrestComponent>(body));
        if (stopped)
        {
            EnsureComp<StoppedCirculationComponent>(body);
            loss = MathF.Max(loss, _saturationLoss);
        }
        else
        {
            RemComp<StoppedCirculationComponent>(body);
        }

        // Without any lung the respirator already suffocates the body.
        if (expectsLungs && lungs > 0f && lungs < 1f)
            cap = MathF.Min(cap, OneLungCap);

        SetOxygenLimit(body, loss, cap);

        var kidneyRate = !expectsKidneys ? 1f
            : kidneys <= 0f ? DeadKidneyRate
            : sickKidneys || kidneys < 1f ? SickKidneyRate
            : 1f;
        SetKidneyRate(body, kidneyRate);

        var poison = 0f;
        if (expectsLiver && !liver)
            poison += _deadOrganPoison * GetDialysisFactor(body, Liver);
        if (expectsKidneys && kidneys <= 0f)
            poison += _deadOrganPoison * GetDialysisFactor(body, PairedOrgans.Kidneys);
        SetPoisoning(body, poison);

        if (expectsSpleen && !spleen)
            EnsureComp<BloodDrainComponent>(body);
        else
            RemComp<BloodDrainComponent>(body);

        LocId? reason = null;
        if (expectsBrain && !brain)
            reason = NoBrainReason;
        else if (stopped)
            reason = NoHeartReason;

        SetUnrevivable(body, reason);
    }

    /// <summary>
    /// Share of the poison a dead or failing organ of this category still deals, 1 without dialysis.
    /// </summary>
    public float GetDialysisFactor(EntityUid body, ProtoId<OrganCategoryPrototype>? category)
    {
        if (CompOrNull<OnDialysisComponent>(body)?.Machine is not { } machine
            || !TryComp<DialysisComponent>(machine, out var dialysis))
            return 1f;

        if (PairedOrgans.Matches(category, PairedOrgans.Kidneys))
            return dialysis.KidneyPoisonFactor;

        return category == Liver ? dialysis.LiverPoisonFactor : 1f;
    }

    private OrganFailureStage? GetStage(EntityUid organ)
    {
        if (!TryComp<OrganFailureComponent>(organ, out var failure) || failure.Stage <= 0)
            return null;

        var stages = _proto.Index(failure.Failure).Stages;
        return stages[Math.Min(failure.Stage, stages.Count) - 1];
    }

    private void SetOxygenLimit(EntityUid body, float loss, float cap)
    {
        if (loss <= 0f && cap >= 1f)
        {
            RemComp<OxygenLimitComponent>(body);
            return;
        }

        if (!TryComp<OxygenLimitComponent>(body, out var limit))
        {
            limit = AddComp<OxygenLimitComponent>(body);
            limit.NextUpdate = _timing.CurTime + UpdateInterval;
            limit.Ceiling = CompOrNull<RespiratorComponent>(body)?.Saturation ?? 0f;
        }

        limit.Loss = loss;
        limit.Cap = cap;
    }

    private void SetKidneyRate(EntityUid body, float rate)
    {
        if (rate >= 1f)
        {
            RemComp<KidneyFunctionComponent>(body);
            return;
        }

        var kidneys = EnsureComp<KidneyFunctionComponent>(body);
        if (kidneys.Rate.Equals(rate))
            return;

        kidneys.Rate = rate;
        Dirty(body, kidneys);
    }

    private void SetPoisoning(EntityUid body, float perSecond)
    {
        if (perSecond <= 0f)
        {
            RemComp<OrganPoisoningComponent>(body);
            return;
        }

        if (!TryComp<OrganPoisoningComponent>(body, out var poisoning))
        {
            poisoning = AddComp<OrganPoisoningComponent>(body);
            poisoning.NextUpdate = _timing.CurTime + UpdateInterval;
        }

        poisoning.PerSecond = perSecond;
    }

    /// <summary>
    /// The defibrillator still fires on an unrevivable body, it just reports why nothing happened.
    /// </summary>
    private void SetUnrevivable(EntityUid body, LocId? reason)
    {
        var ours = HasComp<MissingVitalOrganComponent>(body);
        if (reason is not { } message)
        {
            if (!ours)
                return;

            RemComp<UnrevivableComponent>(body);
            RemComp<MissingVitalOrganComponent>(body);
            return;
        }

        UnrevivableComponent unrevivable;
        if (ours)
        {
            unrevivable = Comp<UnrevivableComponent>(body);
        }
        else
        {
            // An unrevivable trait is not ours to change.
            if (HasComp<UnrevivableComponent>(body))
                return;

            AddComp<MissingVitalOrganComponent>(body);
            unrevivable = AddComp<UnrevivableComponent>(body);
            unrevivable.Analyzable = false;
            unrevivable.Cloneable = true;
        }

        unrevivable.ReasonMessage = message;
        Dirty(body, unrevivable);
    }

    /// <summary>
    /// Oxygen saturation as a percentage a pulse oximeter would show. Null for a body that does not breathe.
    /// </summary>
    /// <remarks>
    /// The respirator's saturation falls between breaths and jumps back on each inhale, so the reading is the
    /// highest value of the last full breath: what the lungs actually reach. It stays put while the patient
    /// breathes normally and falls only when the breaths themselves bring in less.
    /// </remarks>
    public float? ReadSpO2(EntityUid body)
    {
        // The respirator stops at death, so its last value would read like a living patient.
        if (!TryComp<RespiratorComponent>(body, out var respirator) || _mobState.IsDead(body))
            return null;

        var saturation = UsableSaturation(body, respirator);
        if (respirator.SuffocationCycles < SpO2SuffocationCycles && TryComp<SpO2ReadingComponent>(body, out var reading))
        {
            for (var i = 0; i < reading.Count; i++)
            {
                saturation = MathF.Max(saturation, reading.Samples[i]);
            }
        }

        var spo2 = ToSpO2(respirator, saturation);

        // Smoking shows on the reading for a while, without taking any oxygen away.
        if (_statusEffects.HasEffectComp<SmokerBreathStatusEffectComponent>(body))
            spo2 = MathF.Max(spo2 - _cfg.GetCVar(CCVars.OrgansSmokingSpO2Drop), 0f);

        return spo2;
    }

    /// <summary>
    /// The respirator's saturation, without the part a weak heart or a missing lung cannot use.
    /// An inhale can overshoot that limit until the next update trims it.
    /// </summary>
    private float UsableSaturation(EntityUid body, RespiratorComponent respirator)
    {
        var saturation = respirator.Saturation;
        if (TryComp<OxygenLimitComponent>(body, out var limit))
            saturation = MathF.Min(saturation, MathF.Min(limit.Ceiling, respirator.MaxSaturation * limit.Cap));

        return saturation;
    }

    private void SampleSpO2()
    {
        if (_timing.CurTime < _nextSpO2Sample)
            return;

        _nextSpO2Sample = _timing.CurTime + SpO2SampleInterval;

        var breathers = EntityQueryEnumerator<RespiratorComponent>();
        while (breathers.MoveNext(out var uid, out var respirator))
        {
            if (!TryComp<SpO2ReadingComponent>(uid, out var reading))
            {
                _untrackedBreathers.Add(uid);
                continue;
            }

            // The respirator stops at death; what it showed before must not come back with the patient.
            if (_mobState.IsDead(uid))
            {
                reading.Count = 0;
                reading.Next = 0;
                continue;
            }

            if (_timing.CurTime < reading.NextSample)
                continue;

            // Two samples per respirator update, so a slowed metabolism (stasis) still gets one full breath.
            reading.NextSample = _timing.CurTime + respirator.AdjustedUpdateInterval / 2;
            reading.Samples[reading.Next] = UsableSaturation(uid, respirator);
            reading.Next = (reading.Next + 1) % SpO2ReadingComponent.Window;
            reading.Count = Math.Min(reading.Count + 1, SpO2ReadingComponent.Window);
        }

        foreach (var uid in _untrackedBreathers)
        {
            EnsureComp<SpO2ReadingComponent>(uid);
        }

        _untrackedBreathers.Clear();
    }

    /// <summary>
    /// Maps a respirator saturation to a pulse oximeter percentage. The game's saturation is not a percentage:
    /// a healthy body tops it up to the maximum with every breath, which reads 99%. The upper 60% of the range
    /// above the suffocation threshold reads 95–99%, the threshold itself 88% and the very bottom 50%.
    /// </summary>
    private static float ToSpO2(RespiratorComponent respirator, float saturation)
    {
        var threshold = respirator.SuffocationThreshold;
        var healthy = threshold + (respirator.MaxSaturation - threshold) * 0.4f;
        saturation = Math.Clamp(saturation, respirator.MinSaturation, respirator.MaxSaturation);

        if (saturation >= healthy)
            return 95f + 4f * (saturation - healthy) / MathF.Max(respirator.MaxSaturation - healthy, 0.01f);

        if (saturation >= threshold)
            return 88f + 7f * (saturation - threshold) / MathF.Max(healthy - threshold, 0.01f);

        return 50f + 38f * (saturation - respirator.MinSaturation) / MathF.Max(threshold - respirator.MinSaturation, 0.01f);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        SampleSpO2();

        var oxygen = EntityQueryEnumerator<OxygenLimitComponent, RespiratorComponent>();
        while (oxygen.MoveNext(out var uid, out var limit, out var respirator))
        {
            if (_timing.CurTime < limit.NextUpdate)
                continue;

            limit.NextUpdate += UpdateInterval;

            // The dead do not breathe, and those in critical condition are out already.
            if (_mobState.IsIncapacitated(uid))
                continue;

            // Chest compressions move the blood of a body that still has its heart: the oxygen it has stays.
            var compressed = TryComp<ChestCompressionsComponent>(uid, out var compressions)
                             && compressions.Until > _timing.CurTime;

            if (limit.Loss <= 0f)
                limit.Ceiling = respirator.MaxSaturation;
            else if (!compressed)
                limit.Ceiling = MathF.Max(respirator.MinSaturation, limit.Ceiling - limit.Loss * (float) UpdateInterval.TotalSeconds);

            // Breathing still fills the lungs, the oxygen just does not get where it is needed. From here the
            // respirator deals its usual suffocation damage.
            var most = MathF.Min(limit.Ceiling, respirator.MaxSaturation * limit.Cap);
            if (respirator.Saturation > most)
                _respirator.UpdateSaturation(uid, most - respirator.Saturation, respirator);

            // Only a body that keeps suffocating passes out. One lung dips below the threshold for a moment
            // at the end of every breath and recovers with the next one.
            // A capped reserve alone (one lung, a weak heart) leaves the patient as awake as a healthy one.
            if (limit.Loss > 0f
                && respirator.Saturation < respirator.SuffocationThreshold
                && respirator.SuffocationCycles >= SpO2SuffocationCycles)
                _statusEffects.TryUpdateStatusEffectDuration(uid, Unconscious, PassOutTime);
        }

        var drained = EntityQueryEnumerator<BloodDrainComponent, BloodstreamComponent>();
        while (drained.MoveNext(out var uid, out var drain, out var bloodstream))
        {
            if (_timing.CurTime < drain.NextUpdate)
                continue;

            drain.NextUpdate = _timing.CurTime + bloodstream.AdjustedUpdateInterval;

            // The dead neither regenerate nor lose blood this way.
            if (_mobState.IsDead(uid))
                continue;

            // The bloodstream still regenerates on its own, so taking twice that leaves the body losing what a
            // healthy spleen would have given back.
            _bloodstream.TryModifyBloodLevel((uid, bloodstream), -2 * bloodstream.BloodRefreshAmount);
        }

        var poisoned = EntityQueryEnumerator<OrganPoisoningComponent>();
        while (poisoned.MoveNext(out var uid, out var poisoning))
        {
            if (_timing.CurTime < poisoning.NextUpdate)
                continue;

            poisoning.NextUpdate += UpdateInterval;

            // Poison counts towards the limit a defibrillator can bring back, so the dead are spared it.
            if (_mobState.IsDead(uid))
                continue;

            DealOrganDamage(uid, new DamageSpecifier(_proto.Index(Poison), poisoning.PerSecond * UpdateInterval.TotalSeconds));
        }
    }
}
