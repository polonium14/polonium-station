// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <ozzeusz@gmail.com>
// SPDX-FileCopyrightText: 2026 github-actions[bot] <41898282+github-actions[bot]@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

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
    /// If true, this phone is on the public station directory (red, blue, banana).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ListedInDirectory = true;

    /// <summary>
    /// If true, may only dial listed public lines, not the private CentComm line (blue, banana).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ExcludeCentCommFromDial;

    /// <summary>
    /// If true, may dial and see the private CentComm line (red phone).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IncludeCentCommInDirectory;

    /// <summary>
    /// If true, the handset directory lists only CentComm lines (station red phone).
    /// Requires <see cref="IncludeCentCommInDirectory"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool OnlyCentCommInDirectory;

    /// <summary>
    /// If true, this line is private but may dial any public listed line (blood-red phone).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PrivateLine;

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
    /// Maximum distance in tiles the phone holder may move from the phone base before it gets back
    /// </summary>
    [DataField]
    public float HandsetCordRange = 2.5f;

    /// <summary>
    /// Server-side display name override for admin phone impersonation during a call.
    /// </summary>
    [ViewVariables]
    public string? AdminImpersonationName;

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
    /// Looped while the handset is off-hook and the line is idle.
    /// </summary>
    [DataField]
    public SoundSpecifier? DialTone;

    /// <summary>
    /// Server-side looping dial-tone audio stream.
    /// </summary>
    [ViewVariables]
    public EntityUid? DialToneStream;

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

    [DataField]
    public SoundSpecifier? InCallStaticSound;

    /// <summary>
    /// Server side looping in-call static audio stream for the handset holder
    /// </summary>
    [ViewVariables]
    public EntityUid? InCallStaticStream;
}
