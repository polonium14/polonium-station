// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Polonium.StationEvents;

/// <summary>
/// Marker for the BreakerDown station event (disables all APCs on a station).
/// </summary>
[RegisterComponent]
public sealed partial class BreakerDownRuleComponent : Component;
