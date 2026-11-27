using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Smoking;

/// <summary>
/// A smokable with one of these reagents in it blows up when lit. The server sums the intensity of every listed
/// reagent and deletes the smokable.
/// </summary>
[RegisterComponent]
public sealed partial class SmokableExplosiveComponent : Component
{
    /// <summary>
    /// Explosion intensity added per unit of each reagent.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> Reagents = new()
    {
        { "Plasma", 1.5f },
        { "WeldingFuel", 0.5f },
    };

    /// <summary>
    /// Intensity drop per tile away from the smokable. High, so the blast stays close to the smoker.
    /// </summary>
    [DataField]
    public float Slope = 5f;

    /// <summary>
    /// Cap on the intensity of a single tile.
    /// </summary>
    [DataField]
    public float MaxTileIntensity = 4f;
}
