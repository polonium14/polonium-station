using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Medical.BedInternals;

[Serializable, NetSerializable]
public enum BedInternalsVisuals : byte
{
    TankVisual,
}

/// <summary>
/// Which tank overlay a bed with <see cref="BedInternalsComponent"/> shows.
/// </summary>
[Serializable, NetSerializable]
public enum BedTankVisual : byte
{
    None,
    Nitrogen,
    Oxygen,
    Generic,
    Plasma,
    Nitrous,
}
