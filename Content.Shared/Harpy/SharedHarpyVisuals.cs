using Robust.Shared.Serialization;

namespace Content.Shared.Harpy;

[Serializable, NetSerializable]
public enum HarpyVisualLayers : byte
{
    Singing,
}

[Serializable, NetSerializable]
public enum SingingVisualLayer : byte
{
    False,
    True,
}
