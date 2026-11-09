using Content.Shared._Polonium.Breathalyzer;

namespace Content.Client._Polonium.Breathalyzer;

public sealed partial class BreathalyzerUiSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BreathalyzerComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(Entity<BreathalyzerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_ui.TryGetOpenUi(ent.Owner, BreathalyzerUiKey.Key, out var bui))
            bui.Update();
    }
}
