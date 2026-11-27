using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Polonium.SlotMachine;
using Content.Shared.Cargo.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.SlotMachine;

[TestOf(typeof(SlotMachineSystem))]
public sealed class SlotMachineTest : InteractionTest
{
    private const string MachineProto = "SlotMachineTestAlwaysWin";

    protected override string PlayerPrototype => "MobHuman";

    [TestPrototypes]
    private const string Prototypes = @"
- type: slotMachinePrize
  id: SlotMachineTestWin
  weight: 1
  prizeTable: !type:AllSelector
    children:
    - id: SpaceCash10

- type: entity
  parent: SlotMachine
  id: SlotMachineTestAlwaysWin
  components:
  - type: SlotMachine
    prizes:
    - SlotMachineTestWin
";

    private async Task<SlotMachineComponent> SpawnPoweredMachine()
    {
        await SpawnTarget(MachineProto);
        await Server.WaitPost(() => SEntMan.System<SharedPowerReceiverSystem>().SetNeedsPower(STarget!.Value, false));
        return SEntMan.GetComponent<SlotMachineComponent>(STarget!.Value);
    }

    private int MoneyInSlot(SlotMachineComponent comp)
    {
        var slots = SEntMan.System<ItemSlotsSystem>();
        if (!slots.TryGetSlot(STarget!.Value, comp.MoneySlot, out var slot) || slot.Item is not { } money)
            return 0;

        return SEntMan.GetComponent<StackComponent>(money).Count;
    }

    private int CashOnMap()
    {
        var total = 0;
        var query = SEntMan.EntityQueryEnumerator<CashComponent, StackComponent>();
        while (query.MoveNext(out _, out _, out var stack))
        {
            total += stack.Count;
        }

        return total;
    }

    [Test]
    public async Task SpinTakesMoneyAndPaysOut()
    {
        var comp = await SpawnPoweredMachine();
        await InteractUsing("SpaceCash500", 500);
        Assert.That(MoneyInSlot(comp), Is.EqualTo(500));

        await Activate(awaitDoAfters: false);
        Assert.That(comp.IsSpinning, Is.True);
        Assert.That(MoneyInSlot(comp), Is.EqualTo(500 - comp.SpinCost));

        // A second pull while spinning must not take more money.
        await Activate(awaitDoAfters: false);
        Assert.That(MoneyInSlot(comp), Is.EqualTo(500 - comp.SpinCost));

        await RunSeconds((float) comp.SpinDuration.TotalSeconds + 1f);
        Assert.That(comp.IsSpinning, Is.False);
        Assert.That(MoneyInSlot(comp), Is.EqualTo(500 - comp.SpinCost));
        Assert.That(CashOnMap(), Is.EqualTo(500 - comp.SpinCost + 10), "The prize should have been spawned.");
    }

    [Test]
    public async Task NotEnoughMoneyDoesNotSpin()
    {
        var comp = await SpawnPoweredMachine();
        await InteractUsing("SpaceCash100", 100);

        await Activate(awaitDoAfters: false);
        Assert.That(comp.IsSpinning, Is.False);
        Assert.That(MoneyInSlot(comp), Is.EqualTo(100));
    }
}
