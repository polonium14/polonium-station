using System.Linq;
using Content.Shared._Shitmed.CCVar;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;

/// <summary>
/// Converts damage dealt directly to a limb-organ's own DamageableComponent (mirrored there
/// by BodyDamageBridgeSystem) into wound entities and a derived WoundableSeverity, and
/// heals the same way in reverse.
/// </summary>
public sealed partial class WoundSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    private float _medicalHealingTickrate = 0.5f;
    private TimeSpan _minimumTimeBeforeHeal = TimeSpan.FromSeconds(2);

    private bool _suppressWoundInduction;

    private readonly Dictionary<string, DamageGroupPrototype?> _damageGroupCache = new();

    public override void Initialize()
    {
        base.Initialize();

        InitWounding();

        Subs.CVar(_cfg, SurgeryCVars.MedicalHealingTickrate, val => _medicalHealingTickrate = val, true);
        Subs.CVar(_cfg, SurgeryCVars.MinimumTimeBeforeHeal, val => _minimumTimeBeforeHeal = TimeSpan.FromSeconds(val), true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Server-only for the same reason as OnDamageDealt (see its doc comment) — healing
        // is authoritative state, not predicted.
        if (!_net.IsServer || !_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<BodyComponent>();
        while (query.MoveNext(out var mob, out var body))
        {
            if (TerminatingOrDeleted(mob)
                || Paused(mob)
                || body.Organs is null
                || _mobState.IsIncapacitated(mob))
                continue;

            if (_timing.CurTime < body.HealAt)
                continue;

            var mostRecentDamage = TimeSpan.Zero;
            foreach (var organ in body.Organs.ContainedEntities)
            {
                if (TryComp<WoundableComponent>(organ, out var w) && w.LastDamageTime > mostRecentDamage)
                    mostRecentDamage = w.LastDamageTime;
            }

            if (_timing.CurTime - mostRecentDamage < _minimumTimeBeforeHeal)
                continue;

            body.HealAt = _timing.CurTime + TimeSpan.FromSeconds(1f / _medicalHealingTickrate);

            // Snapshotted: ProcessHealing can heal a wound down to removal, which can cascade
            // into organ-container mutation.
            foreach (var organ in body.Organs.ContainedEntities.ToList())
            {
                if (!TryComp<WoundableComponent>(organ, out var woundable))
                    continue;

                if (woundable.CanHealDamage || woundable.CanHealBleeds)
                    ProcessHealing((organ, woundable));
            }
        }
    }

    public DamageGroupPrototype? GetDamageGroupByType(string damageTypeId)
    {
        if (_damageGroupCache.TryGetValue(damageTypeId, out var cached))
            return cached;

        DamageGroupPrototype? found = null;
        foreach (var group in _proto.EnumeratePrototypes<DamageGroupPrototype>())
        {
            if (!group.DamageTypes.Contains(damageTypeId))
                continue;

            found = group;
            break;
        }

        _damageGroupCache[damageTypeId] = found;
        return found;
    }
}

/// <summary>
/// Raised on a limb-organ when its derived WoundableSeverity changes, e.g. so
/// DismembermentSystem can react to a transition into WoundableSeverity.Severed.
/// </summary>
[ByRefEvent]
public readonly record struct WoundableSeverityChangedEvent(EntityUid Woundable, WoundableSeverity Old, WoundableSeverity New);
