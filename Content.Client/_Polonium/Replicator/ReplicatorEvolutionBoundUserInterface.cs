using Content.Shared._Polonium.Replicator;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Replicator;

[UsedImplicitly]
public sealed partial class ReplicatorEvolutionBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ReplicatorEvolutionWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ReplicatorEvolutionWindow>();
        _window.OnGrow += module => SendMessage(new ReplicatorEvolutionGrowMessage(module));
        _window.OnSpecialize += id => SendMessage(new ReplicatorEvolutionSpecializeMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ReplicatorEvolutionState evolution)
            _window?.SetHive(evolution.Budget, evolution.NestLevel);
    }
}
