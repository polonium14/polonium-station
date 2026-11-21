#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Polonium.Lathe;
using Content.Server.Lathe;
using Content.Server.Lathe.Components;
using Content.Shared._Polonium.Lathe;
using Content.Shared.Audio;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Lathe;

public sealed class LatheConsoleTest : InteractionTest
{
    private const string Lathe = "Autolathe";
    private const string InstantLathe = "OreProcessor";
    private const int Prints = 50;
    private const int Ordered = 3;
    private const int MaxRunSeconds = 60;

    // Long enough for a print to still be going when the reply to a console message has arrived.
    private static readonly TimeSpan MinPrintTime = TimeSpan.FromSeconds(2);

    private readonly Dictionary<string, int> _stock = new();

    private LatheComponent Machine => SEntMan.GetComponent<LatheComponent>(STarget!.Value);
    private LatheWorkSoundsComponent Sounds => SEntMan.GetComponent<LatheWorkSoundsComponent>(STarget!.Value);
    private bool Humming => SEntMan.GetComponent<AmbientSoundComponent>(STarget!.Value).Enabled;

    /// <summary>
    /// Spawns a powered lathe, opens its console and stocks it for a short recipe that takes time,
    /// or for one that takes none.
    /// </summary>
    private async Task<LatheRecipePrototype> SpawnLathe(string prototype, bool instant)
    {
        await SpawnTarget(prototype);
        LatheRecipePrototype? picked = null;
        _stock.Clear();
        await Server.WaitPost(() =>
        {
            SEntMan.System<SharedPowerReceiverSystem>().SetNeedsPower(STarget!.Value, false);
            picked = SEntMan.System<LatheSystem>()
                .GetAvailableRecipes(STarget.Value, Machine)
                .Select(id => ProtoMan.Index(id))
                .Where(recipe => recipe.Materials.Count > 0
                    && (instant ? recipe.CompleteTime == TimeSpan.Zero : recipe.CompleteTime >= MinPrintTime))
                .MinBy(recipe => recipe.CompleteTime);
            if (picked == null)
                return;

            var storage = SEntMan.System<SharedMaterialStorageSystem>();
            foreach (var (material, amount) in picked.Materials)
            {
                storage.TryChangeMaterialAmount(STarget.Value, material, amount * Prints);
                _stock[material] = storage.GetMaterialAmount(STarget.Value, material);
            }
        });

        Assert.That(picked, Is.Not.Null, $"{prototype} has no recipe to test with");
        Assert.That(_stock.Values, Has.All.Positive, "the lathe did not take the materials");
        await RunTicks(5);
        await Activate();
        return picked!;
    }

    private async Task WaitForRunEnd()
    {
        for (var i = 0; i < MaxRunSeconds * 2 && SEntMan.HasComponent<LatheProducingComponent>(STarget); i++)
        {
            await RunSeconds(0.5f);
        }

        AssertComp<LatheProducingComponent>(false);
    }

    /// <summary>
    /// How many copies of a sound of the lathe, as its prototype sets it, are playing right now.
    /// </summary>
    private int Playing(SoundSpecifier? sound)
    {
        HashSet<string> files = sound switch
        {
            SoundPathSpecifier path => [path.Path.ToString()],
            SoundCollectionSpecifier { Collection: { } collection } =>
                ProtoMan.Index(collection).PickFiles.Select(file => file.ToString()).ToHashSet(),
            _ => [],
        };

        Assert.That(files, Is.Not.Empty, "the prototype of the lathe does not set this sound");
        return SEntMan.EntityQuery<AudioComponent>(true)
            .Select(audio => audio.FileName)
            .Count(files.Contains);
    }

    /// <summary>
    /// A print run starts with the start sound and the loop, and ends with the stop sound and silence.
    /// </summary>
    [Test]
    public async Task RunHasStartLoopAndStop()
    {
        var recipe = await SpawnLathe(Lathe, false);
        Assert.That(Humming, Is.False);

        await SendBui(LatheUiKey.Key, new LatheQueueRecipeMessage(recipe.ID, 1));
        AssertComp<LatheProducingComponent>();
        Assert.Multiple(() =>
        {
            Assert.That(Humming, Is.True);
            Assert.That(Playing(Sounds.StartSound), Is.EqualTo(1));
        });

        await WaitForRunEnd();
        Assert.Multiple(() =>
        {
            Assert.That(Humming, Is.False);
            Assert.That(Playing(Sounds.StopSound), Is.EqualTo(1));
        });
    }

