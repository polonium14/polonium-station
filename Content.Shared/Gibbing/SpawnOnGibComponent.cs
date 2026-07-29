using Content.Shared.Storage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Gibbing;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SpawnOnGibSystem))]
public sealed partial class SpawnOnGibComponent : Component
{
    [DataField(required: true)]
    public List<EntitySpawnEntry> Spawn = new();
}
