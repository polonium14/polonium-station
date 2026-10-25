using Content.Shared._Polonium.Modules.ModSuit;

namespace Content.Client._Polonium.Modules.ModSuit;

public sealed partial class ModSuitSystem : SharedModSuitSystem
{
    public override void Initialize()
    {
        base.Initialize();

        InitializeDeployable();
        InitializeSealable();
    }
}
