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

    public HashSet<int> RevealedLayers = [];
}
