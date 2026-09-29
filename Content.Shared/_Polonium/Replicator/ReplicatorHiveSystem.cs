using Content.Shared._Impstation.Replicator;
using Content.Shared.Humanoid;
using Content.Shared.Materials;
using Content.Shared.Mind.Components;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Replicator;

public sealed partial class ReplicatorHiveSystem : EntitySystem
{
    public const int UnitsPerSheet = 100;

    public static readonly ProtoId<TagPrototype> StructureTag = "ReplicatorStructure";

    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedMaterialStorageSystem _materialStorage = default!;

    public bool TryGetHive(EntityUid replicator, out Entity<ReplicatorHiveComponent, ReplicatorNestComponent> hive)
    {
        hive = default;

        if (!TryComp<ReplicatorComponent>(replicator, out var replicatorComp) || replicatorComp.MyNest is not { } nest)
            return false;

        if (TerminatingOrDeleted(nest)
            || !TryComp<ReplicatorHiveComponent>(nest, out var hiveComp)
            || !TryComp<ReplicatorNestComponent>(nest, out var nestComp))
            return false;

        hive = (nest, hiveComp, nestComp);
        return true;
    }

    public int GetBudget(Entity<ReplicatorHiveComponent> hive)
    {
        return _materialStorage.GetMaterialAmount(hive, hive.Comp.Material);
    }

    public bool CanAfford(Entity<ReplicatorHiveComponent> hive, int cost)
    {
        return cost <= 0 || GetBudget(hive) >= cost;
    }

    public bool TrySpend(Entity<ReplicatorHiveComponent> hive, int cost)
    {
        if (cost <= 0)
            return true;

        if (_net.IsClient)
            return CanAfford(hive, cost);

        return _materialStorage.TryChangeMaterialAmount(hive, hive.Comp.Material, -cost);
    }

    public void AddBudget(Entity<ReplicatorHiveComponent> hive, int amount)
    {
        if (amount <= 0 || _net.IsClient)
            return;

        _materialStorage.TryChangeMaterialAmount(hive, hive.Comp.Material, amount);
    }

    public void AddPoints(Entity<ReplicatorHiveComponent> hive, int points)
    {
        AddBudget(hive, points * hive.Comp.UnitsPerPoint);
    }

    public int GetStoredBudget(Entity<ReplicatorHiveComponent> hive, EntityUid uid)
    {
        if (!TryComp<PhysicalCompositionComponent>(uid, out var composition)
            || !composition.MaterialComposition.TryGetValue(hive.Comp.Material, out var perItem))
            return 0;

        var count = TryComp<StackComponent>(uid, out var stack) ? stack.Count : 1;
        return perItem * count;
    }

    public bool CanDigest(EntityUid uid)
    {
        if (HasComp<HumanoidProfileComponent>(uid))
            return false;

        return HasComp<ReplicatorComponent>(uid) || !HoldsMind(uid);
    }

    public bool IsProtected(EntityUid uid)
    {
        return HasComp<HumanoidProfileComponent>(uid) || HoldsMind(uid);
    }

    public static double ToSheets(int units)
    {
        return Math.Round(units / (double)UnitsPerSheet, 2);
    }

    private bool HoldsMind(EntityUid uid)
    {
        return TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind;
    }
}
