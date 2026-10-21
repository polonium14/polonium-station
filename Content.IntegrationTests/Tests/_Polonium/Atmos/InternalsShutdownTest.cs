using Content.IntegrationTests.Fixtures;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Atmos;

[TestFixture]
[TestOf(typeof(Content.Shared.Body.Systems.SharedInternalsSystem))]
public sealed class InternalsShutdownTest : GameTest
{
    /// <summary>
    /// When a mob's internals go away (the mob is deleted or gibbed) while its tank survives,
    /// the tank must not keep its networked User pointing at the dead entity.
    /// </summary>
    [Test]
    public async Task TankForgetsAUserWhoseInternalsShutDown()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var vox = entMan.SpawnEntity("MobVox", map.GridCoords);
            EquipInternals(entMan, vox);
            Assert.That(entMan.System<Content.Shared.Inventory.InventorySystem>().TryGetSlotEntity(vox, "pocket1", out var tank));
            var gas = entMan.GetComponent<Content.Shared.Atmos.Components.GasTankComponent>(tank!.Value);
            if (gas.User == null)
                entMan.System<Content.Shared.Atmos.EntitySystems.SharedGasTankSystem>().ConnectToInternals((tank.Value, gas));
            Assert.That(gas.User, Is.EqualTo(vox), "Sanity: the Vox must be breathing from the tank.");

            entMan.RemoveComponent<Content.Shared.Body.Components.InternalsComponent>(vox);
            Assert.That(gas.User, Is.Null);
        });
    }

    /// <summary>
    /// Dropping the mask disconnects the tank while its use delay from connecting is still running.
    /// The tank refuses, but the internals let go of it anyway - so the tank must let go of the user too.
    /// </summary>
    [Test]
    public async Task TankForgetsAUserWhoDisconnectsDuringItsUseDelay()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var vox = entMan.SpawnEntity("MobVox", map.GridCoords);
            EquipInternals(entMan, vox);
            Assert.That(entMan.System<Content.Shared.Inventory.InventorySystem>().TryGetSlotEntity(vox, "pocket1", out var tank));
            var gas = entMan.GetComponent<Content.Shared.Atmos.Components.GasTankComponent>(tank!.Value);
            if (gas.User == null)
                entMan.System<Content.Shared.Atmos.EntitySystems.SharedGasTankSystem>().ConnectToInternals((tank.Value, gas));
            Assert.That(gas.User, Is.EqualTo(vox), "Sanity: the Vox must be breathing from the tank.");

            var internals = entMan.GetComponent<Content.Shared.Body.Components.InternalsComponent>(vox);
            entMan.System<Content.Shared.Body.Systems.SharedInternalsSystem>().DisconnectTank((vox, internals));
            Assert.That(internals.GasTankEntity, Is.Null);
            Assert.That(gas.User, Is.Null, "Both sides must agree the tank is disconnected.");
        });
    }

    private static void EquipInternals(IEntityManager entMan, EntityUid mob)
    {
        var inventory = entMan.System<Content.Shared.Inventory.InventorySystem>();
        var coords = entMan.GetComponent<TransformComponent>(mob).Coordinates;
        inventory.TryEquip(mob, entMan.SpawnEntity("ClothingMaskBreath", coords), "mask", force: true);
        inventory.TryEquip(mob, entMan.SpawnEntity("EmergencyNitrogenTankFilled", coords), "pocket1", force: true);
    }
}
