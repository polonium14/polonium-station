using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Shitmed.Medical.Surgery.Wounds;

/// <summary>
/// How severe a woundable (limb-organ)'s overall damage is. Driven by a per-prototype
/// Thresholds dictionary against WoundableComponent.WoundableIntegrity. Mangled is reached
/// via thresholds; Severed is set only programmatically by WoundSystem.DestroyWoundable /
/// AmputateWoundable, never by crossing a threshold directly.
/// </summary>
public enum WoundableSeverity : byte
{
    Healthy,
    Minor,
    Moderate,
    Severe,
    Critical,
    Mangled,
    Severed,
}

/// <summary>
/// How severe an individual wound is, driven by WoundComponent.WoundSeverityPoint against
/// the static WoundSystem.WoundThresholds table (scaled by the woundable's IntegrityCap).
/// Independent scale from WoundableSeverity, which describes the limb as a whole.
/// </summary>
public enum WoundSeverity
{
    Healed,
    Minor,
    Moderate,
    Severe,
    Critical,
    Loss,
}

public enum WoundType
{
    External,
    Internal,
}

public enum WoundVisibility
{
    Always,
    HandScanner,
    AdvancedScanner,
}

public enum BleedingSeverity
{
    Minor,
    Severe,
}

/// <summary>
/// Appearance-data key for a woundable's current wound list, read client-side by
/// WoundableVisualsSystem to render damage/bleed sprite overlays.
/// </summary>
[Serializable, NetSerializable]
public enum WoundableVisualizerKeys
{
    Wounds,
}

/// <summary>
/// Net entities of the wounds currently on a woundable, pushed via SharedAppearanceSystem.SetData
/// so the client can compute overlay state without needing direct wound-container access.
/// </summary>
[Serializable, NetSerializable]
public sealed class WoundVisualizerGroupData : ICloneable
{
    public List<NetEntity> GroupList;

    public WoundVisualizerGroupData(List<NetEntity> groupList)
    {
        GroupList = groupList;
    }

    public object Clone()
    {
        return new WoundVisualizerGroupData(new List<NetEntity>(GroupList));
    }
}

/// <summary>
/// Raised on a wound entity (and, since AutoNetworkedField events aren't a thing, consumed
/// via direct subscription) whenever its WoundSeverityPoint changes. Lets other subsystems
/// (Pain, Traumas) react to a specific wound worsening/healing without polling.
/// Overflow is currently always null; callers fall back to the computed
/// NewSeverity-OldSeverity delta.
/// </summary>
[ByRefEvent]
public readonly record struct WoundSeverityPointChangedEvent(EntityUid Wound, WoundComponent Component, FixedPoint2 OldSeverity, FixedPoint2 NewSeverity, FixedPoint2? Overflow = null);

/// <summary>
/// Raised on a woundable before it attempts to heal any wound damage, letting subscribers
/// (Traumas: bone/organ/dismemberment traumas block healing until treated) cancel the heal.
/// </summary>
[ByRefEvent]
public record struct WoundHealAttemptEvent(Entity<Components.WoundableComponent> Woundable, bool IgnoreBlockers, bool Cancelled = false);

/// <summary>
/// Raised on a wound entity right before it's removed from its woundable's Wounds
/// container and deleted (reached WoundSeverity.Healed).
/// </summary>
[ByRefEvent]
public readonly record struct WoundRemovedEvent(EntityUid Wound, WoundComponent Component);

/// <summary>
/// Raised on a wound entity right after it's created and inserted into its woundable's
/// Wounds container.
/// </summary>
[ByRefEvent]
public readonly record struct WoundAddedEvent(WoundComponent Component, Components.WoundableComponent Woundable);
