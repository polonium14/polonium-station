using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Atmos.AirFlow;

/// <summary>
/// Colour of the fog and streaks drawn when a visible gas is carried by rushing air.
/// The ID is the ID of the gas prototype. Gases without an entry use their own UI colour.
/// </summary>
[Prototype]
public sealed partial class AirFlowGasTintPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Color Color;
}
