namespace Content.Server._Polonium.GameTicking;

/// <summary>
/// Keeps a game rule out of the automatic event rotation until the named boolean CVar is on.
/// Admins can still start the rule by hand.
/// </summary>
[RegisterComponent]
public sealed partial class GameRuleFeatureSwitchComponent : Component
{
    [DataField("cvar", required: true)]
    public string CVar = string.Empty;
}
