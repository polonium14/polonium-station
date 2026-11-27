using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Horror;

/// <summary>
/// A chemical the horror can pump into its host's blood from the chemicals menu.
/// </summary>
[Prototype("horrorChemical")]
public sealed partial class HorrorChemicalPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    /// <summary>
    /// Units injected per use.
    /// </summary>
    [DataField]
    public FixedPoint2 Amount = 10;

    /// <summary>
    /// Chemicals spent per use.
    /// </summary>
    [DataField]
    public int Cost = 30;

    /// <summary>
    /// Short description shown in the chemicals menu.
    /// </summary>
    [DataField(required: true)]
    public LocId Description;

    /// <summary>
    /// Upgrade that unlocks the chemical, null when every horror has it.
    /// </summary>
    [DataField]
    public ProtoId<HorrorUpgradePrototype>? RequiredUpgrade;
}

/// <summary>
/// A permanent upgrade bought with devoured souls in the mutations menu.
/// The effect fields stack with the horror's base values.
/// </summary>
[Prototype("horrorUpgrade")]
public sealed partial class HorrorUpgradePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    /// <summary>
    /// Souls it costs.
    /// </summary>
    [DataField]
    public int SoulPrice = 2;

    /// <summary>
    /// Extra chemicals regenerated per tick.
    /// </summary>
    [DataField]
    public int ChemicalRegenBonus;

    /// <summary>
    /// Chemicals regenerate outside of a host too.
    /// </summary>
    [DataField]
    public bool RegenWithoutHost;

    /// <summary>
    /// Damage healed per tick, everywhere.
    /// </summary>
    [DataField]
    public FixedPoint2 Regeneration;

    /// <summary>
    /// Multiplies the horror's damage thresholds (crit and death).
    /// </summary>
    [DataField]
    public float ThresholdMultiplier = 1f;

    /// <summary>
    /// Extra bite damage, of the bite's own type.
    /// </summary>
    [DataField]
    public FixedPoint2 BiteBonus;

    /// <summary>
    /// Extra bite damage against silicons, on top of <see cref="BiteBonus"/>.
    /// </summary>
    [DataField]
    public FixedPoint2 SiliconBiteBonus;

    /// <summary>
    /// How much shorter bonding with the host's brain is.
    /// </summary>
    [DataField]
    public TimeSpan BondTimeReduction = TimeSpan.Zero;

    /// <summary>
    /// The trapped mind needs about twice as long to resist the control.
    /// </summary>
    [DataField]
    public bool DeepControl;

    /// <summary>
    /// The tentacle trip shocks and knocks out the victim instead.
    /// </summary>
    [DataField]
    public bool ParalyzingTrip;
}
