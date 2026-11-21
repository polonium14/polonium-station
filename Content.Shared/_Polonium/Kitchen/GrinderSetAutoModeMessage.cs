using Content.Shared.Kitchen;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Kitchen;

/// <summary>
/// Sent by the client to put the automatic mode of a reagent grinder into one given position.
/// </summary>
[Serializable, NetSerializable]
public sealed class GrinderSetAutoModeMessage(GrinderAutoMode mode) : BoundUserInterfaceMessage
{
    public GrinderAutoMode Mode = mode;
}
