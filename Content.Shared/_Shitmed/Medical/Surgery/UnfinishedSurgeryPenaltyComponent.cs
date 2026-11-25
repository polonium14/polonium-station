// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery;

/// <summary>
/// Tracks how much of a body's bleeding comes from surgical incisions left open,
/// so it does not clot and stops when the incisions are closed.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class UnfinishedSurgeryPenaltyComponent : Component
{
    [DataField, AutoNetworkedField]
    public float PenaltyBleed;
}
