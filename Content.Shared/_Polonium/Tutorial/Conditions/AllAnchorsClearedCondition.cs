// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>Done when no entities with this AnchorId are left on the grid. Polled.</summary>
public sealed partial class AllAnchorsClearedCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}
