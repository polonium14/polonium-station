namespace Content.Server._Polonium.Abductors;

/// <summary>
/// Objective progress counted from the experiments finished on the mind's ship.
/// </summary>
[RegisterComponent, Access(typeof(AbductorObjectiveSystem))]
public sealed partial class AbductorExperimentsConditionComponent : Component;
