namespace Content.Shared._Polonium.Replicator;

/// <summary>
/// Drives the nest's local point light from its level, with a slow breathing pulse.
/// </summary>
[RegisterComponent]
public sealed partial class ReplicatorNestGlowComponent : Component
{
    [DataField]
    public Color Color = Color.FromHex("#c45cff");

    /// <summary>
    /// The light a nest gives from each level on. Levels without an entry below them stay dark.
    /// </summary>
    [DataField(required: true)]
    public List<ReplicatorNestGlowLevel> Levels = new();

    [DataField]
    public TimeSpan PulsePeriod = TimeSpan.FromSeconds(4);

    /// <summary>
    /// How far the energy swings around its level value, as a fraction of it.
    /// </summary>
    [DataField]
    public float PulseDepth = 0.2f;
}

[DataDefinition]
public sealed partial class ReplicatorNestGlowLevel
{
    [DataField(required: true)]
    public int Level;

    [DataField(required: true)]
    public float Radius;

    [DataField(required: true)]
    public float Energy;
}
