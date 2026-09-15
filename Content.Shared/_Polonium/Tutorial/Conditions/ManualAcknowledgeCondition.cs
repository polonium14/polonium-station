namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// Done when the player clicks the "Got it" button on the bubble.
/// Use for free-form steps where we can't reliably detect completion in code
/// (e.g. "throw the bag — works on any disposals").
/// </summary>
public sealed partial class ManualAcknowledgeCondition : TutorialCondition
{
}
