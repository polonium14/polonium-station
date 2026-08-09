// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Tutorial.Components;

/// <summary>
/// Stick this on a door/item/whatever in the map editor to make it targetable by tutorial steps.
/// Networked so the client can resolve anchor ids itself (pathfinding picks the closest match).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialAnchorComponent : Component
{
    /// <summary>Has to match what's in the flow YAML (e.g. "first_door").</summary>
    [DataField(required: true), AutoNetworkedField]
    public string AnchorId = string.Empty;
}
