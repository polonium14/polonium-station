// SPDX-FileCopyrightText: 2025 Steve <marlumpy@gmail.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <maciej.walendziuk@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.RPD;

/// <summary>
/// Whitelists an entity for RPD deconstruction. The RPD refuses to deconstruct anything else, so
/// it can only ever take apart piping and atmos devices. Cost, delay and effect still come from
/// RCDDeconstructableComponent, which this is expected to accompany.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(RPDSystem))]
public sealed partial class RPDDeconstructableComponent : Component;
