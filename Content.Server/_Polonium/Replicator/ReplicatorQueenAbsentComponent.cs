using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Polonium.Replicator;

/// <summary>
/// Sits on a queen whose player has disconnected. She keeps the crown until the deadline passes.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ReplicatorQueenAbsentComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan Deadline;
}
