using Content.Shared._Polonium.Tutorial.Watchers;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Prototypes;

/// <summary>
/// Watchers a whole stretch of steps shares. A net that has to stay armed no matter which step is
/// current - a trainee building three machines at once can run out of steel or put the wrong board
/// in at any point - belongs here instead of being copied onto every step that could be the one.
/// </summary>
[Prototype]
public sealed partial class TutorialWatcherSetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<TutorialWatcher> Watchers = new();
}
