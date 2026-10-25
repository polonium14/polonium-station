namespace Content.Shared._Polonium.Replicator;

/// <summary>
/// An EMP knocks this replicator out for a moment and drops its cloak.
/// </summary>
[RegisterComponent]
public sealed partial class ReplicatorEmpVulnerableComponent : Component
{
    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(4);

    [DataField]
    public LocId Popup = "replicator-emp-stunned";
}
