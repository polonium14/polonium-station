using Content.Client._Polonium.Modules.ModSuit.Events;
using Content.Shared.Clothing;
using Content.Shared.Hands;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Polonium.Modules.ModSuit;

public partial class ModSuitSystem
{
    private void InitializeDeployableRelay()
    {
        SubscribeLocalEvent<ModSuitDeployedPartComponent, AppearanceChangeEvent>(RelayToSuit);
        SubscribeLocalEvent<ModSuitDeployedPartComponent, GetEquipmentVisualsEvent>(RelayToSuit);
        SubscribeLocalEvent<ModSuitDeployedPartComponent, GetInhandVisualsEvent>(RelayToSuit);

        SubscribeLocalEvent<ModSuitDeployedPartComponent, ModSuitSealedGetIconLayersEvent>(RelayToSuit);
        SubscribeLocalEvent<ModSuitDeployedPartComponent, ModSuitSealedGetClothingLayersEvent>(RelayToSuit);
    }
}
