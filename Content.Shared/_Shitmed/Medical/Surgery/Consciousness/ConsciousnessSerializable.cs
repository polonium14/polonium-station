using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Shitmed.Medical.Surgery.Consciousness;

[Serializable]
public enum ConsciousnessModType
{
    Generic, // Same for generic
    Pain, // Pain is affected only by pain multipliers
}

[ByRefEvent]
public record struct ConsciousnessUpdatedEvent(bool IsConscious);

// Not [DataRecord]: never used as a [DataField], only as plain runtime Dictionary
// values (server-only, unnetworked). See PainSerializable.cs for why DataRecord is
// deliberately omitted here.
public partial record struct ConsciousnessModifier(FixedPoint2 Change, TimeSpan? Time, ConsciousnessModType Type = ConsciousnessModType.Generic);

public partial record struct ConsciousnessMultiplier(FixedPoint2 Change, TimeSpan? Time, ConsciousnessModType Type = ConsciousnessModType.Generic);
