using System.Numerics;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Polonium.Weapons.Ranged.Components;
using Content.Shared._Polonium.Weapons.Ranged.Systems;
using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Sprinting;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._Polonium.Weapons;

[TestOf(typeof(GunMovementSpreadSystem))]
public sealed class GunMovementSpreadTest : InteractionTest
{
    private const string GunProto = "WeaponPistolMk58";
    private const double Tolerance = 0.01;

    protected override string PlayerPrototype => "MobHuman";

    private async Task<double> PeekAtSpeed(EntityUid gun, float speed)
    {
        var degrees = 0.0;
        await Server.WaitAssertion(() =>
        {
            SEntMan.System<SharedPhysicsSystem>().SetLinearVelocity(SPlayer, new Vector2(speed, 0f));
            var gunComp = SEntMan.GetComponent<GunComponent>(gun);
            degrees = SEntMan.System<SharedGunSystem>().PeekSpread((gun, gunComp), SPlayer).Degrees;
        });
        return degrees;
    }

    [Test]
    public async Task NoPenaltyWhenDisabled()
    {
        var gun = ToServer(await PlaceInHands(GunProto));
        var run = SEntMan.GetComponent<MovementSpeedModifierComponent>(SPlayer).CurrentSprintSpeed;

        Assert.That(await PeekAtSpeed(gun, run), Is.EqualTo(2).Within(Tolerance));
    }

    [Test]
    public async Task PenaltyGrowsWithSpeed()
    {
        await OverrideCVar(Side.Server, CCVars.GunMovementSpread, true);
        var gun = ToServer(await PlaceInHands(GunProto));
        var move = SEntMan.GetComponent<MovementSpeedModifierComponent>(SPlayer);
        var penalty = SEntMan.GetComponent<GunMovementSpreadComponent>(gun);
        var still = await PeekAtSpeed(gun, 0f);

        Assert.That(still, Is.EqualTo(MinSpread(gun)).Within(Tolerance));
        Assert.That(await PeekAtSpeed(gun, move.CurrentWalkSpeed), Is.EqualTo(still + penalty.Walk.Degrees).Within(Tolerance));
        Assert.That(await PeekAtSpeed(gun, move.CurrentSprintSpeed), Is.EqualTo(still + penalty.Run.Degrees).Within(Tolerance));
    }

    [Test]
    public async Task SprintPenaltyFadesAfterSprint()
    {
        await OverrideCVar(Side.Server, CCVars.GunMovementSpread, true);
        var gun = ToServer(await PlaceInHands(GunProto));

        await Server.WaitPost(() =>
        {
            var sprinter = SEntMan.GetComponent<SprinterComponent>(SPlayer);
            sprinter.IsSprinting = false;
            sprinter.LastSprint = STiming.CurTime;
        });

        var penalty = SEntMan.GetComponent<GunMovementSpreadComponent>(gun);
        Assert.That(await PeekAtSpeed(gun, 0f), Is.EqualTo(MinSpread(gun) + penalty.Sprint.Degrees).Within(Tolerance));

        await RunSeconds(1.1f);
        Assert.That(await PeekAtSpeed(gun, 0f), Is.EqualTo(MinSpread(gun)).Within(Tolerance));
    }

    private double MinSpread(EntityUid gun)
    {
        return SEntMan.GetComponent<GunComponent>(gun).MinAngleModified.Degrees;
    }
}
