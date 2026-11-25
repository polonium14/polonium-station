namespace Content.Server._Polonium.Abductors;

/// <summary>
/// Brings the abductors' ship next to the station and greets its crew.
/// </summary>
[RegisterComponent, Access(typeof(AbductorRuleSystem))]
public sealed partial class AbductorRuleComponent : Component
{
    [ViewVariables]
    public EntityUid? Ship;
}
