using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._Polonium.Weapons.Ranged.Components;

[RegisterComponent]
[Access(typeof(SharedGunSystem))]
public sealed partial class AmmoFillDelayModifierComponent : Component
{
    [DataField]
    public float Multiplier = 1f;
}
