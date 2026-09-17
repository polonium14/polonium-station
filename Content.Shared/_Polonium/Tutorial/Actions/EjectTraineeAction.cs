using Content.Shared._Polonium.Tutorial.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>
/// Throws the trainee out: off the chair, onto an anchor in the next room, flat on the floor,
/// and the flow jumps straight to <see cref="Step"/> without finishing the steps in between.
/// </summary>
public sealed partial class EjectTraineeAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField(required: true)]
    public ProtoId<TutorialStepPrototype> Step;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/hit_kick.ogg");

    [DataField]
    public float KnockdownSeconds = 3f;
}
