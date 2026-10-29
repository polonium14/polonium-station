using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Modules.ModSuit.UI;

[Serializable, NetSerializable]
public sealed partial class ModSuitEjectButtonMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Module;

    public ModSuitEjectButtonMessage(NetEntity module)
    {
        Module = module;
    }
}
