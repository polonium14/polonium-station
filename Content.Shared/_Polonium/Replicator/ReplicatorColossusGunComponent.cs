using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Replicator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReplicatorColossusGunComponent : Component
{
    /// <summary>
    /// Where the weapon's muzzles are drawn for each direction the colossus faces, in tiles from its origin as seen on screen.
    /// Shots leave them in turn.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<Direction, List<Vector2>> MuzzleOffsets = new();

    [DataField, AutoNetworkedField]
    public int Shots;

    /// <summary>
    /// Whether shots come out of the holder's <see cref="ReplicatorColossusAmmoComponent"/> instead of the weapon's own ammo.
    /// </summary>
    [DataField]
    public bool SharedAmmo;
}

[RegisterComponent]
public sealed partial class ReplicatorColossusProjectileComponent : Component;
