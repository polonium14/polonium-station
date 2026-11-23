using Robust.Shared.Audio;

namespace Content.Server._Polonium.Abductors;

/// <summary>
/// The research chamber: reads a finished specimen, pays the ship and sends the specimen back to the station.
/// </summary>
[RegisterComponent, Access(typeof(AbductorChamberSystem))]
public sealed partial class AbductorChamberComponent : Component
{
    [DataField]
    public string ContainerId = "specimen";

    [DataField]
    public TimeSpan ExperimentDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long the specimen stays asleep after it is put back on the station.
    /// </summary>
    [DataField]
    public TimeSpan ReturnSleep = TimeSpan.FromSeconds(16);

    [DataField]
    public int Credits = 1;

    /// <summary>
    /// Paid on top of <see cref="Credits"/> when no incision is left open and the specimen is not bleeding.
    /// </summary>
    [DataField]
    public int CleanWorkBonus = 1;

    /// <summary>
    /// Paid on top of <see cref="Credits"/> when the specimen's Mothership order was carried out.
    /// </summary>
    [DataField]
    public int OrderBonus = 1;

    [DataField]
    public SoundSpecifier? StartSound;

    [DataField]
    public SoundSpecifier? SuccessSound;

    [DataField]
    public SoundSpecifier? FailureSound;
}
