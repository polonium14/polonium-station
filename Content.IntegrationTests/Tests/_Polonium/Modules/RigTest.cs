using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Polonium.Modules;
using Content.Shared._Polonium.Modules.Components;
using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Modules;
using Content.Shared._Polonium.Modules.ModSuit;
using Content.Shared._Polonium.Modules.ModSuit.UI;
using Content.Shared._Polonium.Modules.ModSuit.UI.Modules;
using Content.Shared.Actions;
using Content.Shared.Inventory;
using Content.Shared.Light.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Modules;

[TestOf(typeof(SharedModuleSystem))]
public abstract class RigTest : InteractionTest
{
    private static readonly EntProtoId Rig = "ClothingBackpackModSuitEngineering";
    private static readonly EntProtoId Mobility = "ModSuitModuleMobility";

    private EntityUid _rig;
    private EntityUid _module;

    private async Task EquipRigWithMobility()
    {
        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            _rig = SEntMan.SpawnEntity(Rig, coords);
            _module = SEntMan.SpawnEntity(Mobility, coords);

            var modules = SEntMan.GetComponent<ModuleContainerComponent>(_rig).Modules;
            Assert.That(SEntMan.System<SharedContainerSystem>().Insert(_module, modules), "Mobility module was not installed.");
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(SPlayer, _rig, "back", true, true), "R.I.G. was not equipped.");
        });

        await RunTicks(10);
    }

    private async Task ToggleWithAction()
    {
        var action = SEntMan.GetComponent<ToggleableModuleActionComponent>(_module).Action;
        Assert.That(action, Is.Not.Null, "Mobility module has no toggle action.");

        var netAction = SEntMan.GetNetEntity(action!.Value);
        await Client.WaitPost(() => CEntMan.RaisePredictiveEvent(new RequestPerformActionEvent(netAction)));
        await RunTicks(15);
    }

    private bool UiShowsToggled()
    {
        var ui = SEntMan.System<SharedUserInterfaceSystem>();
        Assert.That(ui.TryGetUiState<ModSuitBoundUserInterfaceState>(_rig, ModSuitUiKey.Key, out var state), "R.I.G. interface has no state.");

        var modules = state!.Entries.OfType<ModSuitModuleBuiEntry>().Single();
        var entry = modules.Modules.Single(m => m.Key == SEntMan.GetNetEntity(_module)).Value;
        return ((ModSuitBaseToggleableModuleBuiEntry) entry).Toggled;
    }

    [Test]
    public async Task MobilityModuleSpeedsUpWearer()
    {
        await EquipRigWithMobility();

        var move = SEntMan.GetComponent<MovementSpeedModifierComponent>(SPlayer);
        var walk = move.WalkSpeedModifier;
        var sprint = move.SprintSpeedModifier;

        await ToggleWithAction();

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.System<ToggleableModuleSystem>().IsToggled(_module), "Action did not enable the module.");
            Assert.That(move.WalkSpeedModifier, Is.EqualTo(walk * 1.15f).Within(0.001f));
            Assert.That(move.SprintSpeedModifier, Is.EqualTo(sprint * 1.15f).Within(0.001f));
        });

        await Client.WaitAssertion(() =>
        {
            var clientMove = CEntMan.GetComponent<MovementSpeedModifierComponent>(CPlayer);
            Assert.That(clientMove.WalkSpeedModifier, Is.EqualTo(walk * 1.15f).Within(0.001f), "Client does not predict the faster speed.");
        });

        await ToggleWithAction();

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.System<ToggleableModuleSystem>().IsToggled(_module), Is.False, "Action did not disable the module.");
            Assert.That(move.WalkSpeedModifier, Is.EqualTo(walk).Within(0.001f));
        });
    }

    [Test]
    public async Task ToggleActionUpdatesInterface()
    {
        await EquipRigWithMobility();

        await Server.WaitPost(() => SEntMan.System<SharedUserInterfaceSystem>().OpenUi(_rig, ModSuitUiKey.Key, SPlayer));
        await RunTicks(5);
        Assert.That(UiShowsToggled(), Is.False);

        await ToggleWithAction();
        Assert.That(UiShowsToggled(), "Interface still shows the module as disabled.");

        await ToggleWithAction();
        Assert.That(UiShowsToggled(), Is.False, "Interface still shows the module as enabled.");
    }

    [Test]
    public async Task StorageModuleKeepsItems()
    {
        await EquipRigWithMobility();

        var container = SEntMan.System<SharedContainerSystem>();
        var modules = SEntMan.GetComponent<ModuleContainerComponent>(_rig).Modules;
        var storageModule = modules.ContainedEntities.Single(SEntMan.HasComponent<StorageModuleComponent>);
        EntityUid item = default;

        await Server.WaitPost(() =>
        {
            item = SEntMan.SpawnEntity("Crowbar", SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            Assert.That(SEntMan.System<SharedStorageSystem>().Insert(_rig, item, out _), "Item did not fit into the R.I.G. storage.");
            Assert.That(container.Remove(storageModule, modules), "Storage module was not removed.");
        });
        await RunTicks(5);

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.GetComponent<StorageComponent>(storageModule).StoredItems, Does.ContainKey(item), "Removed storage module lost the item.");
            Assert.That(SEntMan.HasComponent<StorageComponent>(_rig), Is.False);
        });

        await Server.WaitPost(() => Assert.That(container.Insert(storageModule, modules), "Storage module was not installed back."));
        await RunTicks(5);

        Assert.That(SEntMan.GetComponent<StorageComponent>(_rig).StoredItems, Does.ContainKey(item), "Installed storage module did not move the item into the R.I.G.");
    }

    [Test]
    public async Task ModulesTakeTimeToInstallAndRemove()
    {
        var rig = ToServer(await SpawnTarget(Rig));
        var modules = SEntMan.GetComponent<ModuleContainerComponent>(rig).Modules;
        bool Installed() => modules.ContainedEntities.Any(SEntMan.HasComponent<SpeedModuleComponent>);

        await Server.WaitPost(() => SEntMan.System<SharedWiresSystem>().TogglePanel(rig, SEntMan.GetComponent<WiresPanelComponent>(rig), true));
        await RunTicks(5);

        await InteractUsing(Mobility, awaitDoAfters: false);
        await RunSeconds(2);
        Assert.That(Installed(), Is.False, "Module was installed before the do-after ended.");

        await RunSeconds(2);
        Assert.That(Installed(), "Module was not installed.");

        var module = SEntMan.GetNetEntity(modules.ContainedEntities.Single(SEntMan.HasComponent<SpeedModuleComponent>));
        await Server.WaitPost(() => SEntMan.System<SharedUserInterfaceSystem>().OpenUi(rig, ModSuitUiKey.Key, SPlayer));
        await RunTicks(5);

        await SendBui(ModSuitUiKey.Key, new ModSuitEjectButtonMessage(module), Target);
        await RunSeconds(3);
        Assert.That(Installed(), "Module was removed before the do-after ended.");

        await RunSeconds(3);
        Assert.That(Installed(), Is.False, "Module was not removed.");
    }

    [Test]
    public async Task OthersCanUnsealHelmet()
    {
        var wearer = ToServer(await SpawnTarget("MobHuman"));
        var modSuit = SEntMan.System<SharedModSuitSystem>();
        EntityUid? helmet = null;

        await Server.WaitPost(() =>
        {
            var rig = SEntMan.SpawnEntity(Rig, SEntMan.GetComponent<TransformComponent>(wearer).Coordinates);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(wearer, rig, "back", true, true), "R.I.G. was not equipped.");
            Assert.That(modSuit.TryGetDeployedPart(rig, ModSuitPartType.Helmet, out helmet));
            Assert.That(modSuit.SetSeal(helmet!.Value, true), "Helmet was not sealed.");
        });
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var verbs = SEntMan.System<SharedVerbSystem>().GetLocalVerbs(helmet!.Value, SPlayer, typeof(EquipmentVerb));
            var unseal = verbs.SingleOrDefault(v => v.Text == Loc.GetString("modsuit-unseal-other-verb", ("part", helmet.Value)));
            Assert.That(unseal?.Act, Is.Not.Null, "Other players have no verb to unseal the helmet.");
            unseal!.Act!.Invoke();
        });

        await RunSeconds(1);
        Assert.That(modSuit.IsSealed(helmet!.Value), "Helmet was unsealed before the do-after ended.");

        await RunSeconds(3);
        Assert.That(modSuit.IsSealed(helmet.Value), Is.False, "Helmet was not unsealed by another player.");
    }

    [Test]
    public async Task HelmetLightWorksWithoutPower()
    {
        await EquipRigWithMobility();

        await Server.WaitPost(() =>
        {
            var container = SEntMan.System<SharedContainerSystem>();
            container.EmptyContainer(container.GetContainer(_rig, "cell_slot"));
        });
        await RunTicks(5);

        Assert.That(SEntMan.System<SharedModSuitSystem>().TryGetDeployedPart(_rig, ModSuitPartType.Helmet, out var helmet));
        var light = SEntMan.GetComponent<HandheldLightComponent>(helmet!.Value);
        Assert.That(light.ToggleActionEntity, Is.Not.Null, "R.I.G. helmet has no light action.");

        var netAction = SEntMan.GetNetEntity(light.ToggleActionEntity!.Value);
        await Client.WaitPost(() => CEntMan.RaisePredictiveEvent(new RequestPerformActionEvent(netAction)));
        await RunTicks(15);

        Assert.Multiple(() =>
        {
            Assert.That(light.Activated, "Helmet light did not turn on without a cell in the R.I.G.");
            Assert.That(SEntMan.System<SharedPointLightSystem>().TryGetLight(helmet.Value, out var pointLight) && pointLight.Enabled, "Helmet light gives no light.");
        });
    }

    [Test]
    public async Task LightsGoOutWithoutPower()
    {
        await EquipRigWithMobility();

        var modSuit = SEntMan.System<SharedModSuitSystem>();
        var parts = modSuit.GetAllParts(_rig).ToList();
        Assert.That(parts, Is.Not.Empty);
        Assert.That(parts.Append(_rig).Any(modSuit.IsUnpowered), Is.False, "Charged R.I.G. is drawn without power.");

        await Server.WaitPost(() =>
        {
            var container = SEntMan.System<SharedContainerSystem>();
            Assert.That(container.EmptyContainer(container.GetContainer(_rig, "cell_slot")), Is.Not.Empty);
        });
        await RunTicks(5);

        Assert.That(parts.Append(_rig).All(modSuit.IsUnpowered), "R.I.G. without a cell still has its lights on.");

        await Client.WaitAssertion(() =>
        {
            var rig = ToClient(_rig);
            Assert.That(CEntMan.System<SharedModSuitSystem>().IsUnpowered(rig));

            var states = CEntMan.GetComponent<SpriteComponent>(rig).AllLayers.Select(l => l.RsiState.Name);
            Assert.That(states, Does.Contain("icon-unpowered"));
        });
    }
}

public sealed class RigHumanTest : RigTest
{
    protected override string PlayerPrototype => "MobHuman";
}

public sealed class RigDionaTest : RigTest
{
    protected override string PlayerPrototype => "MobDiona";
}

/// <summary>
/// Reptilians look up species-specific clothing states, which load the R.I.G. layer sprites by path.
/// </summary>
public sealed class RigReptilianTest : RigTest
{
    protected override string PlayerPrototype => "MobReptilian";
}
