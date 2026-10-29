using Content.Shared._Polonium.Modules;

namespace Content.Server._Polonium.Modules;

public sealed partial class ModuleSystem : SharedModuleSystem
{
    public override void Initialize()
    {
        base.Initialize();

        InitializePower();
    }
}
