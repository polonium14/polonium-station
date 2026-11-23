using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Abductors;

/// <summary>
/// An extra procedure the Mothership can request on a specimen for a bonus credit.
/// </summary>
[Prototype]
public sealed partial class AbductorOrderPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The organ or limb that has to be replaced.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<OrganCategoryPrototype> Category;

    /// <summary>
    /// Whether the replacement has to come out of another specimen rather than the biofabricator.
    /// </summary>
    [DataField]
    public bool FromDonor;

    [DataField]
    public float Weight = 1f;

    [DataField(required: true)]
    public ProtoId<AbductorCuratorLinePrototype> Line;
}
