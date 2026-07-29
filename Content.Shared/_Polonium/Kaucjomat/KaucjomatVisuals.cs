using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Kaucjomat;

[Serializable, NetSerializable]
public enum KaucjomatVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum KaucjomatVisualState : byte
{
    Off,
    Normal,
    Accept,
    Deny,
    Broken,
}

[Serializable, NetSerializable]
public enum KaucjomatVisualLayers : byte
{
    Base,
    Screen,
}
