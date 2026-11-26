using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Smoking;

/// <summary>
/// Sent to clients near a smoker who blew out a cloud of smoke, so they draw it at the smoker's mouth.
/// </summary>
[Serializable, NetSerializable]
public sealed class SmokePuffEvent(NetEntity smoker) : EntityEventArgs
{
    public readonly NetEntity Smoker = smoker;
}
