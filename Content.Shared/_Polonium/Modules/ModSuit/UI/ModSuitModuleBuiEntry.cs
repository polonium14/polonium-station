using Content.Shared._Polonium.Modules.ModSuit.UI.Modules;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Modules.ModSuit.UI;

[Serializable, NetSerializable]
public sealed class ModSuitModuleBuiEntry : BaseModSuitBuiEntry
{
    public KeyValuePair<NetEntity, ModSuitBaseModuleBuiEntry>[] Modules;

    public ModSuitModuleBuiEntry()
    {
        Modules = [];
    }

    public ModSuitModuleBuiEntry(KeyValuePair<NetEntity, ModSuitBaseModuleBuiEntry>[] modules)
    {
        Modules = modules;
    }
}
