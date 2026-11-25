using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Audio;

namespace Content.Server._Polonium.Abductors;

/// <summary>
/// Makes the target let out the call of a random creature, with a matching emote.
/// </summary>
[RegisterComponent]
public sealed partial class AbductorCreatureCallOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField(required: true)]
    public List<AbductorCreatureCall> Calls = new();

    [DataField]
    public LocId Popup = "abductor-gland-popup-creature-call";
}

[DataDefinition]
public sealed partial class AbductorCreatureCall
{
    [DataField(required: true)]
    public LocId Emote;

    [DataField(required: true)]
    public SoundSpecifier Sound = default!;
}
