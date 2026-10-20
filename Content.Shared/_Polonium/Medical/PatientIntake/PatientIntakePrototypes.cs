using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Medical.PatientIntake;

/// <summary>
/// One kind of patient-intake case: a difficulty tier or a special order.
/// </summary>
[Prototype]
public sealed partial class PatientIntakeCasePrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Relative roll weight against every other case prototype.
    /// </summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>
    /// Issue points spent per patient. Ignored when <see cref="Issues"/> is set.
    /// </summary>
    [DataField]
    public int Budget;

    /// <summary>
    /// Fixed issues instead of a random budget (specific requests).
    /// </summary>
    [DataField]
    public List<ProtoId<PatientIntakeIssuePrototype>>? Issues;

    [DataField(required: true)]
    public int PayMin;

    [DataField(required: true)]
    public int PayMax;

    /// <summary>
    /// Time limit, counted from the first time the tray is opened.
    /// </summary>
    [DataField]
    public TimeSpan WindowMin = TimeSpan.FromMinutes(10);

    [DataField]
    public TimeSpan WindowMax = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Case file lists every issue instead of vague symptoms.
    /// </summary>
    [DataField]
    public bool FullIssueList;

    /// <summary>
    /// Hard deadline: pay stays flat inside the window and a late return debits the account.
    /// Otherwise pay decays to zero across the window and lateness never debits.
    /// </summary>
    [DataField]
    public bool DebitOnTimeout;

    /// <summary>
    /// Fraction of the would-be payout taken from the account on a debit.
    /// </summary>
    [DataField]
    public float DebitFraction = 0.5f;

    /// <summary>
    /// Lets the generator roll <see cref="PatientIntakeIssueKind.RemovePart"/>.
    /// </summary>
    [DataField]
    public bool AllowMissingParts;

    /// <summary>
    /// Living, connected medical staff needed before this case can be rolled.
    /// </summary>
    [DataField]
    public int MinMedics = 1;

    [DataField]
    public int PatientsMin = 1;

    [DataField]
    public int PatientsMax = 1;
}

public enum PatientIntakeIssueKind : byte
{
    /// <summary>Damage on one random limb.</summary>
    Damage,
    /// <summary>Bone trauma on one random limb.</summary>
    Bone,
    /// <summary>Organ trauma on one organ from <see cref="PatientIntakeIssuePrototype.Categories"/>.</summary>
    Organ,
    /// <summary>Reagent injected into the blood.</summary>
    Poison,
    /// <summary>Damage on a limb plus blood volume lost.</summary>
    Bleed,
    Blind,
    Implant,
    /// <summary>An organ or limb from <see cref="PatientIntakeIssuePrototype.Categories"/> is missing.</summary>
    RemovePart,
    /// <summary>An organ from <see cref="PatientIntakeIssuePrototype.Categories"/> must be swapped for another.</summary>
    ReplacePart,
}

[Prototype]
public sealed partial class PatientIntakeIssuePrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public PatientIntakeIssueKind Kind;

    [DataField]
    public int Cost = 1;

    [DataField]
    public float Weight = 1f;

    [DataField]
    public DamageSpecifier? Damage;

    /// <summary>
    /// Trauma severity for bone/organ issues, blood volume lost for bleeds.
    /// </summary>
    [DataField]
    public FixedPoint2 Severity;

    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    [DataField]
    public FixedPoint2 Quantity;

    [DataField]
    public EntProtoId? Implant;

    [DataField]
    public int EyeDamage;

    [DataField]
    public List<ProtoId<OrganCategoryPrototype>> Categories = new();

    /// <summary>
    /// Case file line when the full issue list is shown. Gets <c>$part</c>.
    /// </summary>
    [DataField(required: true)]
    public LocId Description;

    /// <summary>
    /// Case file line when only symptoms are shown.
    /// </summary>
    [DataField(required: true)]
    public LocId Symptom;
}
