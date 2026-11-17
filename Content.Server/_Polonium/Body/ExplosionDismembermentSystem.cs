using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Polonium.Body;

/// <summary>
/// Raised on an entity after an explosion damaged it, with the damage it actually took.
/// </summary>
[ByRefEvent]
public readonly record struct ExplosionHitEvent(DamageSpecifier Damage);

/// <summary>
/// A strong enough explosion tears one arm or leg off a body. The head and the torso stay.
/// </summary>
public sealed partial class ExplosionDismembermentSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TraumaSystem _trauma = default!;

    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Piercing = "Piercing";

    private static readonly HashSet<ProtoId<OrganCategoryPrototype>> Limbs = ["ArmLeft", "ArmRight", "LegLeft", "LegRight"];

    private readonly List<Entity<WoundableComponent>> _candidates = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, ExplosionHitEvent>(OnExplosionHit);
    }

    private void OnExplosionHit(Entity<BodyComponent> ent, ref ExplosionHitEvent args)
    {
        if (ent.Comp.Organs is not { } organs)
            return;

        var physical = args.Damage.DamageDict.GetValueOrDefault(Blunt) + args.Damage.DamageDict.GetValueOrDefault(Piercing);
        var chance = (physical.Float() - _cfg.GetCVar(CCVars.DismembermentExplosionThreshold))
            * _cfg.GetCVar(CCVars.DismembermentExplosionChancePerDamage);
        if (chance <= 0f || !_random.Prob(Math.Min(chance, 1f)))
            return;

        _candidates.Clear();
        foreach (var organ in organs.ContainedEntities)
        {
            if (CompOrNull<OrganComponent>(organ)?.Category is { } category
                && Limbs.Contains(category)
                && TryComp<WoundableComponent>(organ, out var woundable))
                _candidates.Add((organ, woundable));
        }

        if (_candidates.Count > 0)
            _trauma.SeverLimb(_random.Pick(_candidates), physical);
    }
}
