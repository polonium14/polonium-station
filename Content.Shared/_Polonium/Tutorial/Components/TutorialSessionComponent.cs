// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Polonium.Tutorial.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Tutorial.Components;

/// <summary>
/// Lives on the player mob while a tutorial is running.
/// Server owns everything; client just reads the networked fields for UI.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class TutorialSessionComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<TutorialFlowPrototype> Flow;

    /// <summary>Index into the flow. -1 before the first step.</summary>
    [DataField, AutoNetworkedField]
    public int CurrentStepIndex = -1;

    [DataField, AutoNetworkedField]
    public ProtoId<TutorialStepPrototype>? CurrentStep;

    /// <summary>Anchor id for pathfinding</summary>
    [DataField, AutoNetworkedField]
    public string? NavigationAnchor;

    /// <summary>Resolved entity for debugging</summary>
    [DataField, AutoNetworkedField]
    public NetEntity? NavigationTarget;

    /// <summary>Server-only. Cached at flow start so we don't query every tick.</summary>
    [ViewVariables]
    public Dictionary<string, EntityUid> Anchors = new();

    [ViewVariables]
    public TimeSpan StepStartedAt;
}

/// <summary>Placeholder for potential one-off notifications later.</summary>
[Serializable, NetSerializable]
public sealed class TutorialStepPresentationState
{
    public string? StepId;
    public string? InstructionLocId;
    public NetEntity? NavigationTarget;
}
