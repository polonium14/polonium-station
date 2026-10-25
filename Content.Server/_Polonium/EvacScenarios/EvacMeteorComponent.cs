namespace Content.Server._Polonium.EvacScenarios;

/// <summary>
/// A meteor thrown at an evac scenario shuttle, which blows up as soon as it hits it.
/// </summary>
[RegisterComponent, Access(typeof(EvacMeteorShowerSystem))]
public sealed partial class EvacMeteorComponent : Component
{
    [DataField]
    public EntityUid Shuttle;

    [DataField]
    public float IntensityMultiplier = 1f;
}
