using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Polonium.Weapons.Ranged.Systems;

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
    [TestCase("WeaponTurretSyndicate", "Fan")]
    [TestCase("WeaponShotgunDoubleBarreledRubber", "Cross")]
    public async Task PicksCrosshairOfGun(string gun, string crosshair)
    {
        var target = ToServer(await SpawnTarget(gun));

        await Server.WaitAssertion(() =>
        {
            var picked = SEntMan.System<GunCrosshairSystem>().GetCrosshair(target, out var pellets);
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
            var picked = SEntMan.System<GunCrosshairSystem>().GetCrosshair(target, out var pellets);
            Assert.That(picked.Id, Is.EqualTo("Arcs"));
            Assert.That(pellets.Degrees, Is.EqualTo(15 * 0.6).Within(0.01)); // buckshot cone times the Kammerer choke
        });
    }
}
