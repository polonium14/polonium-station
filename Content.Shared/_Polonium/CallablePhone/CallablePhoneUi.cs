using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.CallablePhone;

[Serializable, NetSerializable]
public enum CallablePhoneUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CallablePhoneBoundInterfaceState : BoundUserInterfaceState
{
    public readonly Dictionary<NetEntity, string> Phones;

    public CallablePhoneBoundInterfaceState(Dictionary<NetEntity, string> phones)
    {
        Phones = phones;
    }
}

[Serializable, NetSerializable]
public sealed class CallablePhoneCallMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Receiver;

    public CallablePhoneCallMessage(NetEntity receiver)
    {
        Receiver = receiver;
    }
}

[Serializable, NetSerializable]
public sealed class CallablePhoneAnswerMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CallablePhoneHangUpMessage : BoundUserInterfaceMessage;
