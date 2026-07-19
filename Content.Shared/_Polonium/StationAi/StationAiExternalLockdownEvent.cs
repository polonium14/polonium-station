// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;

namespace Content.Shared._Polonium.StationAi;

/// <summary>
/// Instant action that bolts all external airlocks on the performer's station.
/// </summary>
public sealed partial class StationAiExternalLockdownEvent : InstantActionEvent;
