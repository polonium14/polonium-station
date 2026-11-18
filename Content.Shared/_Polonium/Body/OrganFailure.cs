using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Body;

/// <summary>
/// Lets an organ fall into failure once it is sick. Failure grows through stages, harms the organ further
/// and shows symptoms, and passes when the organ is healthy enough for its stage, repaired or replaced.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrganFailureComponent : Component
{
    /// <summary>
    /// How this organ fails.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<OrganFailurePrototype> Failure;

    /// <summary>
    /// Current stage, 0 while the organ is not failing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Stage;
}

/// <summary>
/// The stages of one kind of organ failure.
/// </summary>
[Prototype]
public sealed partial class OrganFailurePrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Stage 1 first.
    /// </summary>
    [DataField(required: true)]
    public List<OrganFailureStage> Stages = new();
}

[DataDefinition]
public sealed partial class OrganFailureStage
{
    /// <summary>
    /// Fraction of the organ's maximum integrity the failure takes away per second.
    /// </summary>
    [DataField]
    public float SelfDamage;

    /// <summary>
    /// Fraction of its maximum integrity the organ has to get back, on top of being healthy, for the failure to pass.
    /// </summary>
    [DataField]
    public float CureAt;

    /// <summary>
    /// Healing the organ no longer makes the failure pass: only the repair surgery or a new organ does.
    /// </summary>
    [DataField]
    public bool NeedsSurgery;

    /// <summary>
    /// Fraction of its maximum integrity the organ loses for good when the failure grows into this stage.
    /// </summary>
    [DataField]
    public float Scar;

    /// <summary>
    /// Damage dealt to the body per second.
    /// </summary>
    [DataField]
    public DamageSpecifier? Damage;

    /// <summary>
    /// Fraction of the body's oxygen saturation it can keep at most.
    /// </summary>
    [DataField]
    public float SaturationCap = 1f;

    /// <summary>
    /// Oxygen saturation the body loses per second, until it has none left.
    /// </summary>
    [DataField]
    public float SaturationLoss;

    [DataField]
    public List<OrganFailureSymptom> Symptoms = new();
}

/// <summary>
/// Something the patient or the people around notice. Every part is optional.
/// </summary>
[DataDefinition]
public sealed partial class OrganFailureSymptom
{
    /// <summary>
    /// Chance per second.
    /// </summary>
    [DataField(required: true)]
    public float Chance;

    /// <summary>
    /// Popup only the patient sees.
    /// </summary>
    [DataField]
    public LocId? Self;

    /// <summary>
    /// Popup the people around see, with the patient as <c>$name</c>.
    /// </summary>
    [DataField]
    public LocId? Others;

    [DataField]
    public ProtoId<EmotePrototype>? Emote;

    /// <summary>
    /// The patient passes out for this long.
    /// </summary>
    [DataField]
    public TimeSpan? Faint;
}
