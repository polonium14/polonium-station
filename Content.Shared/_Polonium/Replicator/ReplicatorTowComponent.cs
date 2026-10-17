namespace Content.Shared._Polonium.Replicator;

[RegisterComponent]
public sealed partial class ReplicatorTowComponent : Component
{
    [DataField]
    public float PulledFrictionModifier = 0.2f;

    [DataField]
    public float WeightlessAccelerationModifier = 1f;
}
