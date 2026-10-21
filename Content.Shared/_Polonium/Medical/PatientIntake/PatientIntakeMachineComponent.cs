using Content.Shared.Access;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.DeviceLinking;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Radio;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Medical.PatientIntake;

/// <summary>
/// Morgue-like machine that delivers NPC patients with generated injuries and pays the
/// department account when they come back healthy.
/// </summary>
[RegisterComponent]
public sealed partial class PatientIntakeMachineComponent : Component
{
    /// <summary>
    /// The on/off toggle. Losing power also stops it, without flipping this.
    /// </summary>
    [DataField]
    public bool Enabled = true;

    /// <summary>
    /// Needed to switch the machine on. Anyone can switch it off.
    /// </summary>
    [DataField]
    public ProtoId<AccessLevelPrototype> EnableAccess = "Medical";

    /// <summary>
    /// Any of these lets someone mark the active case as missing.
    /// </summary>
    [DataField]
    public List<ProtoId<AccessLevelPrototype>> MarkMissingAccess = new() { "ChiefMedicalOfficer", "Captain" };

    [DataField]
    public ProtoId<CargoAccountPrototype> Account = "Medical";

    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = "Medical";

    [DataField]
    public ProtoId<DepartmentPrototype> Department = "Medical";

    [DataField]
    public ProtoId<SourcePortPrototype> FaxPort = "PatientIntakeFax";

    [DataField]
    public ProtoId<RandomHumanoidSettingsPrototype> Humanoid = "PatientIntakePatient";

    /// <summary>
    /// Clothing groups a patient can arrive in, picked by their weight.
    /// </summary>
    [DataField]
    public List<ProtoId<PatientIntakeOutfitPrototype>> Outfits = new();

    /// <summary>
    /// Extra gear per species, e.g. nitrogen internals for Vox.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<SpeciesPrototype>, ProtoId<StartingGearPrototype>> SpeciesGear = new();

    /// <summary>
    /// Delay between patients of a mass casualty case.
    /// </summary>
    [DataField]
    public TimeSpan PopDelay = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Payout cut range when any issued gear is missing from the tray.
    /// </summary>
    [DataField]
    public float GearCutMin = 0.5f;

    [DataField]
    public float GearCutMax = 0.75f;

    /// <summary>
    /// Time left until the next case. Only counts down while the machine is on and powered.
    /// </summary>
    [ViewVariables]
    public TimeSpan UntilNextCase;

    [ViewVariables]
    public PatientIntakeCase? Case;

    /// <summary>
    /// Running case number, printed on papers.
    /// </summary>
    [ViewVariables]
    public int CaseCount;

    /// <summary>
    /// Net spesos of the last finished case.
    /// </summary>
    [ViewVariables]
    public int LastNet;

    /// <summary>
    /// Cases closed this round, for the round-end summary.
    /// </summary>
    [ViewVariables]
    public int CasesClosed;

    /// <summary>
    /// Net spesos paid to (or charged from) the account this round.
    /// </summary>
    [ViewVariables]
    public int TotalNet;
}

public sealed class PatientIntakeCase
{
    public ProtoId<PatientIntakeCasePrototype> Proto;

    public int Payout;

    public TimeSpan Window;

    /// <summary>
    /// Set the first time the tray is opened with a patient inside.
    /// </summary>
    public TimeSpan? ClockStart;

    public int PatientsTotal;

    /// <summary>
    /// Patients not spawned yet (mass casualty).
    /// </summary>
    public int ToSpawn;

    public TimeSpan NextPop;

    /// <summary>
    /// Spawned and not yet judged.
    /// </summary>
    public HashSet<EntityUid> Patients = new();

    /// <summary>
    /// Pay minus debits so far; settled when the case ends.
    /// </summary>
    public int Net;

    public List<string> Receipt = new();
}
