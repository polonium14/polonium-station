namespace Content.Shared._Polonium.Projectiles;

/// <summary>
/// Entities a launched projectile or thrown item already overlapped at launch and should pass through,
/// because they stand behind or beside the line of fire.
/// </summary>
[RegisterComponent, Access(typeof(LaunchOverlapSystem))]
public sealed partial class LaunchOverlapComponent : Component
{
    public HashSet<EntityUid> Ignored = new();

    public EntityUid? Shooter;

    public TimeSpan EndTime;
}
