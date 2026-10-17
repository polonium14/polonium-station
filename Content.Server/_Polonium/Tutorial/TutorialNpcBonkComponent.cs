using Robust.Shared.Map;

namespace Content.Server._Polonium.Tutorial;

public enum TutorialBonkStage : byte
{
    Waiting,
    Walking,
    Settling,
    Climbing,
}

/// <summary>Walks one patient to a table, lets him try to climb it and puts him down. Gone once he is.</summary>
[RegisterComponent]
public sealed partial class TutorialNpcBonkComponent : Component
{
    [ViewVariables]
    public EntityUid Table;

    [ViewVariables]
    public float Blunt;

    [ViewVariables]
    public TutorialBonkStage Stage;

    /// <summary>The tile beside the table he walks to.</summary>
    [ViewVariables]
    public EntityCoordinates Target;

    /// <summary>When the current stage stops waiting and moves on regardless.</summary>
    [ViewVariables]
    public TimeSpan NextAt;
}
