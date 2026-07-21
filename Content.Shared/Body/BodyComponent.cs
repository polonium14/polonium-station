using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

/// <summary>
/// Component on the entity that "has" a body, and that oversees entities with the <see cref="OrganComponent"/> inside it.
/// </summary>
/// <seealso cref="BodySystem" />
/// <seealso cref="SharedVisualBodySystem" />
[RegisterComponent, NetworkedComponent]
[Access(typeof(BodySystem))]
public sealed partial class BodyComponent : Component
{
    public const string ContainerID = "body_organs";

    /// <summary>
    /// The actual container with entities with <see cref="OrganComponent" /> in it
    /// </summary>
    [ViewVariables]
    public Container? Organs;

    /// <summary>
    /// Whether this body is visible through thermal vision overlays.
    /// </summary>
    [DataField]
    public bool ThermalVisibility = true;

    [ViewVariables, Access(Other = AccessPermissions.ReadWrite)]
    public TimeSpan HealAt;

    /// <summary>
    /// Every vital organ category this body has ever had inserted into it. Grows monotonically
    /// (never shrinks on removal) so it doubles as a manifest of what the body is supposed to
    /// have - diffing this against currently-present organs is how HealthAnalyzerSystem reports
    /// a missing heart/lungs/etc. Limbs are deliberately excluded (see BodySystem.OnBodyEntInserted);
    /// dismemberment already has its own visible state and shouldn't be double-reported here.
    /// </summary>
    [ViewVariables, Access(typeof(BodySystem), Other = AccessPermissions.Read)]
    public HashSet<ProtoId<OrganCategoryPrototype>> ExpectedOrgans = new();
}

/// <summary>
/// Raised on organ entity, when it is inserted into a body
/// </summary>
[ByRefEvent]
public readonly record struct OrganGotInsertedEvent(EntityUid Target);

/// <summary>
/// Raised on organ entity, when it is removed from a body
/// </summary>
[ByRefEvent]
public readonly record struct OrganGotRemovedEvent(EntityUid Target);

/// <summary>
/// Raised on body entity, when an organ is inserted into it
/// </summary>
[ByRefEvent]
public readonly record struct OrganInsertedIntoEvent(EntityUid Organ);

/// <summary>
/// Raised on body entity, when an organ is removed from it
/// </summary>
[ByRefEvent]
public readonly record struct OrganRemovedFromEvent(EntityUid Organ);
