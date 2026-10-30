using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Modules.ModSuit.UI;

[Serializable, NetSerializable]
public sealed class ModSuitComplexityBuiEntry : BaseModSuitBuiEntry
{
    public (int Complexity, int MaxComplexity)? Complexity;

    public ModSuitComplexityBuiEntry(int complexity, int maxComplexity)
    {
        Complexity = (complexity, maxComplexity);
    }

    public ModSuitComplexityBuiEntry((int, int) complexity)
    {
        Complexity = complexity;
    }
}
