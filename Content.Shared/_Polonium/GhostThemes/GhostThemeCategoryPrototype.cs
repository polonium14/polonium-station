using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.GhostThemes;

/// <summary>
/// A group of ghost themes: a tab in the theme window and the players who can use its themes.
/// </summary>
[Prototype]
public sealed partial class GhostThemeCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The title of the tab.
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Tabs are sorted by this, lowest first.
    /// </summary>
    [DataField]
    public int Priority;

    /// <summary>
    /// Who can use the themes of this category.
    /// </summary>
    [DataField]
    public GhostThemeAccess Access = GhostThemeAccess.Everyone;

    /// <summary>
    /// Hides the tab from players who cannot use any of its themes.
    /// </summary>
    [DataField]
    public bool HideIfLocked;
}

/// <summary>
/// Who can use the themes of a <see cref="GhostThemeCategoryPrototype"/>.
/// </summary>
public enum GhostThemeAccess : byte
{
    /// <summary>
    /// Every player, once they have played long enough for the theme.
    /// </summary>
    Everyone,

    /// <summary>
    /// Players who support the server.
    /// </summary>
    Supporters,

    /// <summary>
    /// Players with the contributor role on Discord.
    /// </summary>
    Contributors,

    /// <summary>
    /// Admins, including the ones who deadminned.
    /// </summary>
    Admins,
}
