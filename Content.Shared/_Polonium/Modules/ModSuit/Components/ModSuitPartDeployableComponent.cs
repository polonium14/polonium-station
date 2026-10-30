using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Modules.ModSuit.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModSuitPartDeployableComponent : Component
{
    [DataField("deployableParts"), AutoNetworkedField, Access(typeof(SharedModSuitSystem))]
    public Dictionary<string, EntProtoId> DeployablePartIds = [];

    [ViewVariables, Access(typeof(SharedModSuitSystem))]
    public Dictionary<string, ContainerSlot> DeployableContainers = [];

    [ViewVariables]
    public Dictionary<string, EntityUid> DeployableParts
    {
        get
        {
            Dictionary<string, EntityUid> parts = [];

            foreach (var (slot, container) in DeployableContainers)
            {
                if (container.ContainedEntity != null)
                    parts.Add(slot, container.ContainedEntity.Value);
            }

            return parts;
        }
    }

    [ViewVariables, AutoNetworkedField]
    public Dictionary<string, EntityUid> DeployedParts = [];

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Wearer;
}
