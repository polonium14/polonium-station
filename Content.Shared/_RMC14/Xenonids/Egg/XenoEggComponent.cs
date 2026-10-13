using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Egg;

// Polonium: jajo z NSV13. Rośnie 90-150 s, potem czeka na ofiarę obok i wypuszcza facehuggera.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoEggSystem))]
public sealed partial class XenoEggComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId SpawnPrototype = "XenoFacehugger";

    [DataField, AutoNetworkedField]
    public XenoEggState State = XenoEggState.Growing;

    [DataField, AutoNetworkedField]
    public TimeSpan MinGrowTime = TimeSpan.FromSeconds(90);

    [DataField, AutoNetworkedField]
    public TimeSpan MaxGrowTime = TimeSpan.FromSeconds(150);

    [DataField, AutoNetworkedField]
    public TimeSpan NextGrow;

    /// <summary>
    /// Ile trwa animacja otwierania, zanim wyskoczy facehugger.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan OpenTime = TimeSpan.FromSeconds(1.5);

    [DataField, AutoNetworkedField]
    public TimeSpan OpenAt;

    /// <summary>
    /// Czy facehugger ma wyjść martwy.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool KillChild;

    /// <summary>
    /// Z jakiej odległości dojrzałe jajo wyczuwa ofiarę.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TriggerRange = 1.5f;

    [DataField, AutoNetworkedField]
    public TimeSpan NextCheck;
}

[Serializable, NetSerializable]
public enum XenoEggState : byte
{
    Growing,
    Grown,
    Opening,
    Opened,
}

[Serializable, NetSerializable]
public enum XenoEggVisuals : byte
{
    State,
}
