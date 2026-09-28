namespace Content.Shared._Polonium.GameTicking;

[RegisterComponent]
public sealed partial class GameRuleMaxPlayersComponent : Component
{
    [DataField(required: true)]
    public int MaxPlayers;
}
