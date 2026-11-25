using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.MaskHailer;

[Prototype]
public sealed partial class MaskHailerSetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<MaskHailerPhrase> Phrases = new();
}

[DataDefinition]
public sealed partial class MaskHailerPhrase
{
    /// <summary>
    /// What the wearer says in chat while the recording plays.
    /// </summary>
    [DataField(required: true)]
    public LocId Text;

    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;
}
