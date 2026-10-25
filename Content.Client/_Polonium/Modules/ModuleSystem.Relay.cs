using Content.Client._Polonium.Modules.ModSuit.Events;
using Content.Shared.Clothing;
using Content.Shared.Hands;
using Content.Shared._Polonium.Modules.Components;
using Content.Shared._Polonium.Modules.ModSuit.Events;
using Robust.Client.GameObjects;

namespace Content.Client._Polonium.Modules;

public partial class ModuleSystem
{
    private void InitializeRelay()
    {
        SubscribeLocalEvent<ModuleContainerComponent, AppearanceChangeEvent>(RelayToModules);
        SubscribeLocalEvent<ModuleContainerComponent, GetEquipmentVisualsEvent>(RelayToModules);
        SubscribeLocalEvent<ModuleContainerComponent, GetInhandVisualsEvent>(RelayToModules);

        SubscribeLocalEvent<ModuleContainerComponent, ModSuitDeployedPartRelayedEvent<AppearanceChangeEvent>>(RelayToModules);
        SubscribeLocalEvent<ModuleContainerComponent, ModSuitDeployedPartRelayedEvent<GetEquipmentVisualsEvent>>(RelayToModules);
        SubscribeLocalEvent<ModuleContainerComponent, ModSuitDeployedPartRelayedEvent<GetInhandVisualsEvent>>(RelayToModules);

        SubscribeLocalEvent<ModuleContainerComponent, ModSuitDeployedPartRelayedEvent<ModSuitSealedGetClothingLayersEvent>>(RelayToModules);
        SubscribeLocalEvent<ModuleContainerComponent, ModSuitDeployedPartRelayedEvent<ModSuitSealedGetIconLayersEvent>>(RelayToModules);
    }
}
