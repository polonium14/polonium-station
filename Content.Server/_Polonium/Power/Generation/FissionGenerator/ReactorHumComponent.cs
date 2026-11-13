namespace Content.Server._Polonium.Power.Generation.FissionGenerator;

/// <summary>
/// Turns on the ambient hum of a nuclear reactor while it produces heat.
/// </summary>
[RegisterComponent, Access(typeof(ReactorHumSystem))]
public sealed partial class ReactorHumComponent : Component
{
    /// <summary>
    /// Thermal power from which the reactor hums.
    /// </summary>
    [DataField]
    public float MinThermalPower = 100000f;
}
