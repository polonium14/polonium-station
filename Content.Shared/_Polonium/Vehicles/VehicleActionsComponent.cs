using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Vehicles;

/// <summary>
/// Gives these actions to whoever is operating the vehicle.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(VehicleActionsSystem))]
public sealed partial class VehicleActionsComponent : Component
{
    [DataField(required: true), AutoNetworkedField, AlwaysPushInheritance]
    public List<EntProtoId> Actions = new();

    [DataField, AutoNetworkedField]
    public List<EntityUid> ActionEntities = new();
}

public sealed partial class VehicleSignalActionEvent : InstantActionEvent;
