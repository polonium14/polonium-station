namespace Content.Server._Polonium.FlashMessage;

/// <summary>
/// Put this on a <c>FlashComponent</c> item to show flavor text to anyone it flashes.
/// Fires on every flash (in-hand AoE and melee). Flash-immune targets (sunglasses,
/// welding masks) never get flashed, so they never see either message.
/// </summary>
[RegisterComponent, Access(typeof(FlashMessageSystem))]
public sealed partial class FlashMessageComponent : Component
{
    /// <summary>
    /// Private second-person popup shown only to the flashed target.
    /// </summary>
    [DataField(required: true)]
    public LocId Popup;

    /// <summary>
    /// Third-person emote action performed by the flashed target, visible to everyone nearby.
    /// The target's name is prepended automatically (e.g. "Bob looks confused.").
    /// </summary>
    [DataField(required: true)]
    public LocId Emote;

    /// <summary>
    /// Whether the flashed target performs the public <see cref="Emote"/>.
    /// Set false to show only the private popup.
    /// </summary>
    [DataField]
    public bool DoEmote = true;
}
