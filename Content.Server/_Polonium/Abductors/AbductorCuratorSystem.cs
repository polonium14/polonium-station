using Content.Shared._Polonium.Abductors;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Polonium.Abductors;

public sealed partial class AbductorCuratorSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;

    /// <summary>
    /// Opens the Curator's window for the listeners, all of them hearing the same variant of the line.
    /// </summary>
    public void Say(ProtoId<AbductorCuratorLinePrototype> line, Filter listeners)
    {
        if (!_prototype.Resolve(line, out var prototype) || prototype.Variants.Count == 0)
            return;

        RaiseNetworkEvent(new AbductorCuratorSpeechEvent(_random.Pick(prototype.Variants)), listeners);
    }
}
