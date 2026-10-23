using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Modules.ModSuit.UI;

[Serializable, NetSerializable]
public sealed partial class ModSuitFlashlightColorChangedMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Module;
    public readonly Color Color;

    public ModSuitFlashlightColorChangedMessage(NetEntity module, Color color)
    {
        Module = module;
        Color = color;
    }
}
