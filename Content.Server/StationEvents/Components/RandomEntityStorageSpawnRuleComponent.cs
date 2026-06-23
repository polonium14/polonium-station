using Content.Server.StationEvents.Events;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.StationEvents.Components;

/// <summary>
/// Spawns a single entity in a random EntityStorage on the station
/// </summary>
[RegisterComponent, Access(typeof(RandomEntityStorageSpawnRule))]
public sealed partial class RandomEntityStorageSpawnRuleComponent : Component
{
    /// <summary>
    /// The entity to be spawned.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Prototype;

    /// <summary>
    /// Sound to play the first time the storage is opened.
    /// </summary>
    [DataField("openStorageSound")]
    public SoundSpecifier? FirstOpenStorageSound;
}
