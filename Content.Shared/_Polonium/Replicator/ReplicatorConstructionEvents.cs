using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Replicator;

public sealed partial class ReplicatorConvertActionEvent : WorldTargetActionEvent
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(0.5);

    [DataField]
    public int WallCost = 5;

    [DataField]
    public int TileCost;

    [DataField(required: true)]
    public EntProtoId Wall;

    [DataField(required: true)]
    public EntProtoId ReinforcedWall;

    /// <summary>
    /// Walls whose destruction threshold is at least this much damage become <see cref="ReinforcedWall"/>.
    /// </summary>
    [DataField]
    public float ReinforcedThreshold = 300;

    [DataField(required: true)]
    public ProtoId<ContentTileDefinition> Tile;

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
    public bool Solid = true;

    [DataField]
    public EntProtoId? Effect;
}

[Serializable, NetSerializable]
public sealed partial class ReplicatorConvertDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates Location;

    [DataField]
    public NetEntity? Wall;

    [DataField]
    public EntProtoId? Replacement;

    [DataField]
    public ProtoId<ContentTileDefinition>? Tile;

    [DataField]
    public int Cost;

    [DataField]
    public EntProtoId? Effect;

    private ReplicatorConvertDoAfterEvent()
    {
    }

    public ReplicatorConvertDoAfterEvent(NetCoordinates location, NetEntity? wall, EntProtoId? replacement,
        ProtoId<ContentTileDefinition>? tile, int cost, EntProtoId? effect)
    {
        Location = location;
        Wall = wall;
        Replacement = replacement;
        Tile = tile;
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
    public bool Solid;

    [DataField]
    public NetEntity? Effect;

    private ReplicatorBuildDoAfterEvent()
    {
    }

    public ReplicatorBuildDoAfterEvent(NetCoordinates location, EntProtoId prototype, int cost, bool solid, NetEntity? effect)
    {
        Location = location;
        Prototype = prototype;
        Cost = cost;
        Solid = solid;
        Effect = effect;
    }

    public override DoAfterEvent Clone() => this;
}
