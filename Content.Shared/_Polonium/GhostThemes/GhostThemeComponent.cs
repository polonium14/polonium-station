using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.GhostThemes;

/// <summary>
/// The theme a player picked for their ghost. Without it the ghost looks like the default one.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class GhostThemeComponent : Component
{
    /// <summary>
    /// The picked theme.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<GhostThemePrototype>? Theme;

    /// <summary>
    /// The color of the theme. White unless the theme can be recolored.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color Color = Color.White;
}
