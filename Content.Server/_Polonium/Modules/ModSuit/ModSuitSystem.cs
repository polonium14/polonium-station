using Content.Shared._Polonium.Modules.ModSuit;
using Content.Shared._Polonium.Modules.ModSuit.Components;

namespace Content.Server._Polonium.Modules.ModSuit;

public sealed partial class ModSuitSystem : SharedModSuitSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModSuitPartDeployableComponent, MapInitEvent>(OnDeployableMapInit);
    }
}
