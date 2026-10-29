using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Modules.ModSuit.UI;

[Serializable, NetSerializable]
public sealed partial class ModSuitSealButtonMessage : BoundUserInterfaceMessage
{
    public readonly Dictionary<NetEntity, bool> Parts;

    public ModSuitSealButtonMessage(Dictionary<NetEntity, bool> parts)
    {
        Parts = parts;
    }
}
