using Content.Shared.Alert;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Horror.Components;

/// <summary>
/// An eldritch horror: a parasite that crawls into heads, feeds on chemicals and devours souls to mutate.
/// The infestation core follows the cortical borer from Monolith Station (MPL-2.0),
/// the souls, mutations and host control follow the eldritch horror from Aquila (Yogstation#13033).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class EldritchHorrorComponent : Component
{
    /// <summary>
    /// The body the horror lives in, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Host;

    /// <summary>
    /// Chemicals the horror can spend on injections and abilities.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Chemicals = 10;

    /// <summary>
    /// The most chemicals the horror can store.
    /// </summary>
    [DataField]
    public int MaxChemicals = 250;

    /// <summary>
    /// Chemicals gained every <see cref="RegenInterval"/> inside a living host.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ChemicalRegen = 2;

    /// <summary>
    /// Chemicals gained every <see cref="RegenInterval"/> inside a dead host.
    /// </summary>
    [DataField]
    public int DeadHostChemicalRegen = 1;

    /// <summary>
    /// How often chemicals regenerate and the passive effects tick.
    /// </summary>
    [DataField]
    public TimeSpan RegenInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// When the next regeneration tick happens.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextRegen;

    /// <summary>
    /// Damage the horror heals every tick while it sits in a living host.
    /// </summary>
    [DataField]
    public FixedPoint2 HostHealing = 1;

    /// <summary>
    /// Points to spend on mutations. Each devoured soul adds one.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int SoulPoints = 4;

    /// <summary>
    /// How many souls the horror has devoured.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ConsumedSouls;

    /// <summary>
    /// Mind of the soul the horror hunts, picked with the seek soul ability.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SoulTarget;

    /// <summary>
    /// Abilities the horror owns, as action prototypes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntProtoId> OwnedAbilities = new();

    /// <summary>
    /// Upgrades the horror owns.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<HorrorUpgradePrototype>> Upgrades = new();

    /// <summary>
    /// Action entities granted for <see cref="OwnedAbilities"/>, by prototype.
    /// They live on the horror or, while it controls the body, on the host.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntProtoId, EntityUid> ActionEntities = new();

    /// <summary>
    /// Whether the horror is in full control of the host's body.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Controlling;

    /// <summary>
    /// Time it takes to bond with the host's brain before taking control.
    /// </summary>
    [DataField]
    public TimeSpan BondTime = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Time it takes to crawl out of the host on its own.
    /// </summary>
    [DataField]
    public TimeSpan LeaveTime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Time it takes to crawl into a new host.
    /// </summary>
    [DataField]
    public TimeSpan InfestTime = TimeSpan.FromSeconds(3);

    /// <summary>
    /// While controlling, a hit that leaves the host with at least this much damage hands the body back.
    /// </summary>
    [DataField]
    public FixedPoint2 ControlBreakDamage = 25;

    /// <summary>
    /// The trapped mind of the host while the horror controls the body.
    /// </summary>
    [DataField]
    public EntProtoId CaptiveMindPrototype = "PoloniumHorrorCaptiveMind";

    /// <summary>
    /// Alert counting the stored chemicals.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> ChemicalAlert = "PoloniumHorrorChemicals";

    /// <summary>
    /// Abilities every horror starts with.
    /// </summary>
    [DataField]
    public List<EntProtoId> StartingAbilities = new();

    #region Souls

    /// <summary>
    /// How many souls the seek soul ability offers to choose from.
    /// </summary>
    [DataField]
    public int SoulChoices = 4;

    /// <summary>
    /// Minds offered by the last seek soul, the pick must be one of them.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> SoulCandidates = new();

    /// <summary>
    /// How long devouring a soul from inside its body takes.
    /// </summary>
    [DataField]
    public TimeSpan ConsumeTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// World angle from the horror to the hunted soul, for the locator alert.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle SoulArrow;

    /// <summary>
    /// How far the hunted soul is, for the locator alert.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HorrorSoulDistance SoulDistance = HorrorSoulDistance.Unknown;

    /// <summary>
    /// Alert with the arrow pointing at the hunted soul.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> SoulLocatorAlert = "PoloniumHorrorSoulLocator";

    /// <summary>
    /// Locator distances: within the first the soul counts as reached, then close, then medium, beyond that far.
    /// </summary>
    [DataField]
    public float SoulReachedDistance = 1f;

    [DataField]
    public float SoulCloseDistance = 8f;

    [DataField]
    public float SoulMediumDistance = 16f;

    #endregion

    #region Abilities

    /// <summary>
    /// The horror lies low, under items and tables.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Hiding;

    /// <summary>
    /// The chameleon skin is on.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Invisible;

    /// <summary>
    /// Chemicals needed to turn the chameleon skin on.
    /// </summary>
    [DataField]
    public int InvisibilityStartCost = 10;

    /// <summary>
    /// Chemicals the chameleon skin burns every tick, instead of regenerating.
    /// </summary>
    [DataField]
    public int InvisibilityUpkeep = 5;

    /// <summary>
    /// How much the chameleon skin fades the horror every tick, on the stealth scale from 1 (seen) to -1 (unseen).
    /// </summary>
    [DataField]
    public float InvisibilityFade = 0.8f;

    /// <summary>
    /// How far the trip and the lube spill reach around the horror.
    /// </summary>
    [DataField]
    public float AbilityRange = 1.5f;

    /// <summary>
    /// How long a tripped victim lies on the floor.
    /// </summary>
    [DataField]
    public TimeSpan TripKnockdown = TimeSpan.FromSeconds(7);

    /// <summary>
    /// How long a tripped victim can't move.
    /// </summary>
    [DataField]
    public TimeSpan TripImmobilize = TimeSpan.FromSeconds(5);

    /// <summary>
    /// With the paralyzing upgrade, how long the victim is stunned and asleep.
    /// </summary>
    [DataField]
    public TimeSpan ParalyzeTime = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Shock damage of the paralyzing trip.
    /// </summary>
    [DataField]
    public int ParalyzeShockDamage = 15;

    /// <summary>
    /// Time to squeeze through a closed airlock.
    /// </summary>
    [DataField]
    public TimeSpan SqueezeTime = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Chemicals the lube spill costs.
    /// </summary>
    [DataField]
    public int LubeCost = 50;

    /// <summary>
    /// Chance each tile around the horror gets a puddle.
    /// </summary>
    [DataField]
    public float LubeChance = 0.6f;

    /// <summary>
    /// Reagent and amount spilled on each tile.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> LubeReagent = "SpaceLube";

    [DataField]
    public FixedPoint2 LubeAmount = 5;

    /// <summary>
    /// Chemicals growing the tentacle costs.
    /// </summary>
    [DataField]
    public int TentacleCost = 50;

    /// <summary>
    /// The tentacle put in the host's hand.
    /// </summary>
    [DataField]
    public EntProtoId TentaclePrototype = "HorrorTentacle";

    /// <summary>
    /// Time to move into a new host next to the current one.
    /// </summary>
    [DataField]
    public TimeSpan TransferTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Transfer time when the host pulls the new one.
    /// </summary>
    [DataField]
    public TimeSpan TransferPullTime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Chemicals reviving the host costs.
    /// </summary>
    [DataField]
    public int JumpstartCost = 250;

    /// <summary>
    /// Damage the revived host wakes up with, set in the prototype (Aquila: 30 suffocation).
    /// </summary>
    [DataField]
    public DamageSpecifier JumpstartDamage = new();

    /// <summary>
    /// Shock the revival jolt gives the host.
    /// </summary>
    [DataField]
    public int JumpstartShockDamage = 15;

    /// <summary>
    /// Chance the host notices the horror going through their memories.
    /// </summary>
    [DataField]
    public float MemoryNoticeChance = 0.2f;

    /// <summary>
    /// While controlling, the horror's speech goes to the trapped mind instead of out loud.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool TalkToCaptive;

    #endregion
}

/// <summary>
/// How far the hunted soul is, in the steps the locator arrow shows.
/// </summary>
public enum HorrorSoulDistance : byte
{
    Unknown,
    Reached,
    Close,
    Medium,
    Far,
}
