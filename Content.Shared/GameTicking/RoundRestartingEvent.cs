// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared.GameTicking;

/// <summary>
/// Event notifying about the restart of the round
/// </summary>
[ByRefEvent]
public readonly record struct RoundRestartingEvent();
