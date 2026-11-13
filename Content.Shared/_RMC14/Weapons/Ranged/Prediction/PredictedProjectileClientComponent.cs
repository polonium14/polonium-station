using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Timing;

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

    // Polonium start: the twin is drawn between its last two tick positions, nothing else lerps it
    public EntityUid LerpParent;
    public GameTick LerpTick;
    public Vector2 LerpFrom;
    public Vector2 LerpTo;
    public bool Lerped;
    // Polonium end
}
