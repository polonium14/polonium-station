using Content.Shared.Atmos.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Piping.Trinary.Components
{
    [Serializable, NetSerializable]
    public enum GasFilterUiKey
    {
        Key,
    }

    [Serializable, NetSerializable]
    public sealed class GasFilterBoundUserInterfaceState : BoundUserInterfaceState
    {
        public string FilterLabel { get; }
        public float TransferRate { get; }
        public bool Enabled { get; }
        public HashSet<Gas>? FilterGases { get; } // Funky - for filtering of multiple gases

        // Funky - removed for filtering of multiple gases
        // public Gas? FilteredGas { get; }

        // Funky - removed for filtering of multiple gases
        // public GasFilterBoundUserInterfaceState(string filterLabel, float transferRate, bool enabled, Gas? filteredGas)
        // {
        //     FilterLabel = filterLabel;
        //     TransferRate = transferRate;
        //     Enabled = enabled;
        //     FilteredGas = filteredGas;
        // }

        public GasFilterBoundUserInterfaceState(string filterLabel, float transferRate, bool enabled, HashSet<Gas>? filterGases)
        {
            FilterLabel = filterLabel;
            TransferRate = transferRate;
            Enabled = enabled;
            FilterGases = filterGases;
        }
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
    public sealed class GasFilterChangeGasesMessage : BoundUserInterfaceMessage
    {
        public HashSet<Gas> Gases { get; }

        public GasFilterChangeGasesMessage(HashSet<Gas> gases)
        {
            Gases = gases;
        }
    }

    // Funky - removed for filtering of multiple gases
    // public sealed class GasFilterSelectGasMessage : BoundUserInterfaceMessage
    // {
    //     public int? ID { get; }
    //     public GasFilterSelectGasMessage(int? id)
    //     {
    //         ID = id;
    //     }
    // }
}
