using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Vehicles;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TankPilotComponent : Component
{
    [DataField]
    public float EnterDelay = 1.5f;

    [DataField, AutoNetworkedField]
    public EntityUid? Pilot;

    /// <summary> -1..1 przód/tył – tylko serwer </summary>
    public float Forward;

    /// <summary> -1..1 obrót – tylko serwer </summary>
    public float Rotate;
}