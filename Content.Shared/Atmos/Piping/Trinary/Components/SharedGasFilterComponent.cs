using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Piping.Trinary.Components;

[Serializable, NetSerializable]
public enum GasFilterUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class GasFilterToggleStatusMessage : BoundUserInterfaceMessage
{
    public bool Enabled { get; }

    public GasFilterToggleStatusMessage(bool enabled)
    {
        Enabled = enabled;
    }
}

[Serializable, NetSerializable]
public sealed class GasFilterChangeRateMessage : BoundUserInterfaceMessage
{
    public float Rate { get; }

    public GasFilterChangeRateMessage(float rate)
    {
        Rate = rate;
    }
}

[Serializable, NetSerializable]
public sealed class GasFilterSelectGasMessage(Gas? gas) : BoundUserInterfaceMessage
{
    public readonly Gas? Gas = gas;
}

/// <summary>
/// Sets multiple gases to filter at once.
/// </summary>
[Serializable, NetSerializable]
public sealed class GasFilterChangeGasesMessage : BoundUserInterfaceMessage
{
    public HashSet<Gas> Gases { get; }

    public GasFilterChangeGasesMessage(HashSet<Gas> gases)
    {
        Gases = gases;
    }
}
