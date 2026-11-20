using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Polonium.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Kitchen.EntitySystems;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Kitchen;

public sealed class MicrowaveSoundTest : InteractionTest
{
    private const string Microwave = "KitchenMicrowave";
    private const string SafeMicrowave = "KitchenMicrowaveTutorial";
    private const string DoorClose = "/Audio/Items/toolbox_close.ogg";
    private const string DoorOpen = "/Audio/Items/toolbox_open.ogg";
    private const string Burn = "/Audio/Effects/sizzle.ogg";
    private const string Sparks = "/Audio/Effects/sparks";
    private const uint CookSeconds = 5;
    private const int CookTimeKey = 1;

    private MicrowaveSoundsComponent Sounds => SEntMan.GetComponent<MicrowaveSoundsComponent>(STarget!.Value);

    private async Task SpawnMicrowave(string prototype, params string[] contents)
    {
        await SpawnTarget(prototype);
        await Server.WaitPost(() => SEntMan.System<SharedPowerReceiverSystem>().SetNeedsPower(STarget!.Value, false));
        await RunTicks(5);
        foreach (var item in contents)
        {
            await InteractUsing(item);
        }

        await Activate();
    }

    private async Task StartCooking(int key, uint seconds)
    {
        await SendBui(MicrowaveUiKey.Key, new MicrowaveSelectCookTimeMessage(key, seconds));
        await SendBui(MicrowaveUiKey.Key, new MicrowaveStartCookMessage());
    }

    private async Task WaitForCookEnd()
    {
        for (var i = 0; i < CookSeconds * 2 && SEntMan.HasComponent<ActiveMicrowaveComponent>(STarget); i++)
        {
            await RunSeconds(1);
        }

        AssertComp<ActiveMicrowaveComponent>(false);
    }

    private int Playing(string file)
    {
        return SEntMan.EntityQuery<AudioComponent>(true)
            .Select(audio => audio.FileName)
            .Count(name => name.StartsWith(file, StringComparison.Ordinal));
    }

    /// <summary>
    /// The door is heard shutting when cooking starts and opening when it ends, and food alone does not sizzle.
    /// </summary>
    [Test]
    public async Task DoorSoundsAroundCooking()
    {
        await SpawnMicrowave(Microwave, "FoodBreadBun");
        await StartCooking(CookTimeKey, CookSeconds);
        AssertComp<ActiveMicrowaveComponent>();
        Assert.Multiple(() =>
        {
            Assert.That(Playing(DoorClose), Is.EqualTo(1));
            Assert.That(Playing(DoorOpen), Is.Zero);
            Assert.That(Playing(Burn), Is.Zero);
        });

        await WaitForCookEnd();
        Assert.That(Playing(DoorOpen), Is.EqualTo(1));
    }

    /// <summary>
    /// An instant cook is over before the door could move, so the door stays quiet.
    /// </summary>
    [Test]
    public async Task InstantCookKeepsDoorQuiet()
    {
        await SpawnMicrowave(Microwave, "FoodBreadBun");
        await StartCooking(0, 0);
        await RunTicks(5);

        AssertComp<ActiveMicrowaveComponent>(false);
        Assert.Multiple(() =>
        {
            Assert.That(Playing(DoorClose), Is.Zero);
            Assert.That(Playing(DoorOpen), Is.Zero);
        });
    }

    /// <summary>
    /// Plastic that melts at the start is heard burning.
    /// </summary>
    [Test]
    public async Task MeltingPlasticSizzles()
    {
        await SpawnMicrowave(Microwave, "ForkPlastic");
        await StartCooking(CookTimeKey, CookSeconds);
        Assert.That(Playing(Burn), Is.EqualTo(1));

        await WaitForCookEnd();
    }

    /// <summary>
    /// Metal keeps crackling for as long as the microwave runs with it.
    /// </summary>
    [Test]
    public async Task MetalCrackles()
    {
        await SpawnMicrowave(SafeMicrowave, "Fork");
        await StartCooking(CookTimeKey, CookSeconds);
        var first = Sounds.NextSpark;
        var wait = Math.Max(0, (first - STiming.CurTime).TotalSeconds);
        await RunSeconds((float) wait + 0.1f);
        Assert.Multiple(() =>
        {
            Assert.That(Sounds.NextSpark, Is.GreaterThan(first));
            Assert.That(Playing(Sparks), Is.GreaterThanOrEqualTo(1));
        });

        await WaitForCookEnd();
    }
}
