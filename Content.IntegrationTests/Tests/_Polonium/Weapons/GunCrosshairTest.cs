using Content.Client._Polonium.Crosshairs;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Polonium.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.IntegrationTests.Tests._Polonium.Weapons;

[TestOf(typeof(GunCrosshairSystem))]
public sealed class GunCrosshairTest : InteractionTest
{
    [TestCase("WeaponPistolMk58", "Cross")]
    [TestCase("WeaponSniperMosin", "TCross")]
    [TestCase("Musket", "Cross")]
    [TestCase("WeaponLauncherChinaLake", "Grid")]
    [TestCase("WeaponImprovisedPneumaticCannon", "Corners")]
    [TestCase("BowImprovised", "Ring")]
    [TestCase("WeaponWandFireball", "Cloud")]
    [TestCase("WeaponWaterPistol", "DashedRing")]
    [TestCase("RevolverCapGun", "PixelCross")]
    [TestCase("WeaponTurretSyndicate", "Rails")]
    [TestCase("WeaponShotgunDoubleBarreledRubber", "Cross")]
    public async Task PicksCrosshairOfGun(string gun, string crosshair)
    {
        var target = ToServer(await SpawnTarget(gun));

        await Server.WaitAssertion(() =>
        {
            SEntMan.System<GunCrosshairSystem>().TryGetCrosshair(target, out var picked, out var pellets);
            Assert.That(picked.Id, Is.EqualTo(crosshair));
            Assert.That(pellets.Degrees, Is.Zero);
        });
    }

    [Test]
    public async Task BuckshotShowsPelletCone()
    {
        var target = ToServer(await SpawnTarget("WeaponShotgunKammerer"));

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<GunCrosshairSystem>().TryGetCrosshair(target, out var picked, out var pellets));
            Assert.That(picked.Id, Is.EqualTo("Arcs"));
            Assert.That(pellets.Degrees, Is.EqualTo(15 * 0.6).Within(0.01)); // buckshot cone times the Kammerer choke
        });
    }

    [Test]
    public async Task EmptiedShotgunKeepsPelletCrosshair()
    {
        var gun = await PlaceInHands("WeaponShotgunKammerer");
        await SetCombatMode(true);
        await RunTicks(5);

        var crosshairs = CEntMan.System<CombatCrosshairSystem>();
        await Client.WaitAssertion(() => Assert.That(crosshairs.Crosshair?.ID, Is.EqualTo("Arcs")));

        var sGun = ToServer(gun);
        await Server.WaitPost(() =>
            SGun.SetBallisticUnspawned((sGun, SEntMan.GetComponent<BallisticAmmoProviderComponent>(sGun)), 0));
        await RunTicks(5);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.System<GunCrosshairSystem>().TryGetCrosshair(ToClient(gun), out _, out _), Is.False);
            Assert.That(crosshairs.Crosshair?.ID, Is.EqualTo("Arcs"));
        });
    }
}
