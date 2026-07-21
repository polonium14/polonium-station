using Content.Shared._Shitmed.Medical.Surgery.Pain.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery.Consciousness.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConsciousnessComponent : Component
{
    /// <summary>
    /// Represents the limit at which point the entity falls unconscious.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 Threshold = 95;

    /// <summary>
    /// Represents the base consciousness value before applying any modifiers.
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 RawConsciousness = -1;

    /// <summary>
    /// Gets the consciousness value after applying the multiplier and clamping between 0 and Cap.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 Consciousness => FixedPoint2.Clamp(RawConsciousness * Multiplier, 0, Cap);

    /// <summary>
    /// Represents the multiplier to be applied on the RawConsciousness.
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 Multiplier = 1.0;

    /// <summary>
    /// Represents the maximum possible consciousness value. Also used as the default RawConsciousness value if it is set to -1.
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 Cap = 190;

    /// <summary>
    /// Represents the collection of additional effects that modify the base consciousness level.
    /// Server-only bookkeeping, not networked (see class doc comment).
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<(EntityUid, string), ConsciousnessModifier> Modifiers = new();

    /// <summary>
    /// Represents the collection of coefficients that further modulate the consciousness level.
    /// Server-only bookkeeping, not networked (see class doc comment).
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<(EntityUid, string), ConsciousnessMultiplier> Multipliers = new();

    /// <summary>
    /// Defines which parts of the consciousness state are necessary for the entity.
    /// Server-only bookkeeping, not networked (see class doc comment).
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<string, (EntityUid?, bool, bool)> RequiredConsciousnessParts = new();

    /// <summary>
    /// Not networked: both client and server independently discover their own NerveSystem
    /// via the same organ-insert event flow, so there's nothing to synchronize.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Entity<NerveSystemComponent> NerveSystem = default;

    [DataField]
    public TimeSpan ConsciousnessUpdateTime = TimeSpan.FromSeconds(0.8f);

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextConsciousnessUpdate;

    // Forceful control attributes, it's recommended not to use them directly.
    [ViewVariables(VVAccess.ReadWrite)]
    public bool PassedOut = false;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan PassedOutTime = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan ForceConsciousnessTime = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public bool ForceDead;

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public bool ForceUnconscious;

    // funny
    [ViewVariables(VVAccess.ReadOnly)]
    public bool ForceConscious;

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public bool IsConscious = true;
    // Forceful control attributes, it's recommended not to use them directly.

    [DataField]
    public bool HasPainScreams;
}
