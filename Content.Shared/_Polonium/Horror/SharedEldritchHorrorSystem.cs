using Content.Shared._Polonium.Horror.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Horror;

/// <summary>
/// Shared helpers of the eldritch horror. The game logic runs on the server.
/// </summary>
public abstract partial class SharedEldritchHorrorSystem : EntitySystem
{
    [Dependency] protected IPrototypeManager Proto = default!;

    /// <summary>
    /// Whether the horror owns the upgrade.
    /// </summary>
    public bool HasUpgrade(Entity<EldritchHorrorComponent> ent, ProtoId<HorrorUpgradePrototype> upgrade)
    {
        return ent.Comp.Upgrades.Contains(upgrade);
    }

    /// <summary>
    /// Owned upgrades as prototypes.
    /// </summary>
    public IEnumerable<HorrorUpgradePrototype> GetUpgrades(Entity<EldritchHorrorComponent> ent)
    {
        foreach (var id in ent.Comp.Upgrades)
        {
            if (Proto.TryIndex(id, out var upgrade))
                yield return upgrade;
        }
    }

    /// <summary>
    /// Chemicals the horror can inject right now, sorted by name of the reagent.
    /// </summary>
    public List<HorrorChemicalPrototype> GetAvailableChemicals(Entity<EldritchHorrorComponent> ent)
    {
        var list = new List<HorrorChemicalPrototype>();
        foreach (var chem in Proto.EnumeratePrototypes<HorrorChemicalPrototype>())
        {
            if (chem.RequiredUpgrade is { } upgrade && !ent.Comp.Upgrades.Contains(upgrade))
                continue;

            list.Add(chem);
        }

        list.Sort((a, b) => string.CompareOrdinal(a.ID, b.ID));
        return list;
    }

    /// <summary>
    /// The state the horror is in, used to pick which abilities show up.
    /// </summary>
    public static HorrorActionState GetState(EldritchHorrorComponent comp)
    {
        if (comp.Host == null)
            return HorrorActionState.Free;

        return comp.Controlling ? HorrorActionState.Controlling : HorrorActionState.Infesting;
    }
}
