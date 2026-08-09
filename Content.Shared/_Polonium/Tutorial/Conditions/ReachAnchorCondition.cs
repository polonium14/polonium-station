// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>Player gets close enough to the anchor. Polled every 250ms.</summary>
public sealed partial class ReachAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    /// <summary>Metres. Default is arm's length-ish.</summary>
    [DataField]
    public float Range = 1.5f;
}
