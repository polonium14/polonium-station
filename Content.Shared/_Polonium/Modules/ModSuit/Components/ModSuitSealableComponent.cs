using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Modules.ModSuit.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModSuitSealableComponent : Component
{
    [DataField, AutoNetworkedField, Access(typeof(SharedModSuitSystem))]
    public bool Sealed;

    [DataField, AutoNetworkedField]
    public TimeSpan? DelayPerPart = TimeSpan.FromSeconds(0.3f);

    /// <summary>
    /// How long it takes another player to seal or unseal this part on its wearer.
    /// </summary>
    [DataField]
    public TimeSpan SealOtherDelay = TimeSpan.FromSeconds(5);

    [DataField, AlwaysPushInheritance]
    public ComponentRegistry SealedComponents = [];

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Wearer;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SealSound = new SoundPathSpecifier("/Audio/Mecha/mechmove03.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier? UnsealSound = new SoundPathSpecifier("/Audio/Mecha/mechmove03.ogg");

    [DataField, AutoNetworkedField]
    public Dictionary<bool, List<PrototypeLayerData>> IconLayers = [];

    [DataField, AutoNetworkedField]
    public Dictionary<bool, Dictionary<string, List<PrototypeLayerData>>> ClothingLayers = [];

    [DataField, AutoNetworkedField]
    public Dictionary<bool, SpriteSpecifier> UiLayer = [];

    /// <summary>
    /// Replaces <see cref="IconLayers"/> while the suit has no power, so its lights go dark.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<bool, List<PrototypeLayerData>> UnpoweredIconLayers = [];

    /// <summary>
    /// Replaces <see cref="ClothingLayers"/> for a slot while the suit has no power.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<bool, Dictionary<string, List<PrototypeLayerData>>> UnpoweredClothingLayers = [];

    /// <summary>
    /// Replaces <see cref="UiLayer"/> while the suit has no power.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<bool, SpriteSpecifier> UnpoweredUiLayer = [];

    public HashSet<int> RevealedLayers = [];
}
