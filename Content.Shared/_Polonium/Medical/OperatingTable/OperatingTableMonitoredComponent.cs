using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Medical.OperatingTable;

/// <summary>
/// Added to whoever is strapped to a table with <see cref="OperatingTableLightComponent"/>.
/// Lets the table follow only its own patient's health instead of listening to every damaged entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(OperatingTableLightSystem))]
public sealed partial class OperatingTableMonitoredComponent : Component
{
    /// <summary>
    /// The table that shows this entity's vitals.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid Table;
}
