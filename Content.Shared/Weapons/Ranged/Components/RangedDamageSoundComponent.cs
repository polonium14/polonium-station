using Content.Shared.Damage.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
/// Plays the specified sound upon receiving damage of that type.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RangedDamageSoundComponent : Component
{
    [DataField(customTypeSerializer: typeof(PrototypeIdDictionarySerializer<SoundSpecifier, DamageGroupPrototype>))]
    public Dictionary<string, SoundSpecifier>? SoundGroups;

    [DataField(customTypeSerializer: typeof(PrototypeIdDictionarySerializer<SoundSpecifier, DamageTypePrototype>))]
    public Dictionary<string, SoundSpecifier>? SoundTypes;
}
