using Content.Server._Polonium.Medical.Dialysis;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Polonium.Body;
using Content.Shared._Polonium.Medical.Dialysis;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body;
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
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private OrganHealingSystem _healing = default!;
    [Dependency] private RespiratorSystem _respirator = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly ProtoId<OrganCategoryPrototype> Heart = "Heart";
    private static readonly ProtoId<OrganCategoryPrototype> Brain = "Brain";
    private static readonly ProtoId<OrganCategoryPrototype> Lungs = "Lungs";
    private static readonly ProtoId<OrganCategoryPrototype> Liver = "Liver";
    private static readonly ProtoId<OrganCategoryPrototype> Kidneys = "Kidneys";
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
    /// Share of poison damage the kidneys and the liver lose when it hurts them.
    /// </summary>
    private const float KidneyToxinShare = 0.1f;
    private const float LiverToxinShare = 0.04f;

    /// <summary>
    /// A reading older than this starts the average over.
    /// </summary>
    private static readonly TimeSpan SpO2Memory = TimeSpan.FromSeconds(10);

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
        if (_organPoison
            || !args.Damage.DamageDict.TryGetValue(Poison, out var poison)
            || poison <= FixedPoint2.Zero)
            return;

        HurtOrgan(ent, Kidneys, poison * KidneyToxinShare);
        HurtOrgan(ent, Liver, poison * LiverToxinShare);
    }

    private void HurtOrgan(EntityUid body, ProtoId<OrganCategoryPrototype> category, FixedPoint2 amount)
    {
        if (!_random.Prob(_toxinOrganChance))
            return;

        foreach (var organ in _healing.GetLivingOrgans(body, [category], []))
        {
            _healing.DamageOrgan(organ, amount);
        }
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

        var heart = false;
        var brain = false;
        var liver = false;
        var kidneys = false;
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

            if (category == Kidneys && alive)
            {
                kidneys = true;
                sickKidneys |= CompOrNull<OrganIntegrityComponent>(organ)?.OrganSeverity is not null and not OrganSeverity.Normal;
            }

            if (category != Heart && category != Lungs || !alive || GetStage(organ) is not { } stage)
                continue;

            loss = MathF.Max(loss, stage.SaturationLoss);
            cap = MathF.Min(cap, stage.SaturationCap);
        }

        // A body that never had an organ, like a diona without a heart, has nothing to lose.
        // Enumerated, because BodyComponent only grants other systems read access to the set.
        var expectsHeart = false;
        var expectsBrain = false;
        var expectsLiver = false;
        var expectsKidneys = false;
        foreach (var category in bodyComp.ExpectedOrgans)
        {
            expectsHeart |= category == Heart;
            expectsBrain |= category == Brain;
            expectsLiver |= category == Liver;
            expectsKidneys |= category == Kidneys;
        }

        var stopped = expectsHeart && !heart;
        if (stopped)
        {
            EnsureComp<StoppedCirculationComponent>(body);
            loss = MathF.Max(loss, _saturationLoss);
        }
        else
        {
            RemComp<StoppedCirculationComponent>(body);
        }

        SetOxygenLimit(body, loss, cap);

        var kidneyRate = !expectsKidneys ? 1f
            : !kidneys ? DeadKidneyRate
            : sickKidneys ? SickKidneyRate
            : 1f;
        SetKidneyRate(body, kidneyRate);

        var poison = 0f;
        if (expectsLiver && !liver)
            poison += _deadOrganPoison * GetDialysisFactor(body, Liver);
        if (expectsKidneys && !kidneys)
            poison += _deadOrganPoison * GetDialysisFactor(body, Kidneys);
        SetPoisoning(body, poison);

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

        if (category == Kidneys)
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
    /// Oxygen saturation as a percentage a pulse oximeter would show, averaged over the last few readings.
    /// Null for a body that does not breathe.
    /// </summary>
    public float? ReadSpO2(EntityUid body)
    {
        // The respirator stops at death, so its last value would read like a living patient.
        if (!TryComp<RespiratorComponent>(body, out var respirator) || _mobState.IsDead(body))
            return null;

        var sample = ToSpO2(respirator);
        // A fresh reading, or one after a long break, starts the average over.
        if (!TryComp<SpO2ReadingComponent>(body, out var reading) || _timing.CurTime - reading.LastRead > SpO2Memory)
        {
            reading = EnsureComp<SpO2ReadingComponent>(body);
            reading.Average = sample;
        }
        else
        {
            reading.Average += (sample - reading.Average) * 0.3f;
        }

        reading.LastRead = _timing.CurTime;
        return reading.Average;
    }

    /// <summary>
    /// Maps the respirator's saturation to a pulse oximeter percentage. The game's saturation is not a percentage:
    /// a healthy body swings between breaths in the upper part of its range, which reads 95–99%. The suffocation
    /// threshold reads 88% and the very bottom 50%.
    /// </summary>
    private static float ToSpO2(RespiratorComponent respirator)
    {
        var threshold = respirator.SuffocationThreshold;
        var healthy = threshold + (respirator.MaxSaturation - threshold) * 0.4f;
        var saturation = Math.Clamp(respirator.Saturation, respirator.MinSaturation, respirator.MaxSaturation);

        if (saturation >= healthy)
            return 95f + 4f * (saturation - healthy) / MathF.Max(respirator.MaxSaturation - healthy, 0.01f);

        if (saturation >= threshold)
            return 88f + 7f * (saturation - threshold) / MathF.Max(healthy - threshold, 0.01f);

        return 50f + 38f * (saturation - respirator.MinSaturation) / MathF.Max(threshold - respirator.MinSaturation, 0.01f);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var oxygen = EntityQueryEnumerator<OxygenLimitComponent, RespiratorComponent>();
        while (oxygen.MoveNext(out var uid, out var limit, out var respirator))
        {
            if (_timing.CurTime < limit.NextUpdate)
                continue;

            limit.NextUpdate += UpdateInterval;

            // The dead do not breathe, and those in critical condition are out already.
            if (_mobState.IsIncapacitated(uid))
                continue;

            limit.Ceiling = limit.Loss > 0f
                ? MathF.Max(respirator.MinSaturation, limit.Ceiling - limit.Loss * (float) UpdateInterval.TotalSeconds)
                : respirator.MaxSaturation;

            // Breathing still fills the lungs, the oxygen just does not get where it is needed. From here the
            // respirator deals its usual suffocation damage.
            var most = MathF.Min(limit.Ceiling, respirator.MaxSaturation * limit.Cap);
            if (respirator.Saturation > most)
                _respirator.UpdateSaturation(uid, most - respirator.Saturation, respirator);

            if (respirator.Saturation < respirator.SuffocationThreshold)
                _statusEffects.TryUpdateStatusEffectDuration(uid, Unconscious, PassOutTime);
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
