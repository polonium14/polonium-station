using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

[RegisterComponent, AutoGenerateComponentPause] // Polonium
public sealed partial class PredictedProjectileClientComponent : Component
{
    [DataField]
    public bool Hit;

    [DataField]
    public EntityCoordinates? Coordinates;

    // hit target waiting until physics is done so we send where the foam dart actually FUCKING STOPPED
    public EntityUid? PendingImpactTarget;

    [AutoPausedField] // Polonium
    public TimeSpan HoldUntil;
}
