// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Harpy;

[RegisterComponent, NetworkedComponent]
public sealed partial class HarpySingerComponent : Component
{
    [DataField(serverOnly: true)]
    public EntProtoId? MidiActionId = "ActionHarpyPlayMidi";

    /// <summary>
    /// Server only, as it uses a server-BUI event.
    /// </summary>
    [DataField(serverOnly: true)]
    public EntityUid? MidiAction;
}
