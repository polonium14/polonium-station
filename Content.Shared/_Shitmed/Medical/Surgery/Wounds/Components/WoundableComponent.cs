// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
[Access(typeof(Systems.WoundSystem))]
public sealed partial class WoundableComponent : Component
{
    public const string WoundContainerId = "Wounds";
    public const string BoneContainerId = "Bone";

    /// <summary>
    /// Indicates whether wounds are allowed to be induced on this limb at all.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AllowWounds = true;

    /// <summary>
    /// The same as DamageableComponent's one — which damage types this woundable accepts.
    /// </summary>
    [DataField("damageContainer")]
    public ProtoId<DamageContainerPrototype>? DamageContainerID;

    /// <summary>
    /// Prototype spawned into the Bone container on MapInit, unless this organ has
    /// BonelessComponent.
    /// </summary>
    [DataField]
    public EntProtoId BoneEntity = "Bone";

    /// <summary>
    /// Maximum integrity points of this limb.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public FixedPoint2 IntegrityCap;

    /// <summary>
    /// Current integrity points, derived by summing contained (non-scar) wounds' severity.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public FixedPoint2 WoundableIntegrity;

    /// <summary>
    /// How big is the woundable, mostly used for trauma calculation, dodging and targeting.
    /// </summary>
    [DataField]
    public FixedPoint2 DodgeChance = 0.1;

    /// <summary>
    /// Severity thresholds mapping WoundableSeverity levels to their integrity point values.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<WoundableSeverity, FixedPoint2> Thresholds = new();

    /// <summary>
    /// Pre-sorted version of <see cref="Thresholds"/> in descending order by value.
    /// Populated on init to avoid per-call sort allocations.
    /// </summary>
    [Access(typeof(Systems.WoundSystem))]
    public KeyValuePair<WoundableSeverity, FixedPoint2>[]? SortedThresholds;

    /// <summary>
    /// How much damage will be healed across all wounds on this limb per tick, shared
    /// between all wounds it holds.
    /// </summary>
    [DataField]
    public FixedPoint2 HealAbility = 0.03;

    /// <summary>
    /// How much this limb is bleeding.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public FixedPoint2 Bleeds;

    /// <summary>
    /// How much bleeding will be treated per tick on each wound this limb holds - unlike
    /// <see cref="HealAbility"/> it is not shared between them, so recovery does not slow down as
    /// bleeders accumulate. Only applies while <see cref="CanHealBleeds"/>, i.e. minor bleeding -
    /// heavy bleeding (at or above <see cref="BleedsThreshold"/>) never closes on its own.
    /// </summary>
    [ViewVariables, DataField]
    public FixedPoint2 BleedingTreatmentAbility = 0.05f;

    /// <summary>
    /// Bleed level above which passive bleed-healing halts.
    /// </summary>
    [DataField]
    public FixedPoint2 BleedsThreshold = 3.5f;

    /// <summary>
    /// Integrity-damage level above which passive damage-healing halts.
    /// </summary>
    [DataField]
    public FixedPoint2 DamageThreshold = 45;

    public bool CanHealDamage => WoundableIntegrity > DamageThreshold && WoundableIntegrity < IntegrityCap;

    public bool CanHealBleeds => Bleeds > FixedPoint2.Zero && Bleeds < BleedsThreshold;

    /// <summary>
    /// Per-source severity multipliers, keyed by an arbitrary caller-chosen identifier.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, FixedPoint2> SeverityMultipliers = new();

    /// <summary>
    /// Per-source healing-rate multipliers, same keying scheme as <see cref="SeverityMultipliers"/>.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, FixedPoint2> HealingMultipliers = new();

    [DataField]
    public SoundSpecifier WoundableDestroyedSound = new SoundCollectionSpecifier("WoundableDestroyed");

    [DataField]
    public SoundSpecifier WoundableDelimbedSound = new SoundCollectionSpecifier("WoundableDelimbed");

    /// <summary>
    /// Current derived overall severity of this limb. Recomputed by WoundSystem whenever
    /// WoundableIntegrity changes.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public WoundableSeverity WoundableSeverity = WoundableSeverity.Healthy;

    /// <summary>
    /// The container holding this limb's wound entities. Null until ComponentInit runs.
    /// </summary>
    [ViewVariables]
    public Container? Wounds;

    /// <summary>
    /// The container holding this limb's (singular) bone entity. Null until ComponentInit runs.
    /// </summary>
    [ViewVariables]
    public Container? Bone;

    /// <summary>
    /// Whether this limb can be amputated/dismembered at all.
    /// </summary>
    [DataField]
    public bool CanRemove = true;

    [DataField]
    public bool CanBleed = true;

    [ViewVariables]
    public bool IsBoneExposed;

    /// <summary>
    /// Extra damage inflicted on the redirect target when this limb is amputated.
    /// </summary>
    [DataField]
    public DamageSpecifier? DamageOnAmputate;

    /// <summary>
    /// Timestamp of the last time this limb took net-positive damage.
    /// </summary>
    [ViewVariables]
    public TimeSpan LastDamageTime;
}
