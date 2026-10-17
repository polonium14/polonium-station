namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// Data-only base. Actual checking happens in TutorialConditionTracker —
/// subclass this and add a handler there.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class TutorialCondition
{
}
