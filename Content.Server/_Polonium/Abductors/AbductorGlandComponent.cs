using Content.Shared._Polonium.Abductors;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Polonium.Abductors;

/// <summary>
/// An organ in the heart slot that acts on its carrier once the research chamber has woken it.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(AbductorGlandSystem))]
public sealed partial class AbductorGlandComponent : Component
{
    /// <summary>
    /// What the Curator says about this gland after the experiment. Until then nobody knows its kind.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<AbductorCuratorLinePrototype> Report;

    [DataField]
    public TimeSpan MinInterval = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan MaxInterval = TimeSpan.FromMinutes(6);

    /// <summary>
    /// How many more times the gland's triggers fire. Null means no limit, zero leaves only its passive effects.
    /// </summary>
    [DataField]
    public int? Uses;

    /// <summary>
    /// Components the carrier has for as long as the gland works in it.
    /// </summary>
    [DataField]
    public ComponentRegistry BodyComponents = new();

    /// <summary>
    /// Status effect the carrier has for as long as the gland works in it.
    /// </summary>
    [DataField]
    public EntProtoId? BodyStatusEffect;

    [ViewVariables]
    public bool Awake;

    [ViewVariables]
    public EntityUid? Body;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextTrigger;

    [ViewVariables]
    public ComponentRegistry AddedComponents = new();
}
