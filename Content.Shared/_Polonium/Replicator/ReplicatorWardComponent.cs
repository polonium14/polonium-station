using Content.Shared.DoAfter;
using Content.Shared.Lathe.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Replicator;

/// <summary>
/// Worn gear that keeps replicators from teleporting the wearer and blunts what they deal.
/// </summary>
[RegisterComponent]
public sealed partial class ReplicatorWardComponent : Component
{
    [DataField]
    public float DamageCoefficient = 0.2f;

    [DataField]
    public float StaminaCoefficient = 0.3f;
}

[RegisterComponent]
public sealed partial class ReplicatorColossusCoreComponent : Component
{
    [DataField]
    public TimeSpan StudyTime = TimeSpan.FromSeconds(8);

    [DataField(required: true)]
    public ProtoId<LatheRecipePackPrototype> Unlocks;

    [DataField]
    public LocId Announcement = "replicator-colossus-core-announcement";

    [DataField]
    public SoundSpecifier? StudySound;

    [DataField]
    public bool Studied;
}

[RegisterComponent]
public sealed partial class ReplicatorColossusWreckComponent : Component;

[Serializable, NetSerializable]
public sealed partial class ReplicatorColossusCoreStudyDoAfterEvent : SimpleDoAfterEvent;
