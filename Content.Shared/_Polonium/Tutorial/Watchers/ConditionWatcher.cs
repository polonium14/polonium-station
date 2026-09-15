using Content.Shared._Polonium.Tutorial.Conditions;

namespace Content.Shared._Polonium.Tutorial.Watchers;

/// <summary>
/// Fires when an ordinary completion condition comes true, so every check the flow already has
/// can double as a watcher without a bespoke watcher type for each one.
/// </summary>
public sealed partial class ConditionWatcher : TutorialWatcher
{
    [DataField(required: true)]
    public TutorialCondition Condition = default!;
}
