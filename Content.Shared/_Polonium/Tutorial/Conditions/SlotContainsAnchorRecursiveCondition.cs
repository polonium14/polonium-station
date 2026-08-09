// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// Done when the player's inventory slot (and any container inside it) contains all listed anchors.
/// Useful for "put card into PDA and PDA into ID slot".
/// </summary>
public sealed partial class SlotContainsAnchorRecursiveCondition : TutorialCondition
{
    /// <summary>Inventory slot to start searching from, e.g. "id".</summary>
    [DataField(required: true)]
    public string Slot = string.Empty;

    /// <summary>All these anchors must be found somewhere inside (recursively).</summary>
    [DataField(required: true)]
    public List<string> AnchorIds = new();
}