    /// <summary>
    /// A queue of several items is one run: the start sound is not repeated for every item.
    /// </summary>
    [Test]
    public async Task QueueIsOneRun()
    {
        var recipe = await SpawnLathe(Lathe, false);
        await SendBui(LatheUiKey.Key, new LatheQueueRecipeMessage(recipe.ID, Ordered));

        var starts = new HashSet<EntityUid>();
        for (var i = 0; i < MaxRunSeconds * 10 && SEntMan.HasComponent<LatheProducingComponent>(STarget); i++)
        {
            await RunTicks(3);
            if (Sounds.Stream is { } stream && SEntMan.HasComponent<LatheProducingComponent>(STarget))
                starts.Add(stream);
        }

        AssertComp<LatheProducingComponent>(false);
        Assert.Multiple(() =>
        {
            Assert.That(starts, Has.Count.EqualTo(1));
            Assert.That(Humming, Is.False);
            Assert.That(Playing(Sounds.StopSound), Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Recipes that take no time are over before the motor could spin up: one short sound, no start, stop or loop.
    /// </summary>
    [Test]
    public async Task InstantRunHasItsOwnSound()
    {
        var recipe = await SpawnLathe(InstantLathe, true);

        // Counted within the tick of the order: a start sound cut off a tick later would still reach the players.
        var started = -1;
        await Server.WaitAssertion(() =>
        {
            var lathes = SEntMan.System<LatheSystem>();
            Assert.That(lathes.TryAddToQueue(STarget!.Value, recipe, Ordered), Is.True);
            lathes.TryStartProducing(STarget.Value);
            started = Playing(Sounds.StartSound);
        });
        await RunTicks(5);

        AssertComp<LatheProducingComponent>(false);
        Assert.Multiple(() =>
        {
            Assert.That(started, Is.Zero);
            Assert.That(Humming, Is.False);
            Assert.That(Playing(Sounds.InstantSound), Is.EqualTo(1));
            Assert.That(Playing(Sounds.StartSound), Is.Zero);
            Assert.That(Playing(Sounds.StopSound), Is.Zero);
        });
    }

    /// <summary>
    /// Holding STOP cancels the item in work and the whole queue, gives every material back,
    /// and the console shows it until the next print.
    /// </summary>
    [Test]
    public async Task StopAllClearsTheQueue()
    {
        var recipe = await SpawnLathe(Lathe, false);
        var storage = SEntMan.System<SharedMaterialStorageSystem>();

        await SendBui(LatheUiKey.Key, new LatheQueueRecipeMessage(recipe.ID, Ordered));
        AssertComp<LatheProducingComponent>();
        Assert.That(_stock.Keys.Select(material => storage.GetMaterialAmount(STarget!.Value, material)),
            Is.Not.EqualTo(_stock.Values), "queueing did not take any material");

        await SendBui(LatheUiKey.Key, new LatheStopAllMessage());
        await RunTicks(5);

        AssertComp<LatheProducingComponent>(false);
        AssertComp<LatheStoppedComponent>();
        Assert.Multiple(() =>
        {
            Assert.That(Machine.Queue, Is.Empty);
            Assert.That(Machine.CurrentRecipe, Is.Null);
            Assert.That(Humming, Is.False);
            Assert.That(Playing(Sounds.StopSound), Is.EqualTo(1));
            foreach (var (material, amount) in _stock)
            {
                Assert.That(storage.GetMaterialAmount(STarget!.Value, material), Is.EqualTo(amount), material);
            }
        });

        await SendBui(LatheUiKey.Key, new LatheQueueRecipeMessage(recipe.ID, 1));
        await RunTicks(5);
        AssertComp<LatheStoppedComponent>(false);
    }

    /// <summary>
    /// Holding STOP on an idle lathe is harmless.
    /// </summary>
    [Test]
    public async Task StopAllOnIdleLathe()
    {
        await SpawnLathe(Lathe, false);
        await SendBui(LatheUiKey.Key, new LatheStopAllMessage());
        await RunTicks(5);

        AssertComp<LatheStoppedComponent>();
        Assert.Multiple(() =>
        {
            Assert.That(Machine.Queue, Is.Empty);
            Assert.That(Playing(Sounds.StopSound), Is.Zero);
            foreach (var (material, amount) in _stock)
            {
                Assert.That(SEntMan.System<SharedMaterialStorageSystem>().GetMaterialAmount(STarget!.Value, material),
                    Is.EqualTo(amount), material);
            }
        });
    }
}
