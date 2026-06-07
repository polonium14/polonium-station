using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.GameTicking;

/// <summary>
/// Announces a localized message when the gamerule starts (after any GameRule delay).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DelayedAnnouncementRuleComponent : Component
{
    [DataField(required: true)]
    public LocId Announcement;

    [DataField]
    public LocId? Sender;

    [DataField]
    public Color Color = Color.Gold;

    [DataField]
    public SoundSpecifier? Sound;
}
