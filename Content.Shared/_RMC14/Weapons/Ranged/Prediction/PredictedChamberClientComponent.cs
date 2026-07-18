using Robust.Shared.GameObjects;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

/// <summary>
/// Client-side chamber round tracked outside networked container slots during gun prediction.
/// </summary>
[RegisterComponent]
public sealed partial class PredictedChamberClientComponent : Component
{
    [DataField]
    public EntityUid? Round;
}
