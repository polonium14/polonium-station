using Content.Shared._Shitmed.Medical.Surgery.Pain.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Shitmed.Medical.Surgery.Consciousness.Systems;

public sealed partial class ConsciousnessSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    [Dependency] private PainSystem _pain = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitProcess();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        UpdatePassedOut(frameTime);
    }
}
