using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Abductors;

/// <summary>
/// Something the Curator of the Probing Division says to abductors at one moment of the round.
/// One of the variants is picked each time and its pages are shown one after another.
/// </summary>
[Prototype]
public sealed partial class AbductorCuratorLinePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<List<LocId>> Variants = new();
}
