using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.CallablePhone;

/// <summary>
/// Raised on an admin ghost client when a player is calling a CentComm phone line.
/// </summary>
[Serializable, NetSerializable]
public sealed class CentCommCallPickupPromptEvent(NetEntity phone, string callerName) : EntityEventArgs
{
    public NetEntity Phone = phone;
    public string CallerName = callerName;
}

/// <summary>
/// Raised by a client when an admin ghost accepts or declines a CentComm call pickup prompt.
/// </summary>
[Serializable, NetSerializable]
public sealed class CentCommCallPickupResponseEvent(NetEntity phone, bool accepted, string? rejectionReason = null) : EntityEventArgs
{
    public const int MaxRejectionReasonLength = 200;

    public NetEntity Phone = phone;
    public bool Accepted = accepted;

    /// <summary>
    /// Custom deny reason from the admin
    /// </summary>
    public string? RejectionReason = rejectionReason;
}
