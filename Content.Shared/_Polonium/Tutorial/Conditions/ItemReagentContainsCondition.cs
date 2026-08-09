// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>Done when entity with given anchor has at least MinUnits of given reagent in any solution.</summary>
public sealed partial class ItemReagentContainsCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    /// <summary>Reagent prototype id, e.g. "Water".</summary>
    [DataField(required: true)]
    public string Reagent = string.Empty;

    [DataField]
    public float MinUnits = 1f;
}
