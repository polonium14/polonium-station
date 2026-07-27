using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Pheromones;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoPheromonesSystem))]
public sealed partial class XenoActivePheromonesComponent : Component
{
    public HashSet<EntityUid> Receivers = new();

    [DataField, AutoNetworkedField]
    public XenoPheromones Pheromones;
}
