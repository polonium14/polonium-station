namespace Content.Server._Polonium.Abductors;

/// <summary>
/// An organ taken out of a specimen, remembering whose it was.
/// </summary>
[RegisterComponent, Access(typeof(AbductorOrderSystem))]
public sealed partial class AbductorHarvestedOrganComponent : Component
{
    [ViewVariables]
    public EntityUid Donor;
}
