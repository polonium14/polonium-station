namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// Everything on the current step's lathe guide is gathered: carried, lying by the lathe, or already in the
/// frame at one of its places. Reads the list from the step, so it is never written down twice.
/// </summary>
public sealed partial class LatheGuideGatheredCondition : TutorialCondition
{
}
