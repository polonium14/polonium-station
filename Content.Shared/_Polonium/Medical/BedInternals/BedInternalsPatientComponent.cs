using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Medical.BedInternals;

/// <summary>
/// Added to a patient while a <see cref="BedInternalsComponent"/> bed supplies them with gas.
/// Remembers what the bed took over, so it can be given back when the patient is released.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BedInternalsSystem))]
public sealed partial class BedInternalsPatientComponent : Component
{
    /// <summary>
    /// The tank of the bed that feeds this patient.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? BedTank;

    /// <summary>
    /// The bed's own mask, if the patient had no breath mask and was given one.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? BedMask;

    /// <summary>
    /// The tank that was feeding the patient's internals before the bed took over, to reconnect it afterwards.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? PreviousTank;
}
