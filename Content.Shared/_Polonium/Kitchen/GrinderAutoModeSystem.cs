using Content.Shared.Kitchen.Components;
using Content.Shared.Kitchen.EntitySystems;

namespace Content.Shared._Polonium.Kitchen;

/// <summary>
/// Lets the window of a reagent grinder ask for one automatic mode by name. The game itself only steps to the next mode.
/// </summary>
public sealed class GrinderAutoModeSystem : EntitySystem
{
    [Dependency] private SharedReagentGrinderSystem _grinder = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReagentGrinderComponent, GrinderSetAutoModeMessage>(OnSetAutoMode);
    }

    private void OnSetAutoMode(Entity<ReagentGrinderComponent> ent, ref GrinderSetAutoModeMessage args)
    {
        _grinder.SetAutoMode(ent.AsNullable(), args.Mode);
    }
}
