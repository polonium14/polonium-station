using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Medical.Cpr;

/// <summary>
/// Tells clients to play the push of a rescuer giving chest compressions.
/// </summary>
[Serializable, NetSerializable]
public sealed class CprLungeEvent(NetEntity rescuer) : EntityEventArgs
{
    public NetEntity Rescuer = rescuer;
}
