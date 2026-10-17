namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// Combat mode is the switch behind shoving, swinging and most of security. Worth a step of its
/// own rather than a clause inside "now hit him".
/// </summary>
public sealed partial class CombatModeCondition : TutorialCondition
{
    [DataField]
    public bool Enabled = true;
}
