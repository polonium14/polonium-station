namespace Content.Shared._Polonium.Replicator;

[RegisterComponent]
public sealed partial class ReplicatorDuplicatorComponent : Component
{
    /// <summary>
    /// Sheets that come out for every sheet put in.
    /// </summary>
    [DataField]
    public int Output = 2;

    [DataField]
    public int BoostNestLevel = 3;

    [DataField]
    public float BoostTimeMultiplier = 0.8f;
}
