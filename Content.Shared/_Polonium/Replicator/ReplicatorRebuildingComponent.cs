using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Replicator;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ReplicatorRebuildingComponent : Component
{
    [ViewVariables]
    public EntityUid Nest;

    [ViewVariables]
    public int Tier;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan FinishTime;
}
