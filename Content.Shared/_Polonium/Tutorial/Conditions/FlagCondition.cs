namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>A step flag is set, by a watcher action or by the tracker itself.</summary>
public sealed partial class FlagCondition : TutorialCondition
{
    [DataField(required: true)]
    public string Flag = string.Empty;
}
