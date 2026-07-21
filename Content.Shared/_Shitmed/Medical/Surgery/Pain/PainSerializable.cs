using Content.Shared._Shitmed.Medical.Surgery.Pain.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Shitmed.Medical.Surgery.Pain;

[Serializable, NetSerializable]
public enum PainDamageTypes
{
    WoundPain,
    TraumaticPain,
}

[Serializable, NetSerializable]
public enum PainThresholdTypes
{
    None,
    PainFlinch,
    Agony,
    PainShock,
    PainShockAndAgony,
}

// Not [DataRecord]: never used as a [DataField], only as plain runtime Dictionary
// values (server-only, unnetworked). Tagging these as DataRecord makes the engine's
// SerializationManager eagerly build a reflection-emit instantiator for them at boot
// regardless of usage, which throws InvalidProgramException for this field shape
// (FixedPoint2 + TimeSpan? + enum-default combo) on this engine version.
public partial record struct PainMultiplier(FixedPoint2 Change, string Identifier = "Unspecified", PainDamageTypes PainDamageType = PainDamageTypes.WoundPain, TimeSpan? Time = null);

public partial record struct PainFeelingModifier(FixedPoint2 Change, TimeSpan? Time = null);

public partial record struct PainModifier(FixedPoint2 Change, string Identifier = "Unspecified", PainDamageTypes PainDamageType = PainDamageTypes.WoundPain, TimeSpan? Time = null); // Easier to manage pain with modifiers.

[ByRefEvent]
public partial record struct PainThresholdTriggered(Entity<NerveSystemComponent> NerveSystem, PainThresholdTypes ThresholdType, FixedPoint2 PainInput, bool Cancelled = false);

[ByRefEvent]
public partial record struct PainThresholdEffected(Entity<NerveSystemComponent> NerveSystem, PainThresholdTypes ThresholdType, FixedPoint2 PainInput);

[ByRefEvent]
public record struct PainFeelsChangedEvent(EntityUid NerveSystem, EntityUid NerveEntity, FixedPoint2 CurrentPainFeels);
[ByRefEvent]
public record struct PainModifierAddedEvent(EntityUid NerveSystem, EntityUid NerveUid, FixedPoint2 AddedPain);

[ByRefEvent]
public record struct PainModifierRemovedEvent(EntityUid NerveSystem, EntityUid NerveUid, FixedPoint2 CurrentPain);

[ByRefEvent]
public record struct PainModifierChangedEvent(EntityUid NerveSystem, EntityUid NerveUid, FixedPoint2 CurrentPain);
