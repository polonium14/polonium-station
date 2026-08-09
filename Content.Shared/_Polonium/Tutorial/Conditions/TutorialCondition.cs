// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// Data-only base. Actual checking happens in TutorialConditionTracker —
/// subclass this and add a handler there.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class TutorialCondition
{
}
