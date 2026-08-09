// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>Bolts or unbolts a door by its anchor id.</summary>
public sealed partial class BoltDoorAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField]
    public bool Bolt = true;

    /// <summary>Run after this many seconds — gives Close a moment to actually shut the door.</summary>
    [DataField]
    public float Delay = 0f;
}
