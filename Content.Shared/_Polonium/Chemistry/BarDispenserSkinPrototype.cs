using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Chemistry;

/// <summary>
/// How a bar dispenser is drawn in its window: the colours of its body and keys and what falls out of its nozzle.
/// </summary>
[Prototype]
public sealed partial class BarDispenserSkinPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Whether the stored containers stand upside down on a shelf above the keys.
    /// Without it the device has a plain lid and its containers stay out of sight.
    /// </summary>
    [DataField]
    public bool ShowContainers;

    /// <summary>
    /// Main colour of the body.
    /// </summary>
    [DataField(required: true)]
    public Color Body;

    /// <summary>
    /// Colour of the lit edges of the body.
    /// </summary>
    [DataField(required: true)]
    public Color BodyLight;

    /// <summary>
    /// Colour of the brightest edge, the front of the lid.
    /// </summary>
    [DataField(required: true)]
    public Color BodyBright;

    /// <summary>
    /// Colour of the shaded edges of the body.
    /// </summary>
    [DataField(required: true)]
    public Color BodyShade;

    /// <summary>
    /// Colour of the back wall behind the containers.
    /// </summary>
    [DataField(required: true)]
    public Color BodyDeep;

    /// <summary>
    /// Colour of the recess the drink keys sit in.
    /// </summary>
    [DataField(required: true)]
    public Color Well;

    /// <summary>
    /// Colour of a drink key.
    /// </summary>
    [DataField(required: true)]
    public Color Key;

    /// <summary>
    /// Colour of a drink key under the pointer.
    /// </summary>
    [DataField(required: true)]
    public Color KeyHover;

    /// <summary>
    /// Colour of a drink key that is held down.
    /// </summary>
    [DataField(required: true)]
    public Color KeyPressed;

    /// <summary>
    /// Colour of the lower edge of a drink key.
    /// </summary>
    [DataField(required: true)]
    public Color KeyShade;

    /// <summary>
    /// Colour of the captions printed on the body.
    /// </summary>
    [DataField(required: true)]
    public Color Print;

    /// <summary>
    /// Reagents that fall out of the nozzle as cubes.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ReagentPrototype>> Cubes = new();

    /// <summary>
    /// Reagents that fall out of the nozzle as grains.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ReagentPrototype>> Grains = new();

    /// <summary>
    /// How fizzy a reagent has to be to hiss as it is poured.
    /// </summary>
    [DataField]
    public float FizzFrom = 0.3f;

    /// <summary>
    /// Heard when a still drink reaches the vessel.
    /// </summary>
    [DataField]
    public SoundSpecifier? PourSound;

    /// <summary>
    /// Heard when a fizzy drink reaches the vessel.
    /// </summary>
    [DataField]
    public SoundSpecifier? FizzSound;

    /// <summary>
    /// Heard when the first cube lands in the vessel.
    /// </summary>
    [DataField]
    public SoundSpecifier? CubesSound;

    /// <summary>
    /// Heard when grains start to fall into the vessel.
    /// </summary>
    [DataField]
    public SoundSpecifier? GrainsSound;

    /// <summary>
    /// Heard when the vessel is emptied over the grille.
    /// </summary>
    [DataField]
    public SoundSpecifier? DrainSound;

    /// <summary>
    /// Heard when a stored container leaves the dispenser.
    /// </summary>
    [DataField]
    public SoundSpecifier? ContainerOutSound;
}
