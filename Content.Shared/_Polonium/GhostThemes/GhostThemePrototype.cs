using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.GhostThemes;

/// <summary>
/// A look a player can pick for their ghost.
/// </summary>
[Prototype]
public sealed partial class GhostThemePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The name shown in the theme window.
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// The description shown in the theme window.
    /// </summary>
    [DataField(required: true)]
    public LocId Description;

    /// <summary>
    /// The sprite drawn instead of the default ghost, also used as the icon in the theme window.
    /// </summary>
    [DataField(required: true)]
    public SpriteSpecifier.Rsi Sprite = default!;

    /// <summary>
    /// Keeps the default ghost sprite, which also shows how the player died. Only the color changes.
    /// </summary>
    [DataField]
    public bool KeepGhostSprite;

    /// <summary>
    /// Whether the player can pick a color for this theme.
    /// </summary>
    [DataField]
    public bool Colorizable;

    /// <summary>
    /// The tab of the theme window the theme is in. It also decides who can use the theme.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<GhostThemeCategoryPrototype> Category;

    /// <summary>
    /// How long the player must have played in total to unlock the theme.
    /// </summary>
    [DataField]
    public TimeSpan Playtime = TimeSpan.Zero;
}
