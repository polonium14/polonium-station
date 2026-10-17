using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Replicator;

public sealed partial class ReplicatorConvertFloorActionEvent : InstantActionEvent
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(0.5);

    [DataField]
    public int Cost = 10;

    [DataField(required: true)]
    public ProtoId<ContentTileDefinition> Tile;

    [DataField]
    public EntProtoId? Effect;
}

public sealed partial class ReplicatorConvertWallActionEvent : WorldTargetActionEvent
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(0.75);

    [DataField]
    public int Cost = 20;

    [DataField(required: true)]
    public EntProtoId Wall;

    [DataField(required: true)]
    public EntProtoId ReinforcedWall;

    /// <summary>
    /// Walls whose destruction threshold is at least this much damage become <see cref="ReinforcedWall"/>.
    /// </summary>
    [DataField]
    public float ReinforcedThreshold = 300;

    [DataField]
    public EntProtoId? Effect;
}

public sealed partial class ReplicatorReinforceWallActionEvent : WorldTargetActionEvent
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(8);

    [DataField]
    public int Cost = 300;

    /// <summary>
    /// Only walls of this prototype can be reinforced.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Wall;

    [DataField(required: true)]
    public EntProtoId ReinforcedWall;

    [DataField]
    public EntProtoId? Effect;
}

public sealed partial class ReplicatorBuildActionEvent : WorldTargetActionEvent
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public int Cost;

    [DataField]
    public TimeSpan Delay;

    [DataField]
    public EntProtoId? Effect;
}

public sealed partial class ReplicatorHolowallActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public int Cost;
}

[ByRefEvent]
public record struct ReplicatorBuildAttemptEvent(EntityUid User, EntProtoId Prototype, EntityCoordinates Location)
{
    public string? FailReason;
}

[Serializable, NetSerializable]
public sealed partial class ReplicatorConvertFloorDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates Location;

    [DataField]
    public ProtoId<ContentTileDefinition> Tile;

    [DataField]
    public int Cost;

    [DataField]
    public EntProtoId? Effect;

    private ReplicatorConvertFloorDoAfterEvent()
    {
    }

    public ReplicatorConvertFloorDoAfterEvent(NetCoordinates location, ProtoId<ContentTileDefinition> tile, int cost, EntProtoId? effect)
    {
        Location = location;
        Tile = tile;
        Cost = cost;
        Effect = effect;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class ReplicatorReinforceDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates Location;

    [DataField]
    public NetEntity Wall;

    [DataField]
    public EntProtoId Replacement;

    [DataField]
    public int Cost;

    [DataField]
    public EntProtoId? Effect;

    private ReplicatorReinforceDoAfterEvent()
    {
    }

    public ReplicatorReinforceDoAfterEvent(NetCoordinates location, NetEntity wall, EntProtoId replacement, int cost, EntProtoId? effect)
    {
        Location = location;
        Wall = wall;
        Replacement = replacement;
        Cost = cost;
        Effect = effect;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class ReplicatorConvertDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates Location;

    [DataField]
    public NetEntity Wall;

    [DataField]
    public EntProtoId Replacement;

    [DataField]
    public int Cost;

    [DataField]
    public EntProtoId? Effect;

    private ReplicatorConvertDoAfterEvent()
    {
    }

    public ReplicatorConvertDoAfterEvent(NetCoordinates location, NetEntity wall, EntProtoId replacement, int cost, EntProtoId? effect)
    {
        Location = location;
        Wall = wall;
        Replacement = replacement;
        Cost = cost;
        Effect = effect;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class ReplicatorBuildDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates Location;

    [DataField]
    public EntProtoId Prototype;

    [DataField]
    public int Cost;

    [DataField]
    public NetEntity? Effect;

    /// <summary>
    /// Set when the build is paid with the sheets in hand instead of the nest's stock.
    /// </summary>
    [DataField]
    public ProtoId<ReplicatorStructurePrototype>? Structure;

    private ReplicatorBuildDoAfterEvent()
    {
    }

    public ReplicatorBuildDoAfterEvent(NetCoordinates location, EntProtoId prototype, int cost, NetEntity? effect,
        ProtoId<ReplicatorStructurePrototype>? structure = null)
    {
        Location = location;
        Prototype = prototype;
        Cost = cost;
        Effect = effect;
        Structure = structure;
    }

    public override DoAfterEvent Clone() => this;
}
