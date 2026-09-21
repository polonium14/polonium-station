using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PredictedProjectileServerComponent : Component
{
    public ICommonSession? Shooter;

    [DataField, AutoNetworkedField]
    public int ClientId;

    [DataField, AutoNetworkedField]
    public EntityUid? ClientEnt;

    [DataField]
    public bool Hit;

    // server only - where the predicting client already stuck the shot
    public MapCoordinates? ClientImpact;

    public Vector2 ImpactDirection;
}
