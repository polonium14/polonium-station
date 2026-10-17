using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>Trainee has this prototype in one of their hands. Good for "go and pick up X" steps.</summary>
public sealed partial class HoldingPrototypeCondition : TutorialCondition
{
    [DataField(required: true)]
    public EntProtoId Prototype;
}
