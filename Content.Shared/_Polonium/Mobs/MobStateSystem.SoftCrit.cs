using Content.Shared._Polonium.Mobs;
using Content.Shared.Damage.Components;
using Content.Shared.Emoting;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;

// Polonium: a partial of the upstream system, so its namespace follows the upstream one.
namespace Content.Shared.Mobs.Systems;

public partial class MobStateSystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private EntityQuery<SoftCritComponent> _softCritQuery = default!;
    [Dependency] private EntityQuery<DamageableComponent> _damageableQuery = default!;
    [Dependency] private EntityQuery<PullableComponent> _pullableQuery = default!;

    /// <summary>
    /// Whether the mob is critical but still below its hard crit threshold, see <see cref="SoftCritComponent"/>.
    /// Soft crit is a part of <see cref="MobState.Critical"/>, so <see cref="IsCritical"/> and
    /// <see cref="IsIncapacitated"/> are true for it too: it suffocates and gets CPR like hard crit.
    /// </summary>
    public bool IsSoftCritical(EntityUid target, MobStateComponent? component = null)
    {
        if (!_mobStateQuery.Resolve(target, ref component, false)
            || component.CurrentState != MobState.Critical
            || !_softCritQuery.TryComp(target, out var softCrit)
            || !_damageableQuery.TryComp(target, out var damageable))
            return false;

        var threshold = softCrit.HardCrit
            ? softCrit.HardCritThreshold - softCrit.RecoveryMargin
            : softCrit.HardCritThreshold;
        return _damageable.GetTotalDamage((target, damageable)) < threshold;
    }

    /// <summary>
    /// Whether a mob in soft crit may do what the event asks, which critical mobs otherwise cannot.
    /// Attacks, items (using, picking up, dropping, equipping), throwing and pulling stay blocked,
    /// and interactions need consciousness, which soft crit lacks.
    /// </summary>
    private bool SoftCritAllows(EntityUid target, MobStateComponent component, CancellableEntityEventArgs args)
    {
        if (!IsSoftCritical(target, component))
            return false;

        return args switch
        {
            // Nobody crawls away from someone dragging them.
            UpdateCanMoveEvent => !_pullableQuery.TryComp(target, out var pullable) || !pullable.BeingPulled,
            StandAttemptEvent => _statusEffects.HasEffectComp<SoftCritStandStatusEffectComponent>(target),
            // Items and attacks stay blocked as in hard crit.
            ChangeDirectionAttemptEvent
                or EmoteAttemptEvent
                or SpeakAttemptEvent => true,
            _ => false,
        };
    }
}
