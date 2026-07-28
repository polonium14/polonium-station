using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Construction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedXenoConstructionSystem))]
public sealed partial class XenoConstructionComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan BuildDelay = TimeSpan.FromSeconds(4);

    [DataField, AutoNetworkedField]
    public List<EntProtoId> CanBuild = new();

    [DataField, AutoNetworkedField]
    public EntProtoId? SelectedStructure;

    [DataField, AutoNetworkedField]
    public bool CanUpgrade;

    [DataField, AutoNetworkedField]
    public EntProtoId WeedPrototype = "XenoWeedsSource";

    [DataField, AutoNetworkedField]
    public FixedPoint2 PlantWeedsCost = 90;

    [DataField, AutoNetworkedField]
    public FixedPoint2 BuildRange = 1.75;

    [DataField, AutoNetworkedField]
    public SoundSpecifier BuildSound = new SoundCollectionSpecifier("RMCResinBuild")
    {
        Params = AudioParams.Default.WithVolume(-10f),
    };
}
