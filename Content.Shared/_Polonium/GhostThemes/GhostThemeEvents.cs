using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.GhostThemes;

/// <summary>
/// Sent by the client to learn which ghost themes the player can use.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestGhostThemesEvent : EntityEventArgs;

/// <summary>
/// The server's answer to <see cref="RequestGhostThemesEvent"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class GhostThemesAvailableEvent(HashSet<ProtoId<GhostThemePrototype>> available) : EntityEventArgs
{
    /// <summary>
    /// The themes the player can use right now.
    /// </summary>
    public HashSet<ProtoId<GhostThemePrototype>> Available = available;
}

/// <summary>
/// Sent by the client to put a theme on the player's ghost.
/// </summary>
[Serializable, NetSerializable]
public sealed class SelectGhostThemeEvent(ProtoId<GhostThemePrototype> theme, Color color) : EntityEventArgs
{
    /// <summary>
    /// The picked theme.
    /// </summary>
    public ProtoId<GhostThemePrototype> Theme = theme;

    /// <summary>
    /// The picked color. Ignored for themes that cannot be recolored.
    /// </summary>
    public Color Color = color;
}
