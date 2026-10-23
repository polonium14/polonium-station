using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Modules.ModSuit.UI.Modules;

[Serializable, NetSerializable]
[Virtual]
public partial class ModSuitFlashlightModuleBuiEntry : ModSuitBaseToggleableModuleBuiEntry
{
    public override int Priority => 2;

    public readonly Color Color;

    public ModSuitFlashlightModuleBuiEntry(bool toggled, Color color, int? complexity) : base(toggled, complexity)
    {
        Color = color;
    }
}
