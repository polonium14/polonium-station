namespace Content.Server._Polonium.Spawners;

/// <summary>
/// Damage on this mob never reaches its limbs, so there are no wounds to heal by themselves.
/// </summary>
[RegisterComponent]
public sealed partial class WholeBodyDamageComponent : Component;
