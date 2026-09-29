using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Replicator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ReplicatorRecallImmuneComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndTime;

    [DataField]
    public TimeSpan FadeWarning = TimeSpan.FromSeconds(5);

    [DataField]
    public ProtoId<AlertPrototype> Alert = "ReplicatorRecallImmune";

    [DataField]
    public ResPath TraceSprite = new("/Textures/_Polonium/Effects/replicator_trace.rsi");

    [DataField]
    public string TraceState = "trace";

    [DataField]
    public string FadingState = "trace_fading";
}

[Serializable, NetSerializable]
public enum ReplicatorRecallImmuneVisuals : byte
{
    Trace,
}
