using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Modules.ModSuit;

public abstract partial class SharedModSuitSystem : EntitySystem
{
    [Dependency] protected SharedAppearanceSystem Appearance = default!;
    [Dependency] private SharedModuleSystem _module = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        InitializeComplexity();
        InitializeCore();
        InitializeDeployable();
        InitializePower();
        InitializeSealable();
        InitializeSealVerb();
        InitializeUI();
    }

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateSealable(frameTime);
    }
}
