using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Modules.ModSuit.UI.Modules;

[Serializable, NetSerializable]
[Virtual]
public partial class ModSuitBaseToggleableModuleBuiEntry : ModSuitBaseModuleBuiEntry
{
    public override int Priority => 1;

    public readonly bool Toggled;

    public ModSuitBaseToggleableModuleBuiEntry(bool toggled, int? complexity) : base(complexity)
    {
        Toggled = toggled;
    }
}
