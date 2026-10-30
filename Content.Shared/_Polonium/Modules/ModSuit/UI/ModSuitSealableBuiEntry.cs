using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Modules.ModSuit.UI;

[Serializable, NetSerializable]
public sealed class ModSuitSealableBuiEntry : BaseModSuitPowerBuiEntry
{
    public KeyValuePair<NetEntity, ModSuitSealablePartBuiEntry>[] Parts;

    public ModSuitSealableBuiEntry(KeyValuePair<NetEntity, ModSuitSealablePartBuiEntry>[] parts)
    {
        Parts = parts;
    }
}

[Serializable, NetSerializable]
public sealed class ModSuitSealablePartBuiEntry
{
    public readonly SpriteSpecifier Sprite;
    public readonly ModSuitPartType Type;
    public readonly bool IsSealed;

    public ModSuitSealablePartBuiEntry(SpriteSpecifier sprite, ModSuitPartType type, bool isSealed)
    {
        Sprite = sprite;
        Type = type;
        IsSealed = isSealed;
    }
}
