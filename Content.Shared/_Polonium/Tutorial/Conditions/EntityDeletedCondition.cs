namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// Done as soon as any entity with this AnchorId gets destroyed.
/// Used for "throw the bag into disposals" — entity disappears down the pipe.
/// </summary>
public sealed partial class EntityDeletedCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}
