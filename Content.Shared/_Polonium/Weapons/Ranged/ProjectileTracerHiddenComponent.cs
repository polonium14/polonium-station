using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Weapons.Ranged;

/// <summary>
/// Stops clients from drawing the <see cref="ProjectileTracerComponent"/> streak of this projectile.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ProjectileTracerHiddenComponent : Component;
