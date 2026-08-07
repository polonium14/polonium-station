// SPDX-FileCopyrightText: 2025 beck-thompson <107373427+beck-thompson@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._DV.CustomObjectiveSummary;

/// <summary>
///     Put on a players mind if the wrote a custom summary for their objectives.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CustomObjectiveSummaryComponent : Component
{
    /// <summary>
    ///     Hard cap on the length of a submitted summary. Enforced server-side; the client uses it for its
    ///     character counter / submit-button gating. A malicious client cannot exceed it: the server clamps.
    /// </summary>
    public const int MaxSummaryLength = 1024;

    /// <summary>
    ///     What the player wrote as their summary!
    /// </summary>
    [DataField, AutoNetworkedField]
    public string ObjectiveSummary = "";
}
