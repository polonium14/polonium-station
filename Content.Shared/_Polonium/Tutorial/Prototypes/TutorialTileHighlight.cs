using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Prototypes;

/// <summary>
/// A tile the trainee should stand on or build on, marked with the tile-point sprite nothing can click. With
/// <see cref="To"/> it is the whole run of tiles between two anchors, like the route of a cable.
/// </summary>
[DataDefinition]
public sealed partial class TutorialTileHighlight
{
    [DataField(required: true)]
    public string Anchor = string.Empty;

    /// <summary>Mark every tile on the way to this anchor too.</summary>
    [DataField]
    public string? To;

    /// <summary>A tile drops its marker once one of these stands on it.</summary>
    [DataField]
    public List<EntProtoId> Until = new();
}
