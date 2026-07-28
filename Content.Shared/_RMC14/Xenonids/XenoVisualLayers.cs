using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids;

[Serializable, NetSerializable]
public enum XenoVisualLayers : byte
{
    Base,
    Ovipositor,
    Crest,
    Fortify,
}

[Serializable, NetSerializable]
public enum XenoRestState : byte
{
    NotResting,
    Resting,
}

[Serializable, NetSerializable]
public enum RMCXenoStateVisuals : byte
{
    Resting,
    Downed,
    Dead,
}
