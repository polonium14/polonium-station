using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server.Medical;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Medical;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
public sealed class DefibrillatorChargeTest : GameTest
{
    public sealed class ZapListenerSystem : TestListenerSystem<TargetDefibrillatedEvent>;
    public sealed class CancelZapSystem : EntitySystem
    {
        public override void Initialize()
        {
            SubscribeLocalEvent<CancelDefibrillatorChargeTestComponent, SelfBeforeDefibrillatorZapsEvent>(OnDirectedEvent);
        }

        private void OnDirectedEvent(Entity<CancelDefibrillatorChargeTestComponent> ent, ref SelfBeforeDefibrillatorZapsEvent args)
        {
            args.Cancel();
        }
    }

    [TestCase(100, false, 1, 0)]
    [TestCase(200, false, 1, 100)]
    [TestCase(200, true, 0, 200)]
    public async Task ChargeIsConsumedOnlyForADeliveredShock(int startingCharge, bool cancel, int shocks, int remainingCharge)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        try
        {
            await Server.WaitAssertion(() =>
            {
                var user = SEntMan.SpawnEntity("MobHuman", coords);
                var patient = SEntMan.SpawnEntity("MobHuman", coords);
                SEntMan.AddComponent<TestListenerComponent>(patient);
                if (cancel)
                    SEntMan.AddComponent<CancelDefibrillatorChargeTestComponent>(user);
                var defib = SEntMan.SpawnEntity("Defibrillator", coords);
                Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(user, defib), Is.True);
                Assert.That(SEntMan.System<ItemToggleSystem>().TryActivate(defib, user), Is.True);
                var cells = SEntMan.System<PowerCellSystem>();
                Assert.That(cells.TryGetBatteryFromSlot(defib, out var cell), Is.True);
                var batteries = SEntMan.System<SharedBatterySystem>();
                batteries.SetCharge(cell!.Value.Owner, startingCharge);
                var system = SEntMan.System<DefibrillatorSystem>();
                Assert.That(system.CanZap(defib, patient, user), Is.True);
                system.Zap(defib, patient, user);
                Assert.That(SEntMan.System<ZapListenerSystem>().Count(patient), Is.EqualTo(shocks));
                Assert.That(batteries.GetCharge(cell.Value.Owner), Is.EqualTo(remainingCharge));
            });
        }
        catch (Exception e)
        {
            TestContext.Out.WriteLine(e);
            throw;
        }
    }
}

[RegisterComponent]
public sealed partial class CancelDefibrillatorChargeTestComponent : Component;
