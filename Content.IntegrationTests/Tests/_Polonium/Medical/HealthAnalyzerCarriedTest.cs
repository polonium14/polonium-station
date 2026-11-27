using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Medical.HealthAnalyzer;
using Content.Shared.Storage.EntitySystems;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Medical;

/// <summary>
/// A scanning health analyzer keeps its patient when put in a pocket, the PDA slot or a medical belt, and loses them
/// when put in a backpack.
/// </summary>
[TestFixture]
public sealed class HealthAnalyzerCarriedTest : GameTest
{
    private const string Analyzer = "HandheldHealthAnalyzer";

    public enum Place
    {
        Pocket,
        PdaSlot,
        MedicalBelt,
        Backpack,
    }

    [TestCase(Place.Pocket, true)]
    [TestCase(Place.PdaSlot, true)]
    [TestCase(Place.MedicalBelt, true)]
    [TestCase(Place.Backpack, false)]
    public async Task ScanSurvivesPuttingAway(Place place, bool keepsScanning)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var medic = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var analyzer = SEntMan.SpawnAtPosition(Analyzer, map.GridCoords);
            var inventory = SEntMan.System<InventorySystem>();
            var storage = SEntMan.System<SharedStorageSystem>();
            var hands = SEntMan.System<SharedHandsSystem>();

            Assert.That(hands.TryPickupAnyHand(medic, analyzer));
            StartScan(analyzer, patient);

            switch (place)
            {
                case Place.Pocket:
                    Assert.That(inventory.TryEquip(medic, analyzer, "pocket1", force: true));
                    break;
                case Place.PdaSlot:
                    Assert.That(inventory.TryEquip(medic, analyzer, "id", force: true));
                    break;
                case Place.MedicalBelt:
                    var belt = SEntMan.SpawnAtPosition("ClothingBeltMedical", map.GridCoords);
                    Assert.That(inventory.TryEquip(medic, belt, "belt", force: true));
                    Assert.That(storage.Insert(belt, analyzer, out _, user: medic, playSound: false));
                    break;
                case Place.Backpack:
                    var backpack = SEntMan.SpawnAtPosition("ClothingBackpackMedical", map.GridCoords);
                    Assert.That(inventory.TryEquip(medic, backpack, "back", force: true));
                    Assert.That(storage.Insert(backpack, analyzer, out _, user: medic, playSound: false));
                    break;
            }

            var scanned = SEntMan.GetComponent<HealthAnalyzerComponent>(analyzer).ScannedEntity;
            Assert.That(scanned, keepsScanning ? Is.EqualTo(patient) : Is.Null);

            // The window stays open only while the medic can still reach the analyzer, the closed belt included.
            if (keepsScanning)
                Assert.That(SEntMan.System<SharedInteractionSystem>().IsAccessible(medic, analyzer), "The window would close.");
        });
    }

    /// <summary>
    /// Starts the scan the way a finished scanning do-after does, without playing out the do-after.
    /// </summary>
    private void StartScan(EntityUid analyzer, EntityUid patient)
    {
        var system = SEntMan.EntitySysManager.GetEntitySystem<HealthAnalyzerSystem>();
        var begin = typeof(HealthAnalyzerSystem).GetMethod("BeginAnalyzingEntity", BindingFlags.Instance | BindingFlags.NonPublic)!;
        begin.Invoke(system, [new Entity<HealthAnalyzerComponent>(analyzer, SEntMan.GetComponent<HealthAnalyzerComponent>(analyzer)), patient]);
        Assert.That(SEntMan.GetComponent<HealthAnalyzerComponent>(analyzer).ScannedEntity, Is.EqualTo(patient));
    }
}
