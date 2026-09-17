using System.Numerics;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class TutorialSessionComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<TutorialFlowPrototype> Flow;

    [DataField, AutoNetworkedField]
    public int CurrentStepIndex = -1;

    [DataField, AutoNetworkedField]
    public ProtoId<TutorialStepPrototype>? CurrentStep;

    [DataField, AutoNetworkedField]
    public string? NavigationAnchor;

    [ViewVariables]
    public Dictionary<string, EntityUid> Anchors = new();

    [ViewVariables]
    public TimeSpan StepStartedAt;

    /// <summary>Wall clock when this run of the map tutorial started, for the db duration.</summary>
    [ViewVariables]
    public DateTime FlowStartedAt;

    // condition already true, waiting out the settle delay
    [ViewVariables]
    public TimeSpan? PendingAdvanceAt;

    [ViewVariables]
    public bool StuckHinted;

    /// <summary>The last step ended on a timeout rather than on the trainee finishing it.</summary>
    [ViewVariables]
    public bool AutoSkipped;

    /// <summary>Where they were standing when that happened.</summary>
    [ViewVariables]
    public Vector2? SkippedAt;

    [DataField, AutoNetworkedField]
    public List<string> HighlightAnchors = new();

    [DataField, AutoNetworkedField]
    public LocId? KeybindHint;

    [ViewVariables]
    public HashSet<string> Flags = new();

    [ViewVariables]
    public HashSet<int> FiredWatchers = new();

    /// <summary>When each HeldCondition of the current step last turned true. Keyed by the prototype instance.</summary>
    [ViewVariables]
    public Dictionary<TutorialCondition, TimeSpan> HeldSince = new(ReferenceEqualityComparer.Instance);

    /// <summary>Set by an eject, the tracker jumps here once it is done with the current pass.</summary>
    [ViewVariables]
    public ProtoId<TutorialStepPrototype>? JumpTo;

    [ViewVariables]
    public EntityUid? MentorUid;
}
