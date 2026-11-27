namespace Content.Server._Polonium.Abductors;

/// <summary>
/// The team's experiment count as this mind last knew it. Lives on the mind so it survives the ship's grid.
/// </summary>
[RegisterComponent, Access(typeof(AbductorShipSystem))]
public sealed partial class AbductorResearcherComponent : Component
{
    [ViewVariables]
    public int Experiments;
}
