using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Replicator;

[RegisterComponent]
public sealed partial class ReplicatorColossusGaitComponent : Component
{
    [DataField(required: true)]
    public ResPath Sprite;

    [DataField(required: true)]
    public ResPath StrafeSprite;

    [DataField(required: true)]
    public float TilesPerLoop;

    [DataField]
    public int StepsPerLoop = 2;

    [DataField]
    public SoundSpecifier? StepSound;

    [ViewVariables]
    public Vector2 LastPosition;

    [ViewVariables]
    public Vector2 Velocity;

    [ViewVariables]
    public float Phase;

    [ViewVariables]
    public TimeSpan LastMoved;

    [ViewVariables]
    public bool Tracking;

    [ViewVariables]
    public bool StrafeSpriteActive;
}
