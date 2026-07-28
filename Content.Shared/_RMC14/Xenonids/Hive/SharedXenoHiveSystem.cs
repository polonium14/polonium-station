using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Hive;

public abstract partial class SharedXenoHiveSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;

    private EntityQuery<HiveComponent> _hiveQuery;
    private EntityQuery<HiveMemberComponent> _memberQuery;

    public override void Initialize()
    {
        _hiveQuery = GetEntityQuery<HiveComponent>();
        _memberQuery = GetEntityQuery<HiveMemberComponent>();

        // Queen assignment on evolve is handled by SetHive when TransferXeno copies the hive.
        SubscribeLocalEvent<XenoEvolutionGranterComponent, MobStateChangedEvent>(OnGranterMobStateChanged);
        SubscribeLocalEvent<XenoEvolutionGranterComponent, EntityTerminatingEvent>(OnGranterTerminating);
        SubscribeLocalEvent<AutoAssignHiveComponent, MapInitEvent>(OnAutoAssignHive);
    }

    private void OnGranterMobStateChanged(Entity<XenoEvolutionGranterComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        ClearHiveQueen(ent.Owner);
    }

    private void OnGranterTerminating(Entity<XenoEvolutionGranterComponent> ent, ref EntityTerminatingEvent args)
    {
        if (_mobState.IsDead(ent))
            return;

        ClearHiveQueen(ent.Owner);
    }

    private void OnAutoAssignHive(Entity<AutoAssignHiveComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        if (ent.Comp.Hive is { } existing && Exists(existing))
        {
            SetHive(ent.Owner, existing);
            return;
        }

        if (ent.Comp.HiveId is not { } hiveId)
            return;

        // reuse first matching hive if one already exists
        var query = EntityQueryEnumerator<HiveComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == hiveId.Id)
            {
                SetHive(ent.Owner, uid);
                return;
            }
        }

        var spawned = Spawn(hiveId, MapCoordinates.Nullspace);
        SetHive(ent.Owner, spawned);
    }

    public Entity<HiveComponent>? GetHive(Entity<HiveMemberComponent?> member)
    {
        if (!_memberQuery.Resolve(member, ref member.Comp, false))
            return null;

        if (member.Comp.Hive is not { } uid || TerminatingOrDeleted(uid))
            return null;

        if (!_hiveQuery.TryComp(uid, out var comp))
            return null;

        return (uid, comp);
    }

    public bool TryGetHive(Entity<HiveMemberComponent?> member, [NotNullWhen(true)] out EntityUid? hive)
    {
        if (GetHive(member) is { } found)
        {
            hive = found.Owner;
            return true;
        }

        hive = null;
        return false;
    }

    public void SetHive(Entity<HiveMemberComponent?> member, EntityUid? hive)
    {
        var comp = member.Comp ?? EnsureComp<HiveMemberComponent>(member);
        var old = comp.Hive;
        if (old == hive)
            return;

        if (hive != null && !_hiveQuery.HasComp(hive.Value))
        {
            Log.Error($"Tried to set hive of {ToPrettyString(member)} to bad hive entity {ToPrettyString(hive)}");
            return;
        }

        if (HasComp<XenoEvolutionGranterComponent>(member) &&
            old is { } oldHiveUid &&
            _hiveQuery.TryComp(oldHiveUid, out var oldHiveComp) &&
            oldHiveComp.CurrentQueen == member.Owner)
        {
            ClearHiveQueen((oldHiveUid, oldHiveComp));
        }

        comp.Hive = hive;
        Dirty(member, comp);

        if (HasComp<XenoEvolutionGranterComponent>(member) &&
            hive != null &&
            _hiveQuery.TryComp(hive.Value, out var hiveComp))
        {
            SetHiveQueen(member.Owner, (hive.Value, hiveComp));
        }

        var ev = new HiveChangedEvent(hive, old);
        RaiseLocalEvent(member, ref ev);
    }

    public void SetSameHive(Entity<HiveMemberComponent?> src, Entity<HiveMemberComponent?> dest)
    {
        if (GetHive(src) is { } hive)
            SetHive(dest, hive);
    }

    public bool FromSameHive(Entity<HiveMemberComponent?> a, Entity<HiveMemberComponent?> b)
    {
        if (GetHive(a) is not { } aHive)
            return false;

        return IsMember(b, aHive);
    }

    public bool IsMember(Entity<HiveMemberComponent?> member, EntityUid? hive)
    {
        if (hive == null || GetHive(member) is not { } memberHive)
            return false;

        return memberHive.Owner == hive;
    }

    public bool SetHiveQueen(EntityUid queen, Entity<HiveComponent> hive)
    {
        if (hive.Comp.CurrentQueen == queen)
            return true;

        hive.Comp.CurrentQueen = queen;
        Dirty(hive);
        return true;
    }

    private void ClearHiveQueen(EntityUid queen)
    {
        if (GetHive(queen) is not { } hive || hive.Comp.CurrentQueen != queen)
            return;

        ClearHiveQueen(hive);
    }

    private void ClearHiveQueen(Entity<HiveComponent> hive)
    {
        hive.Comp.CurrentQueen = null;
        Dirty(hive);
    }
}
