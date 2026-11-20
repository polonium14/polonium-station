using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Kitchen.EntitySystems;
using Content.Shared.Kitchen.Components;
using Content.Shared.Kitchen.EntitySystems;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Kitchen;

public sealed class BrokenMicrowaveTest : InteractionTest
{
    private const string Microwave = "KitchenMicrowave";
    private const string Mess = "FoodBadRecipe";
    private const uint CookSeconds = 10;
    private const int CookTimeKey = 2;
    private const int ExplosionTicks = 15;

    private MicrowaveComponent Comp => SEntMan.GetComponent<MicrowaveComponent>(STarget!.Value);

    private async Task SpawnMicrowave(params string[] contents)
    {
        await SpawnTarget(Microwave);
        await Server.WaitPost(() => SEntMan.System<SharedPowerReceiverSystem>().SetNeedsPower(STarget!.Value, false));
        await RunTicks(5);
        foreach (var item in contents)
        {
            await InteractUsing(item);
        }

        Assert.That(Comp.Storage.ContainedEntities, Has.Count.EqualTo(contents.Length));
    }

    private async Task StartCooking()
    {
        await SendBui(MicrowaveUiKey.Key, new MicrowaveSelectCookTimeMessage(CookTimeKey, CookSeconds));
        await SendBui(MicrowaveUiKey.Key, new MicrowaveStartCookMessage());
        AssertComp<ActiveMicrowaveComponent>();
    }

    private async Task Explode()
    {
        await Server.WaitPost(() => SEntMan.System<ServerMicrowaveSystem>().Explode(STarget!.Value));
        await RunTicks(ExplosionTicks);
        Assert.That(Comp.Broken);
    }

    /// <summary>
    /// The window of a microwave that breaks stays until the player closes it, and after that it does not open again.
    /// </summary>
    [Test]
    public async Task BrokenMicrowaveDoesNotReopen()
    {
        await SpawnMicrowave();
        await Activate();
        Assert.That(IsUiOpen(MicrowaveUiKey.Key));

        await Explode();
        Assert.That(IsUiOpen(MicrowaveUiKey.Key), "breaking must not close the window that is already open");

        await CloseBui(MicrowaveUiKey.Key);
        Assert.That(IsUiOpen(MicrowaveUiKey.Key), Is.False);

        await Activate();
        Assert.That(IsUiOpen(MicrowaveUiKey.Key), Is.False, "a broken microwave must not open its window");

        // The player is still able to open a microwave that works.
        var working = await Spawn(Microwave);
        await Activate(working);
        Assert.That(IsUiOpen(MicrowaveUiKey.Key));
    }

    /// <summary>
    /// A broken microwave cannot be opened to take things out, so the explosion has to leave nothing inside.
    /// </summary>
    [Test]
    public async Task ExplosionEmptiesMicrowave()
    {
        await SpawnMicrowave("FoodBreadBun");
        await Activate();
        await StartCooking();

        await Explode();
        AssertComp<ActiveMicrowaveComponent>(false);
        Assert.That(Comp.Storage.ContainedEntities, Is.Empty);
    }

    /// <summary>
    /// Starting with plastic inside used to throw while the plastic was being swapped for the mess.
    /// </summary>
    [Test]
    public async Task PlasticBurnsAtStart()
    {
        await SpawnMicrowave("ForkPlastic", "FoodBreadBun", "SpoonPlastic");
        await Activate();
        await StartCooking();

        var prototypes = Comp.Storage.ContainedEntities
            .Select(uid => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID)
            .ToList();
        Assert.That(prototypes, Is.EquivalentTo(new[] { Mess, Mess, "FoodBreadBun" }));

        await RunSeconds(CookSeconds + 1);
        AssertComp<ActiveMicrowaveComponent>(false);
        Assert.That(Comp.Storage.ContainedEntities, Is.Empty);
    }
}
