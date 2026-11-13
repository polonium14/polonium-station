using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Eggperson;

public sealed partial class EggShellSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EggShellComponent, DamageModifyEvent>(OnDamageModify);
    }

    /// <summary>
    /// Sum of the given damage types in a damage specifier.
    /// </summary>
    public static FixedPoint2 SumTypes(DamageSpecifier damage, List<ProtoId<DamageTypePrototype>> types)
    {
        var total = FixedPoint2.Zero;
        foreach (var type in types)
        {
            if (damage.DamageDict.TryGetValue(type, out var value))
                total += value;
        }

        return total;
    }

    /// <summary>
    /// Current brute multiplier of the shell, based on how much burn damage the egg has.
    /// </summary>
    public float GetBruteModifier(Entity<EggShellComponent> ent)
    {
        var burn = SumTypes(_damageable.GetAllDamage(ent.Owner), ent.Comp.BurnTypes);
        var x = (float) FixedPoint2.Clamp(burn, FixedPoint2.Zero, ent.Comp.HardestBurn);
        return ent.Comp.A * x * x + ent.Comp.B * x + ent.Comp.RawBruteModifier;
    }

    private void OnDamageModify(Entity<EggShellComponent> ent, ref DamageModifyEvent args)
    {
        var modifier = GetBruteModifier(ent);
        foreach (var type in ent.Comp.BruteTypes)
        {
            // Healing goes through untouched, only incoming hits care about the shell.
            if (args.Damage.DamageDict.TryGetValue(type, out var value) && value > 0)
                args.Damage.DamageDict[type] = value * modifier;
        }
    }
}
