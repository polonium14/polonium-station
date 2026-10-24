// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Polonium.Body;

/// <summary>
/// Marks an organ as a prosthesis. It has no behaviour of its own; things like patient requests check for it.
/// </summary>
[RegisterComponent]
public sealed partial class ProstheticComponent : Component;
