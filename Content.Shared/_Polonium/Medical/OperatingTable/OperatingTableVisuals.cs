using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Medical.OperatingTable;

[Serializable, NetSerializable]
public enum OperatingTableVisuals : byte
{
    LightOn,
    VitalsState,
}

[Serializable, NetSerializable]
public enum OperatingTableVisualLayers : byte
{
    Base,
    Vitals,
}

/// <summary>
/// What the vitals monitor of an <see cref="OperatingTableLightComponent"/> table shows.
/// </summary>
[Serializable, NetSerializable]
public enum VitalsState : byte
{
    None,
    Healthy,
    Injured,
    Critical,
    Dead,
}
