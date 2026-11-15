using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Polonium.Body;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
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
/// Animals and other bodies without a character profile live without a heart as before.
/// </summary>
public sealed partial class CirculationSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private RespiratorSystem _respirator = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly ProtoId<OrganCategoryPrototype> Heart = "Heart";
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

        Subs.CVar(_cfg, CCVars.OrgansNoHeartSaturationLoss, perSecond => _saturationLoss = perSecond, true);
    }

    private void OnOrganInserted(Entity<HumanoidProfileComponent> ent, ref OrganInsertedIntoEvent args)
    {
        Refresh(ent);
    }

    private void OnOrganRemoved(Entity<HumanoidProfileComponent> ent, ref OrganRemovedFromEvent args)
    {
        Refresh(ent);
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

        var working = false;
        foreach (var organ in organs.ContainedEntities)
        {
            if (CompOrNull<OrganComponent>(organ)?.Category != Heart || HasComp<DeadOrganComponent>(organ))
                continue;

            working = true;
            break;
        }

        // A body that never had a heart, like a diona, has no circulation to stop.
        // Enumerated, because BodyComponent only grants other systems read access to the set.
        var expectsHeart = false;
        foreach (var category in bodyComp.ExpectedOrgans)
        {
            expectsHeart |= category == Heart;
        }

        if (working || !expectsHeart)
        {
            if (!TryComp<StoppedCirculationComponent>(body, out var stopped))
                return;

            if (stopped.AddedUnrevivable)
                RemComp<UnrevivableComponent>(body);

            RemComp<StoppedCirculationComponent>(body);
            return;
        }

        if (HasComp<StoppedCirculationComponent>(body))
            return;

        var comp = AddComp<StoppedCirculationComponent>(body);
        comp.NextUpdate = _timing.CurTime + UpdateInterval;
        comp.SaturationCeiling = CompOrNull<RespiratorComponent>(body)?.Saturation ?? 0f;

        // The defibrillator still fires on an unrevivable body, it just reports why nothing happened.
        if (HasComp<UnrevivableComponent>(body))
            return;

        comp.AddedUnrevivable = true;
        var unrevivable = AddComp<UnrevivableComponent>(body);
        unrevivable.ReasonMessage = UnrevivableReason;
        unrevivable.Analyzable = false;
        unrevivable.Cloneable = true;
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
                _statusEffects.TryUpdateStatusEffectDuration(uid, SleepingSystem.StatusEffectForcedSleeping, PassOutTime);
        }
    }
}
