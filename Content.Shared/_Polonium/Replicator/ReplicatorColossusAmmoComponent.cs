using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Replicator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ReplicatorColossusAmmoComponent : Component
{
    [DataField(required: true)]
    public int Capacity;

    [DataField, AutoNetworkedField]
    public int Count;

    [DataField]
    public TimeSpan RechargeCooldown = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? NextCharge;
}
