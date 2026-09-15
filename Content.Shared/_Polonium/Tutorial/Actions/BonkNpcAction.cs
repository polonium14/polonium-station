namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>
/// The patient walks up to a table on his own, tries to climb it and cracks his head the way a clumsy
/// monkey does - only this one does not get up. The asphyxiation is worked out on the spot, so one
/// defibrillator zap is enough to bring him back.
/// </summary>
public sealed partial class BonkNpcAction : TutorialAction
{
    [DataField(required: true)]
    public string NpcAnchorId = string.Empty;

    [DataField(required: true)]
    public string TableAnchorId = string.Empty;

    [DataField]
    public float Blunt = 15f;

    /// <summary>Seconds before he gets up and sets off, so the trainee is looking when he does.</summary>
    [DataField]
    public float Delay = 3f;
}
