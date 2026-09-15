using Content.Shared.Damage;

namespace Content.Server._Polonium.Spawners;

/// <summary>
/// Whatever this spawner puts out comes into the world already hurt, as if it had been lying there
/// for a while. Works on conditional and random spawners.
/// </summary>
[RegisterComponent]
public sealed partial class SpawnerDamageComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();
}

/// <summary>
/// Raised on a spawner right after it spawned something.
/// </summary>
[ByRefEvent]
public readonly record struct SpawnerSpawnedEvent(EntityUid Spawned);
