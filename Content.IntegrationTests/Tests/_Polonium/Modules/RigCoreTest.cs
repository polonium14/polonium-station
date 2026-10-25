using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Polonium.Modules.ModSuit;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Modules;

[TestOf(typeof(SharedModSuitSystem))]
public sealed class RigCoreTest : InteractionTest
{
    private static readonly EntProtoId Rig = "ClothingBackpackModSuitEngineering";

    protected override string PlayerPrototype => "MobHuman";

    [Test]
    public async Task BuildRig()
    {
        await SpawnTarget("ModSuitConstructionShell");
        await Interact(
            "ModSuitCoreStandard",
            Screw,
            (Cable, 5),
            Cut,
            "ModSuitConstructionHelmet",
            (Glass, 2),
            "ModSuitConstructionChestplate",
            "ModSuitConstructionGauntlets",
            "ModSuitConstructionBoots",
            Wrench,
            Weld,
            Screw,
            "Multitool",
            "ModSuitPlatingEngineering");

        AssertPrototype(Rig);
    }

    [Test]
    [TestCase("AnomalyCoreGravity", "ModSuitCoreGravity")]
    [TestCase("AnomalyCoreBluespaceInert", "ModSuitCoreBluespace")]
    [TestCase("AnomalyCoreElectricity", "ModSuitCoreElectric")]
    public async Task UpgradeCoreWithAnomaly(string anomalyCore, string result)
    {
        await SpawnTarget("ModSuitCoreStandard");
        await InteractUsing(anomalyCore);
        AssertPrototype("ModSuitCoreUnsecured");

        await InteractUsing(Screw);
        AssertPrototype(result);
    }

    [Test]
    public async Task UpgradeCoreWithPlasma()
    {
        await SpawnTarget("ModSuitCoreStandard");
        await InteractUsing(("Plasma", 5));
        await InteractUsing(Screw);
        AssertPrototype("ModSuitCorePlasma");
    }

    [Test]
    public async Task UnsecuredCoreGivesAnomalyCoreBack()
    {
        await SpawnTarget("ModSuitCoreStandard");
        await InteractUsing("AnomalyCoreGravity");
        await InteractUsing(Pry);
        AssertPrototype("ModSuitCoreStandard");
    }

    private async Task<EntityUid> SpawnRigWithCore(EntProtoId core)
    {
        var rig = ToServer(await SpawnTarget(Rig));
        await Server.WaitPost(() =>
        {
            var container = SEntMan.System<SharedContainerSystem>();
            var slot = container.EnsureContainer<Container>(rig, "core");
            Assert.That(container.Insert(SEntMan.SpawnEntity(core, MapCoordinates.Nullspace), slot));
        });
        await RunTicks(5);
        return rig;
    }

    [Test]
    public async Task GravityCoreLightensRig()
    {
        var rig = ToServer(await SpawnTarget(Rig));
        await Server.WaitPost(() => Assert.That(SEntMan.System<InventorySystem>().TryEquip(SPlayer, rig, "back", true, true)));
        await RunTicks(5);

        var move = SEntMan.GetComponent<MovementSpeedModifierComponent>(SPlayer);
        var walk = move.WalkSpeedModifier;

        await Server.WaitPost(() =>
        {
            var container = SEntMan.System<SharedContainerSystem>();
            var slot = container.EnsureContainer<Container>(rig, "core");
            container.Insert(SEntMan.SpawnEntity("ModSuitCoreGravity", MapCoordinates.Nullspace), slot);
            SEntMan.System<MovementSpeedModifierSystem>().RefreshMovementSpeedModifiers(SPlayer);
        });
        await RunTicks(5);

        Assert.That(move.WalkSpeedModifier, Is.EqualTo(walk * 0.75f / 0.7f).Within(0.001f));
    }

    [Test]
    public async Task ElectricCoreRechargesCell()
    {
        var rig = await SpawnRigWithCore("ModSuitCoreElectric");

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<PowerCellSystem>().TryGetBatteryFromSlot(rig, out var battery));
            var rate = SEntMan.System<SharedBatterySystem>().RefreshChargeRate(battery!.Value.AsNullable());
            Assert.That(rate, Is.EqualTo(0.1f).Within(0.001f));
        });
    }

    [Test]
    public async Task PlasmaCoreRefuelsWithoutTakingStorage()
    {
        var rig = await SpawnRigWithCore("ModSuitCorePlasma");
        var battery = SEntMan.System<SharedBatterySystem>();
        Entity<BatteryComponent>? cell = null;

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<PowerCellSystem>().TryGetBatteryFromSlot(rig, out cell));
            battery.SetCharge(cell!.Value.AsNullable(), 0f);
        });

        var plasma = await PlaceInHands(("Plasma", 3));
        await Interact(altInteract: true);

        Assert.Multiple(() =>
        {
            Assert.That(battery.GetCharge(cell!.Value.AsNullable()), Is.EqualTo(cell.Value.Comp.MaxCharge * 0.25f).Within(1f));
            Assert.That(SEntMan.GetComponent<StackComponent>(ToServer(plasma)).Count, Is.EqualTo(2), "Refuelling did not use exactly one sheet.");
            Assert.That(SEntMan.HasComponent<StorageComponent>(rig) && SEntMan.GetComponent<StorageComponent>(rig).StoredItems.ContainsKey(ToServer(plasma)), Is.False, "Plasma went into the R.I.G. storage.");
        });

        // A plain click still stores the sheets instead of burning them.
        await Interact();
        Assert.That(SEntMan.GetComponent<StorageComponent>(rig).StoredItems, Does.ContainKey(ToServer(plasma)), "Plasma could not be stored in the R.I.G.");
    }
}
