using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Polonium.Medical.BedInternals;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Inventory;
using Content.Shared.Verbs;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Polonium.Medical;

[TestFixture]
[TestOf(typeof(BedInternalsSystem))]
public sealed class BedInternalsTest : GameTest
{
    private const string GasSlot = "gasTankSlot";
    private const string MaskContainer = "bedMask";
    private const string MaskSlot = "mask";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: ClothingMaskBase
  id: BedInternalsTestBlockingMask
  components:
  - type: Sprite
    sprite: Clothing/Mask/gas.rsi
  - type: Clothing
    sprite: Clothing/Mask/gas.rsi
";

    private EntityUid _bed;
    private EntityUid _patient;
    private EntityUid _user;
    private EntityUid _tank;

    private async Task SetUp(string bed)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        await Server.WaitPost(() =>
        {
            _bed = SEntMan.SpawnEntity(bed, coords);
            _patient = SEntMan.SpawnEntity("MobHuman", coords);
            _user = SEntMan.SpawnEntity("MobHuman", coords);
            _tank = SEntMan.SpawnEntity("OxygenTank", coords);
        });

        await Pair.RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<ItemSlotsSystem>().TryInsert(_bed, GasSlot, _tank, null),
                "Sanity check: the tank should fit into the slot.");
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(_patient, null, _bed, popup: false),
                "Sanity check: the patient should buckle to the bed.");
        });

        await Pair.RunTicksSync(5);
    }

    private async Task ToggleInternals()
    {
        await Server.WaitPost(() =>
        {
            var text = Server.ResolveDependency<ILocalizationManager>().GetString(
                SEntMan.GetComponent<BedInternalsComponent>(_bed).Enabled
                    ? "bed-internals-verb-disable"
                    : "bed-internals-verb-enable");

            var verbs = SEntMan.System<SharedVerbSystem>();
            var verb = verbs.GetLocalVerbs(_bed, _user, typeof(InteractionVerb), force: true).Single(v => v.Text == text);
            verbs.ExecuteVerb(verb, _user, _bed, forced: true);
        });

        await Pair.RunTicksSync(5);
    }

    private async Task Unbuckle()
    {
        await Server.WaitPost(() => SEntMan.System<SharedBuckleSystem>().Unbuckle(_patient, null));
        await Pair.RunTicksSync(5);
    }

    private EntityUid? BedMask()
    {
        var container = SEntMan.System<SharedContainerSystem>().GetContainer(_bed, MaskContainer);
        return container.ContainedEntities.Cast<EntityUid?>().FirstOrDefault();
    }

    [Test]
    public async Task TankOverlayFollowsTheTankInTheSlot()
    {
        await SetUp("MedicalBed");

        await Server.WaitAssertion(() =>
        {
            var appearance = SEntMan.System<SharedAppearanceSystem>();
            Assert.That(appearance.TryGetData<BedTankVisual>(_bed, BedInternalsVisuals.TankVisual, out var visual));
            Assert.That(visual, Is.EqualTo(BedTankVisual.Oxygen));

            SEntMan.System<ItemSlotsSystem>().TryEject(_bed, GasSlot, null, out _);

            Assert.That(appearance.TryGetData(_bed, BedInternalsVisuals.TankVisual, out visual));
            Assert.That(visual, Is.EqualTo(BedTankVisual.None));
        });
    }

    [TestCase("MedicalBed")]
    [TestCase("OperatingTable")]
    public async Task EnablingSuppliesTheBedTankThroughTheBedMask(string bed)
    {
        await SetUp(bed);
        await ToggleInternals();

        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            var internals = SEntMan.GetComponent<InternalsComponent>(_patient);

            Assert.That(SEntMan.GetComponent<BedInternalsComponent>(_bed).Enabled);
            Assert.That(internals.GasTankEntity, Is.EqualTo(_tank));
            Assert.That(SEntMan.GetComponent<GasTankComponent>(_tank).User, Is.EqualTo(_patient));
            Assert.That(SEntMan.System<SharedInternalsSystem>().AreInternalsWorking(_patient));

            Assert.That(inventory.TryGetSlotEntity(_patient, MaskSlot, out var worn));
            Assert.That(SEntMan.GetComponent<BedInternalsPatientComponent>(_patient).BedMask, Is.EqualTo(worn));
            Assert.That(BedMask(), Is.Null, "The mask should be on the patient, not in the bed.");
        });
    }

    [Test]
    public async Task ReleasingReturnsTheMaskAndDisconnectsTheTank()
    {
        await SetUp("MedicalBed");

        EntityUid? mask = null;
        await Server.WaitPost(() => mask = BedMask());
        Assert.That(mask, Is.Not.Null, "Sanity check: the bed should come with its mask.");

        await ToggleInternals();
        await Unbuckle();

        await Server.WaitAssertion(() =>
        {
            var internals = SEntMan.GetComponent<InternalsComponent>(_patient);

            Assert.That(SEntMan.GetComponent<BedInternalsComponent>(_bed).Enabled, Is.False);
            Assert.That(internals.GasTankEntity, Is.Null);
            Assert.That(SEntMan.GetComponent<GasTankComponent>(_tank).User, Is.Null);
            Assert.That(SEntMan.HasComponent<BedInternalsPatientComponent>(_patient), Is.False);
            Assert.That(SEntMan.System<InventorySystem>().TryGetSlotEntity(_patient, MaskSlot, out _), Is.False);
            Assert.That(BedMask(), Is.EqualTo(mask), "The mask should be back in the bed.");
        });
    }

    [Test]
    public async Task RemovingTheTankDisablesInternals()
    {
        await SetUp("MedicalBed");
        await ToggleInternals();

        await Server.WaitPost(() => SEntMan.System<ItemSlotsSystem>().TryEject(_bed, GasSlot, null, out _));
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BedInternalsComponent>(_bed).Enabled, Is.False);
            Assert.That(SEntMan.GetComponent<InternalsComponent>(_patient).GasTankEntity, Is.Null);
            Assert.That(SEntMan.GetComponent<GasTankComponent>(_tank).User, Is.Null);
            Assert.That(BedMask(), Is.Not.Null, "The mask should be back in the bed.");
        });
    }

    [Test]
    public async Task PatientTankIsConnectedAgainAfterRelease()
    {
        await SetUp("MedicalBed");

        EntityUid ownTank = default;
        await Server.WaitPost(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            Assert.That(inventory.SpawnItemInSlot(_patient, MaskSlot, "ClothingMaskBreath", silent: true, force: true));
            Assert.That(inventory.SpawnItemInSlot(_patient, "back", "OxygenTank", silent: true, force: true));
            Assert.That(inventory.TryGetSlotEntity(_patient, "back", out var back));
            ownTank = back!.Value;
        });
        await Pair.RunTicksSync(5);

        await Server.WaitPost(() =>
            Assert.That(SEntMan.System<SharedGasTankSystem>().ConnectToInternals((ownTank, SEntMan.GetComponent<GasTankComponent>(ownTank)), _patient),
                "Sanity check: the patient's own tank should connect."));

        // A tank that was just toggled refuses to be toggled again for a moment.
        await Pair.RunSeconds(2f);

        await ToggleInternals();

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<InternalsComponent>(_patient).GasTankEntity, Is.EqualTo(_tank));
            Assert.That(SEntMan.GetComponent<GasTankComponent>(ownTank).User, Is.Null);
        });

        await Unbuckle();

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<InternalsComponent>(_patient).GasTankEntity, Is.EqualTo(ownTank));
            Assert.That(SEntMan.GetComponent<GasTankComponent>(ownTank).User, Is.EqualTo(_patient));
            Assert.That(SEntMan.GetComponent<GasTankComponent>(_tank).User, Is.Null);
            Assert.That(SEntMan.System<InventorySystem>().TryGetSlotEntity(_patient, MaskSlot, out _),
                "The patient's own mask should stay on.");
        });
    }

    [Test]
    public async Task AMaskThatCannotBreatheIsLeftAlone()
    {
        await SetUp("MedicalBed");

        await Server.WaitPost(() =>
            Assert.That(SEntMan.System<InventorySystem>().SpawnItemInSlot(_patient, MaskSlot, "BedInternalsTestBlockingMask", silent: true, force: true)));
        await Pair.RunTicksSync(5);

        await ToggleInternals();

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BedInternalsComponent>(_bed).Enabled, Is.False);
            Assert.That(SEntMan.GetComponent<InternalsComponent>(_patient).GasTankEntity, Is.Null);
            Assert.That(SEntMan.HasComponent<BedInternalsPatientComponent>(_patient), Is.False);
            Assert.That(BedMask(), Is.Not.Null, "The bed should keep its mask.");
        });
    }

    [Test]
    public async Task TheLastPatientLeavingTurnsInternalsOff()
    {
        await SetUp("MedicalBed");
        await ToggleInternals();
        await Unbuckle();

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<BedInternalsComponent>(_bed).Enabled, Is.False));

        await Server.WaitPost(() =>
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(_patient, null, _bed, popup: false)));
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<InternalsComponent>(_patient).GasTankEntity, Is.Null,
                "Internals that were switched off must not come back for the next patient.");
            Assert.That(SEntMan.HasComponent<BedInternalsPatientComponent>(_patient), Is.False);
        });
    }
}
