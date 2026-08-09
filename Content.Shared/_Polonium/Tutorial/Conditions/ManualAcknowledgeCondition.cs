// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// Done when the player clicks the "Got it" button on the bubble.
/// Use for free-form steps where we can't reliably detect completion in code
/// (e.g. "throw the bag — works on any disposals").
/// </summary>
public sealed partial class ManualAcknowledgeCondition : TutorialCondition
{
}
