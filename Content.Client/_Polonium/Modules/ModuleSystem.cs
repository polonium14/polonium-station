using Content.Shared._Polonium.Modules;

namespace Content.Client._Polonium.Modules;

public sealed partial class ModuleSystem : SharedModuleSystem
{
    public override void Initialize()
    {
        base.Initialize();

        InitializeRelay();
    }
}
