using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// One of the prototypes on the tile of every listed anchor, like a flatpack on each engine marker.
/// Goes by tile rather than range, because unpacking and anchoring both snap to the tile.
/// With <see cref="Stray"/> it flips around: true once a match stands near the markers but on none of them.
/// </summary>
public sealed partial class PrototypesOnAnchorsCondition : TutorialCondition
{
    [DataField(required: true)]
    public List<string> AnchorIds = new();

    [DataField(required: true)]
    public List<EntProtoId> Prototypes = new();

    /// <summary>Only count things bolted to the floor.</summary>
    [DataField]
    public bool Anchored;

    [DataField]
    public bool Stray;

    /// <summary>How far from the markers a stray still counts as theirs.</summary>
    [DataField]
    public float StrayRange = 3f;

    /// <summary>Compare the facing with the marker's, for things like a cable terminal that only works one way round.</summary>
    [DataField]
    public TutorialRotationCheck Rotation = TutorialRotationCheck.Any;
}

public enum TutorialRotationCheck : byte
{
    Any,
    Match,
    Mismatch,
}
