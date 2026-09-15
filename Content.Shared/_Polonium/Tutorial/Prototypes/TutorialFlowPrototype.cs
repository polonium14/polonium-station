using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Prototypes;

/// <summary>A sequence of steps. Tied 1:1 to a SolitarySpawning prototype (usually).</summary>
[Prototype]
public sealed partial class TutorialFlowPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<ProtoId<TutorialStepPrototype>> Steps = new();
}
