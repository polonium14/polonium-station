using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Components;

[RegisterComponent]
public sealed partial class TutorialPatientComponent : Component
{
    [DataField]
    public bool SpawnedDead;

    [DataField]
    public ProtoId<DamageTypePrototype>? DamageType;

    [DataField]
    public float HealBelow = 8f;
}
