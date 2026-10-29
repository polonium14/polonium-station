using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Modules.ModSuit.UI.Modules;

[Serializable, NetSerializable]
[Virtual]
public partial class ModSuitBaseModuleBuiEntry
{
    public virtual int Priority => 0;

    public readonly int? Complexity;

    public ModSuitBaseModuleBuiEntry(int? complexity)
    {
        Complexity = complexity;
    }
}
