using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.CallablePhone;

[Serializable, NetSerializable]
public enum CallablePhoneVisuals : byte
{
    HookState,
    OnHook,
    OffHook,
}
