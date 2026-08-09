// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>Force-closes a door by its anchor id (e.g. trap the player after they enter).</summary>
public sealed partial class CloseDoorAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}
