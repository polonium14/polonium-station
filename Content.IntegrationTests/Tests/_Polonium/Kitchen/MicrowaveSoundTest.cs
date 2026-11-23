#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Kitchen.EntitySystems;
using Content.Shared._Polonium.Kitchen;
using Content.Shared.Input;
using Content.Shared.Kitchen.Components;
using Content.Shared.Kitchen.EntitySystems;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;

namespace Content.IntegrationTests.Tests._Polonium.Kitchen;

public sealed class MicrowaveSoundTest : InteractionTest
{
    private const string Microwave = "KitchenMicrowave";
    private const string SafeMicrowave = "KitchenMicrowaveTutorial";
    private const uint CookSeconds = 5;
    private const int CookTimeKey = 1;
    private const int WindowTicks = 30;
    private const int MaxListenTicks = 600;

    private MicrowaveSoundsComponent Sounds => SEntMan.GetComponent<MicrowaveSoundsComponent>(STarget!.Value);

    private SoundSpecifier DoneSound => SEntMan.GetComponent<MicrowaveComponent>(STarget!.Value).FoodDoneSound;

    private bool Cooking => SEntMan.HasComponent<ActiveMicrowaveComponent>(STarget);

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

    private Dictionary<EntityUid, string> ClientSounds()
    {
        var sounds = new Dictionary<EntityUid, string>();
        var query = CEntMan.AllEntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out var audio))
        {
            var name = audio.FileName;
            sounds[uid] = name;
        }

        return sounds;
    }

    /// <summary>
    /// Does the action and returns the files of the sounds that the client starts to play after it.
    /// Listens for as long as the condition holds and for a short while more.
    /// </summary>
    private async Task<List<string>> Listen(Func<Task> action, Func<bool>? during = null)
    {
        Dictionary<EntityUid, string> old = [];
        await Client.WaitPost(() => old = ClientSounds());
        await action();

        var heard = new Dictionary<EntityUid, string>();
        var left = WindowTicks;
        for (var i = 0; i < MaxListenTicks && left > 0; i++)
        {
            await RunTicks(1);
            await Client.WaitPost(() =>
            {
                foreach (var (uid, file) in ClientSounds())
                {
                    if (!old.ContainsKey(uid))
                        heard[uid] = file;
                }
            });

            if (during?.Invoke() != true)
                left--;
        }

        return heard.Values.ToList();
    }

    private int Count(List<string> heard, SoundSpecifier? sound)
    {
        return heard.Count(Files(sound).Contains);
    }

    /// <summary>
    /// Sends a message from the window the way the window does, so the client predicts what it causes.
    /// </summary>
    private Task Send(BoundUserInterfaceMessage message)
    {
        Assert.That(TryGetBui(MicrowaveUiKey.Key, out var bui));
        return Client.WaitPost(() => bui!.SendPredictedMessage(message));
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
    /// The player who cooks hears the door shut once at the start, and the door open with one beep at the end.
    /// Food alone does not sizzle.
    /// </summary>
    [Test]
    public async Task DoorSoundsAroundCooking()
    {
        await SpawnMicrowave(Microwave, "FoodBreadBun");
        await SendBui(MicrowaveUiKey.Key, new MicrowaveSelectCookTimeMessage(CookTimeKey, CookSeconds));
        var start = await Listen(() => Send(new MicrowaveStartCookMessage()));
        AssertComp<ActiveMicrowaveComponent>();
        Assert.Multiple(() =>
        {
            Assert.That(Count(start, Sounds.DoorCloseSound), Is.EqualTo(1));
            Assert.That(Count(start, Sounds.DoorOpenSound), Is.Zero);
            Assert.That(Count(start, Sounds.BurnSound), Is.Zero);
            Assert.That(Count(start, DoneSound), Is.Zero);
        });

        var end = await Listen(() => Task.CompletedTask, () => Cooking);
        AssertComp<ActiveMicrowaveComponent>(false);
        Assert.Multiple(() =>
        {
            Assert.That(Count(end, Sounds.DoorOpenSound), Is.EqualTo(1));
            Assert.That(Count(end, Sounds.DoorCloseSound), Is.Zero);
            Assert.That(Count(end, DoneSound), Is.EqualTo(1));
        });
    }

    /// <summary>
    /// A player without the window hears the door and the beep of a microwave that runs next to them.
    /// </summary>
    [Test]
    public async Task DoorHeardWithoutWindow()
    {
        await SpawnMicrowave(Microwave, "FoodBreadBun");
        await SendBui(MicrowaveUiKey.Key, new MicrowaveSelectCookTimeMessage(CookTimeKey, CookSeconds));
        await CloseBui(MicrowaveUiKey.Key);

        var start = await Listen(() => Server.WaitPost(() =>
        {
            var microwave = SEntMan.GetComponent<MicrowaveComponent>(STarget!.Value);
            SEntMan.System<ServerMicrowaveSystem>().StartCooking((STarget.Value, microwave), null);
        }));
        AssertComp<ActiveMicrowaveComponent>();
        Assert.That(Count(start, Sounds.DoorCloseSound), Is.EqualTo(1));

        var end = await Listen(() => Task.CompletedTask, () => Cooking);
        Assert.Multiple(() =>
        {
            Assert.That(Count(end, Sounds.DoorOpenSound), Is.EqualTo(1));
            Assert.That(Count(end, DoneSound), Is.EqualTo(1));
        });
    }

    /// <summary>
    /// An instant cook is over before the door could move, so the door stays quiet and only the beep is heard.
    /// </summary>
    [Test]
    public async Task InstantCookKeepsDoorQuiet()
    {
        await SpawnMicrowave(Microwave, "FoodBreadBun");
        await SendBui(MicrowaveUiKey.Key, new MicrowaveSelectCookTimeMessage(0, 0));
        var heard = await Listen(() => Send(new MicrowaveStartCookMessage()));

        AssertComp<ActiveMicrowaveComponent>(false);
        Assert.Multiple(() =>
        {
            Assert.That(Count(heard, Sounds.DoorCloseSound), Is.Zero);
            Assert.That(Count(heard, Sounds.DoorOpenSound), Is.Zero);
            Assert.That(Count(heard, DoneSound), Is.EqualTo(1));
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
    /// The player who closes the window of an idle microwave hears the door shut once, and hears it open once with the window.
    /// Closing used to play the door several times while the server confirmed it.
    /// </summary>
    [Test]
    public async Task WindowClicksOnceForItsOwner()
    {
        await SpawnMicrowave(Microwave);
        Assert.That(TryGetBui(MicrowaveUiKey.Key, out var bui));
        var closing = await Listen(() => Client.WaitPost(() => bui!.Close()));
        Assert.Multiple(() =>
        {
            Assert.That(Count(closing, Sounds.DoorCloseSound), Is.EqualTo(1));
            Assert.That(Count(closing, Sounds.DoorOpenSound), Is.Zero);
        });

        var opening = await Listen(() => SetKey(ContentKeyFunctions.ActivateItemInWorld, BoundKeyState.Down));
        Assert.Multiple(() =>
        {
            Assert.That(Count(opening, Sounds.DoorOpenSound), Is.EqualTo(1));
            Assert.That(Count(opening, Sounds.DoorCloseSound), Is.Zero);
        });
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
