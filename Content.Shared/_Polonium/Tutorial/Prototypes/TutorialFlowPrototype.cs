// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Prototypes;

/// <summary>A sequence of steps. Tied 1:1 to a SolitarySpawning prototype (usually).</summary>
[Prototype("tutorialFlow")]
public sealed partial class TutorialFlowPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<ProtoId<TutorialStepPrototype>> Steps = new();
}
