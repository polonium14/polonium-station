using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Kitchen.EntitySystems;
using Content.Shared._Polonium.Kitchen;
using Content.Shared.Interaction;
using Content.Shared.Kitchen.Components;
using Content.Shared.Kitchen.EntitySystems;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Kitchen;

public sealed class MicrowaveSoundTest : InteractionTest
{
    private const string Microwave = "KitchenMicrowave";
    private const string SafeMicrowave = "KitchenMicrowaveTutorial";
    private const uint CookSeconds = 5;
    private const int CookTimeKey = 1;
    private const int WindowTicks = 30;

    private MicrowaveSoundsComponent Sounds => SEntMan.GetComponent<MicrowaveSoundsComponent>(STarget!.Value);

    /// <summary>
    /// The files that a sound of the microwave can play, as its prototype sets them.
    /// </summary>
    private HashSet<string> Files(SoundSpecifier? sound)
    {
        HashSet<string> files = sound switch
        {
            SoundPathSpecifier path => [path.Path.ToString()],
            SoundCollectionSpecifier { Collection: { } collection } =>
                ProtoMan.Index(collection).PickFiles.Select(file => file.ToString()).ToHashSet(),
            _ => [],
        };

        Assert.That(files, Is.Not.Empty, "the prototype of the microwave does not set this sound");
        return files;
    }

    /// <summary>
    /// Counts how many times the client starts to play the sound while the given ticks pass.
    /// </summary>
    private async Task<int> HeardByClient(SoundSpecifier? sound, int ticks)
    {
        var files = Files(sound);
        var heard = new HashSet<EntityUid>();
        for (var i = 0; i < ticks; i++)
        {
            await RunTicks(1);
            await Client.WaitPost(() =>
            {
                var query = CEntMan.AllEntityQueryEnumerator<AudioComponent>();
                while (query.MoveNext(out var uid, out var audio))
                {
                    var name = audio.FileName;
                    if (files.Contains(name))
                        heard.Add(uid);
                }
            });
        }

        return heard.Count;
    }

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

    private int Playing(SoundSpecifier? sound)
    {
        var files = Files(sound);
        return SEntMan.EntityQuery<AudioComponent>(true)
            .Select(audio => audio.FileName)
            .Count(files.Contains);
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
            Assert.That(Playing(Sounds.DoorCloseSound), Is.EqualTo(1));
            Assert.That(Playing(Sounds.DoorOpenSound), Is.Zero);
            Assert.That(Playing(Sounds.BurnSound), Is.Zero);
        });

        await WaitForCookEnd();
        Assert.That(Playing(Sounds.DoorOpenSound), Is.EqualTo(1));
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
            Assert.That(Playing(Sounds.DoorCloseSound), Is.Zero);
            Assert.That(Playing(Sounds.DoorOpenSound), Is.Zero);
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
        Assert.That(Playing(Sounds.BurnSound), Is.EqualTo(1));

        await WaitForCookEnd();
    }

    /// <summary>
    /// A thing that burns up is heard catching fire a moment after the start, without the sizzle of melting.
    /// </summary>
    [Test]
    public async Task BurningThingCatchesFire()
    {
        await SpawnMicrowave(Microwave, "FoodBreadBun");
        await SendBui(MicrowaveUiKey.Key, new MicrowaveSelectCookTimeMessage(CookTimeKey, CookSeconds));
        await Server.WaitPost(() =>
        {
            // The game burns a thing by leaving it to be deleted at the start, as it does with an unlucky ID card.
            var microwave = SEntMan.GetComponent<MicrowaveComponent>(STarget!.Value);
            SEntMan.QueueDeleteEntity(microwave.Storage.ContainedEntities[0]);
            SEntMan.System<ServerMicrowaveSystem>().StartCooking((STarget.Value, microwave), null);
        });
        await RunTicks(2);
        Assert.Multiple(() =>
        {
            Assert.That(Playing(Sounds.BurnSound), Is.Zero);
            Assert.That(Playing(Sounds.IgniteSound), Is.Zero);
        });

        await RunSeconds((float) Sounds.IgniteDelay.TotalSeconds);
        Assert.That(Playing(Sounds.IgniteSound), Is.EqualTo(1));

        await WaitForCookEnd();
    }

    /// <summary>
    /// The player who opens the window hears the door once, and closing the window makes no sound.
    /// Closing used to play the door several times while the server confirmed it.
    /// </summary>
    [Test]
    public async Task WindowClicksOnceForItsOwner()
    {
        await SpawnMicrowave(Microwave);
        Assert.That(TryGetBui(MicrowaveUiKey.Key, out var bui));
        await Client.WaitPost(() => bui!.Close());
        Assert.That(await HeardByClient(Sounds.DoorOpenSound, WindowTicks), Is.Zero);

        await Client.WaitPost(() => CEntMan.System<SharedInteractionSystem>().InteractionActivate(CPlayer, CTarget!.Value));
        Assert.That(await HeardByClient(Sounds.DoorOpenSound, WindowTicks), Is.EqualTo(1));
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
            Assert.That(Playing(Sounds.SparkSound), Is.GreaterThanOrEqualTo(1));
        });

        await WaitForCookEnd();
    }
}
