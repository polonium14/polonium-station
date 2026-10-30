using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Modules.ModSuit.UI;

[Serializable, NetSerializable]
public sealed partial class ModSuitToggleButtonMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Module;
    public readonly bool Toggle;

    public ModSuitToggleButtonMessage(NetEntity module, bool toggle)
    {
        Module = module;
        Toggle = toggle;
    }
}
