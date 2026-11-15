using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Polonium.Body;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Components;
using Content.Shared.Chat;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Body;

/// <summary>
/// Stops the circulation of a humanoid whose heart is dead or cut out. Oxygen no longer reaches the body:
/// its saturation sinks to nothing within seconds, it passes out and then suffocates at the usual rate,
/// whatever it breathes. A defibrillator cannot bring it back until a working heart is in.
/// Losing the brain, by decapitation or surgery, kills the body outright, and it cannot be defibrillated
/// until the brain is back.
/// Animals and other bodies without a character profile live without a heart or brain as before.
/// </summary>
public sealed partial class CirculationSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private RespiratorSystem _respirator = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly ProtoId<OrganCategoryPrototype> Heart = "Heart";
    private static readonly ProtoId<OrganCategoryPrototype> Brain = "Brain";
    private static readonly LocId NoBrainReason = "defibrillator-no-brain";

    /// <summary>
    /// Out cold without being asleep, so there is no snoring popup and a shove does not wake the body up.
    /// </summary>
    private static readonly EntProtoId Unconscious = "StatusEffectUnconscious";
    private static readonly LocId UnrevivableReason = "defibrillator-no-heart";
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long a body without oxygen stays out after each update. Longer than the interval, so it never wakes in between.
    /// </summary>
    private static readonly TimeSpan PassOutTime = TimeSpan.FromSeconds(3);

    private float _saturationLoss;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidProfileComponent, OrganInsertedIntoEvent>(OnOrganInserted);
        SubscribeLocalEvent<HumanoidProfileComponent, OrganRemovedFromEvent>(OnOrganRemoved);
        SubscribeLocalEvent<StoppedCirculationComponent, BeforeEmoteEvent>(OnBeforeEmote);

        Subs.CVar(_cfg, CCVars.OrgansNoHeartSaturationLoss, perSecond => _saturationLoss = perSecond, true);
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

    private void OnBeforeEmote(Entity<StoppedCirculationComponent> ent, ref BeforeEmoteEvent args)
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

    private void Refresh(EntityUid body)
    {
        // Organs also leave a body that is being deleted.
        if (TerminatingOrDeleted(body) || !TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Organs is not { } organs)
            return;

        var heart = false;
        var brain = false;
        foreach (var organ in organs.ContainedEntities)
        {
            var category = CompOrNull<OrganComponent>(organ)?.Category;
            heart |= category == Heart && !HasComp<DeadOrganComponent>(organ);
            brain |= category == Brain;
        }

        // A body that never had a heart or brain, like a diona, has nothing to lose.
        // Enumerated, because BodyComponent only grants other systems read access to the set.
        var expectsHeart = false;
        var expectsBrain = false;
        foreach (var category in bodyComp.ExpectedOrgans)
        {
            expectsHeart |= category == Heart;
            expectsBrain |= category == Brain;
        }

        var stopped = expectsHeart && !heart;
        if (!stopped)
        {
            RemComp<StoppedCirculationComponent>(body);
        }
        else if (!HasComp<StoppedCirculationComponent>(body))
        {
            var comp = AddComp<StoppedCirculationComponent>(body);
            comp.NextUpdate = _timing.CurTime + UpdateInterval;
            comp.SaturationCeiling = CompOrNull<RespiratorComponent>(body)?.Saturation ?? 0f;
        }

        LocId? reason = null;
        if (expectsBrain && !brain)
            reason = NoBrainReason;
        else if (stopped)
            reason = UnrevivableReason;

        SetUnrevivable(body, reason);
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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StoppedCirculationComponent, RespiratorComponent>();
        while (query.MoveNext(out var uid, out var stopped, out var respirator))
        {
            if (_timing.CurTime < stopped.NextUpdate)
                continue;

            stopped.NextUpdate += UpdateInterval;

            // The dead do not breathe, and those in critical condition are out already.
            if (_mobState.IsIncapacitated(uid))
                continue;

            stopped.SaturationCeiling = MathF.Max(respirator.MinSaturation,
                stopped.SaturationCeiling - _saturationLoss * (float) UpdateInterval.TotalSeconds);

            // Breathing still fills the lungs, the oxygen just goes nowhere. From here the respirator
            // deals its usual suffocation damage.
            if (respirator.Saturation > stopped.SaturationCeiling)
                _respirator.UpdateSaturation(uid, stopped.SaturationCeiling - respirator.Saturation, respirator);

            if (respirator.Saturation < respirator.SuffocationThreshold)
                _statusEffects.TryUpdateStatusEffectDuration(uid, Unconscious, PassOutTime);
        }
    }
}
