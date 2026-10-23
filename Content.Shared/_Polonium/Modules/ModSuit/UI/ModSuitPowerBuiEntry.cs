using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Modules.ModSuit.UI;

[Serializable, NetSerializable]
public sealed class ModSuitPowerBuiEntry : BaseModSuitPowerBuiEntry
{
    public float CurrentCharge;
    public float MaxCharge;

    public ModSuitPowerBuiEntry(float currentCharge, float maxCharge)
    {
        CurrentCharge = currentCharge;
        MaxCharge = maxCharge;
    }
}
