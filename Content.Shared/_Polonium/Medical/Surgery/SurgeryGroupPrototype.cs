using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Medical.Surgery;

/// <summary>
/// A heading in the surgery window and the surgeries listed under it, in the order they are shown.
/// Surgeries that no group lists land under a catch-all heading at the bottom.
/// </summary>
[Prototype]
public sealed partial class SurgeryGroupPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The heading shown above the group.
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Groups are shown from the lowest order to the highest.
    /// </summary>
    [DataField]
    public int Order;

    /// <summary>
    /// The surgeries of this group, in display order.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> Surgeries = [];
}
