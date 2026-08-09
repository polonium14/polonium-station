// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Polonium.Tutorial.Actions;
using Content.Shared._Polonium.Tutorial.Conditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Prototypes;

/// <summary>
/// A single step in a flow - what to show, where to point, when to advance.
/// </summary>
[Prototype("tutorialStep")]
public sealed partial class TutorialStepPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Loc key for the bubble text.</summary>
    [DataField(required: true)]
    public LocId Instruction = string.Empty;

    /// <summary>Which anchor to draw a path to. Null = no path line.</summary>
    [DataField]
    public string? NavigationAnchor;

    /// <summary>What the player needs to do. Null = only advances via ForceAdvance.</summary>
    [DataField]
    public TutorialCondition? Completion;

    [DataField]
    public List<TutorialAction> OnEnter = new();

    [DataField]
    public List<TutorialAction> OnComplete = new();
}
