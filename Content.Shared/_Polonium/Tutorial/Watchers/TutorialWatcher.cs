using Content.Shared._Polonium.Tutorial.Actions;

namespace Content.Shared._Polonium.Tutorial.Watchers;

[ImplicitDataDefinitionForInheritors]
public abstract partial class TutorialWatcher
{
    [DataField]
    public List<LocId> Quip = new();

    [DataField]
    public List<TutorialAction> Actions = new();

    [DataField]
    public bool Once = true;

    /// <summary>
    /// With Once, arm again as soon as the watcher stops matching, so the same mistake made twice
    /// gets explained twice instead of once and then never.
    /// </summary>
    [DataField]
    public bool Rearm;
}
