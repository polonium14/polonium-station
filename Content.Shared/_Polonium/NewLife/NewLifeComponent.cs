using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.NewLife;

/// <summary>
/// Put on the ghost of a player who lost their character. Lets them go back to the lobby
/// and join again as someone new once <see cref="CCVar.CCVars.NewLifeDelay"/> has passed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NewLifeComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan GhostedAt;

    /// <summary>
    /// New lives the player had already taken this round when this ghost was made, checked against <see cref="CCVar.CCVars.NewLifeMaxNewLives"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int UsedLives;
}
