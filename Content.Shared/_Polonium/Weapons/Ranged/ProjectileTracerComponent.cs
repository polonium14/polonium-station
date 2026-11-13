namespace Content.Shared._Polonium.Weapons.Ranged;

/// <summary>
/// Draws a glowing tracer streak behind a flying projectile on the client.
/// </summary>
[RegisterComponent]
public sealed partial class ProjectileTracerComponent : Component
{
    /// <summary>
    /// How the tracer looks.
    /// </summary>
    [DataField]
    public TracerStyle Style = TracerStyle.Bullet;

    /// <summary>
    /// Color of the streak, matching the projectile sprite. Alpha sets its opacity.
    /// </summary>
    [DataField]
    public Color Color = Color.FromHex("#FFBB32");

    /// <summary>
    /// Longest streak behind the projectile, in tiles.
    /// </summary>
    [DataField]
    public float Length = 2f;

    /// <summary>
    /// Width of the line in tiles, set by the caliber. Electric arcs also spread in proportion to it.
    /// </summary>
    [DataField]
    public float Width = 0.05f;

    /// <summary>
    /// How long a point of the streak stays visible, so the streak fades out over this time after a hit.
    /// </summary>
    [DataField]
    public TimeSpan FadeTime = TimeSpan.FromSeconds(0.12);
}

public enum TracerStyle : byte
{
    /// <summary>
    /// A thin line that fades out evenly from the projectile to its tail
    /// </summary>
    Bullet,

    /// <summary>
    /// A thin line that keeps its full color behind the projectile and only fades out at its tail
    /// </summary>
    Laser,

    /// <summary>
    /// Thin zigzag arcs behind the projectile that crackle into a new shape several times a second
    /// </summary>
    Electric,
}
