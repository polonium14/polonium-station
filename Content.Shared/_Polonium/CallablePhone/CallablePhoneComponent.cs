// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.CallablePhone;

/// <summary>
/// Marks an instrument phone as participating in the handset calling system.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CallablePhoneComponent : Component
{
    public const string HandsetSlotId = "handset";

    /// <summary>
    /// If true, this phone appears in the dial directory (red phones only).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ListedInDirectory = true;

    /// <summary>
    /// If true, calling this phone opens an admin chat window (CentComm line).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public bool IsCentComm;

    /// <summary>
    /// Prefix for admin announcements and admin IC name during CentComm calls.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string AdminChatPrefix = "prayer-chat-notify-centcom";

    /// <summary>
    /// Display name in the phone directory. Set in prototype YAML or via View Variables.
    /// If a locale id, it is resolved on map init (same as <see cref="Labels.Components.LabelComponent"/>).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public string? PhoneName;

    /// <summary>
    /// Entity currently holding this phone's handset off-hook.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? HandsetHolder;

    /// <summary>
    /// Played when the handset is picked up while the line is idle or ringing.
    /// </summary>
    [DataField]
    public SoundSpecifier? PickupHandsetSound;

    /// <summary>
    /// Played when the handset is picked up during an active call (random variant).
    /// </summary>
    [DataField]
    public SoundSpecifier? PickupHandsetInCallSound;

    /// <summary>
    /// Played when the handset is returned to the cradle while idle or ringing.
    /// </summary>
    [DataField]
    public SoundSpecifier? HangupHandsetSound;

    /// <summary>
    /// Played when the handset is returned during or just after an active call (random variant).
    /// </summary>
    [DataField]
    public SoundSpecifier? HangupHandsetInCallSound;

    /// <summary>
    /// Played once when an outbound call is placed successfully.
    /// </summary>
    [DataField]
    public SoundSpecifier? DialSound;

    /// <summary>
    /// Looped on the caller while <see cref="Telephone.TelephoneState.Calling"/>.
    /// </summary>
    [DataField]
    public SoundSpecifier? CallWaitingTone;

    /// <summary>
    /// Server-side looping call-waiting audio stream.
    /// </summary>
    [ViewVariables]
    public EntityUid? CallWaitingStream;

    /// <summary>
    /// Bumped to cancel a pending post-dial call-waiting start.
    /// </summary>
    [ViewVariables]
    public int CallWaitingDelayGeneration;

    /// <summary>
    /// Played when the dialed line is busy.
    /// </summary>
    [DataField]
    public SoundSpecifier? BusyTone;

    /// <summary>
    /// Server-side looping busy-tone audio stream on the caller's phone.
    /// </summary>
    [ViewVariables]
    public EntityUid? BusyToneStream;
}
