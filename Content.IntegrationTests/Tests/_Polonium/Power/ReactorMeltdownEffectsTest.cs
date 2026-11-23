using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Power.Generation.FissionGenerator;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared._Polonium.Power.Generation.FissionGenerator;
using Content.Shared.Radiation.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Polonium.Power;

[TestFixture]
[TestOf(typeof(ReactorMeltdownEffectsSystem))]
public sealed class ReactorMeltdownEffectsTest : GameTest
{
    private const string ReactorProto = "NuclearReactorEmpty";

    // Floor around the reactor: it is 5x5 tiles, so this leaves room for the corium to flow out.
    private const int FloorRadius = 8;

    public override PoolSettings PoolSettings => new() { Dirty = true, Connected = true };

    private async Task<EntityUid> SpawnReactor()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        var reactor = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var mapSystem = server.EntMan.System<SharedMapSystem>();
            for (var x = -FloorRadius; x <= FloorRadius; x++)
            {
                for (var y = -FloorRadius; y <= FloorRadius; y++)
                {
                    mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            var coords = mapSystem.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero);
            reactor = server.EntMan.SpawnEntity(ReactorProto, coords);

            // The station-wide meltdown warning plays a sound file that doesn't exist, and the client's error fails the test.
            // Cooled reactors stay overheated in these tests, because cooling below that would send the warning again.
            server.EntMan.GetComponent<NuclearReactorComponent>(reactor).HasSentWarning = true;
        });

        return reactor;
    }

    [Test]
    public async Task GlowFollowsTemperature()
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var reactor = await SpawnReactor();

        // Halfway between the meltdown warning (1750 K) and the meltdown temperature (2000 K),
        // which only brings the effects halfway to the meltdown.
        await server.WaitPost(() => entMan.GetComponent<NuclearReactorComponent>(reactor).Temperature = 1875f);
        await RunTicksSync(10);
        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<ReactorMeltdownVisualsComponent>(reactor).Danger, Is.EqualTo(0.25f).Within(0.051f)));

        await server.WaitPost(() => entMan.GetComponent<NuclearReactorComponent>(reactor).Temperature = 1600f);
        await RunTicksSync(10);
        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<ReactorMeltdownVisualsComponent>(reactor).Danger, Is.Zero));
    }

    [Test]
    public async Task MeltdownWaitsForTheCriticalPhase()
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();
        var reactor = await SpawnReactor();

        await server.WaitAssertion(() =>
        {
            entMan.GetComponent<NuclearReactorComponent>(reactor).Temperature = 2100f;
            Assert.That(AttemptMeltdown(entMan, reactor), Is.False, "The reactor melted down as soon as it reached the meltdown temperature.");
            SetCriticalElapsed(entMan, timing, reactor, 0.5);
        });
        await RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(AttemptMeltdown(entMan, reactor), Is.False, "The reactor melted down before the end of the critical phase.");
            Assert.That(entMan.GetComponent<ReactorMeltdownVisualsComponent>(reactor).Danger, Is.EqualTo(0.75f).Within(0.051f));
        });

        await server.WaitPost(() => entMan.GetComponent<NuclearReactorComponent>(reactor).Temperature = 1900f);
        await RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<ReactorMeltdownEffectsComponent>(reactor).CriticalStart, Is.Null, "Cooling the reactor down didn't end the critical phase.");

            entMan.GetComponent<NuclearReactorComponent>(reactor).Temperature = 2100f;
            Assert.That(AttemptMeltdown(entMan, reactor), Is.False, "The critical phase didn't start over after the reactor cooled down.");
            SetCriticalElapsed(entMan, timing, reactor, 1);
            Assert.That(AttemptMeltdown(entMan, reactor), Is.True, "The reactor didn't melt down at the end of the critical phase.");
        });
    }

    private static bool AttemptMeltdown(IEntityManager entMan, EntityUid reactor)
    {
        var attempt = new ReactorMeltdownAttemptEvent();
        entMan.EventBus.RaiseLocalEvent(reactor, ref attempt);
        return !attempt.Cancelled;
    }

    private static void SetCriticalElapsed(IEntityManager entMan, IGameTiming timing, EntityUid reactor, double fraction)
    {
        var effects = entMan.GetComponent<ReactorMeltdownEffectsComponent>(reactor);
#pragma warning disable RA0002
        effects.CriticalStart = timing.CurTime - effects.CriticalDuration * fraction;
#pragma warning restore RA0002
    }

    [Test]
    public async Task RumbleStopsWhenTheBuildUpEnds()
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var reactor = await SpawnReactor();
        var session = Pair.Player!;
        var stream = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var listener = entMan.SpawnEntity(null, entMan.GetComponent<TransformComponent>(reactor).Coordinates.Offset(new Vector2(4, 0)));
            server.PlayerMan.SetAttachedEntity(session, listener);
            entMan.GetComponent<NuclearReactorComponent>(reactor).Temperature = 1875f;
        });
        await RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            var streams = entMan.GetComponent<ReactorMeltdownEffectsComponent>(reactor).RumbleStreams;
            Assert.That(streams.TryGetValue(session, out stream), "The rumble didn't start during the build-up.");
        });

        await server.WaitPost(() => entMan.GetComponent<NuclearReactorComponent>(reactor).Temperature = 1600f);
        await RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<ReactorMeltdownEffectsComponent>(reactor).RumbleStreams, Is.Empty);
            Assert.That(entMan.EntityExists(stream), Is.False, "The rumble kept playing after the reactor cooled down.");
        });

        await server.WaitPost(() => entMan.GetComponent<NuclearReactorComponent>(reactor).Temperature = 1875f);
        await RunTicksSync(10);
        await server.WaitPost(() =>
        {
            var streams = entMan.GetComponent<ReactorMeltdownEffectsComponent>(reactor).RumbleStreams;
            Assert.That(streams.TryGetValue(session, out stream));
            entMan.GetComponent<NuclearReactorComponent>(reactor).Melted = true;
            var ev = new ReactorMeltdownEvent();
            entMan.EventBus.RaiseLocalEvent(reactor, ref ev);
        });
        await RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<ReactorMeltdownEffectsComponent>(reactor).RumbleStreams, Is.Empty);
            Assert.That(entMan.EntityExists(stream), Is.False, "The rumble kept playing after the meltdown.");
        });
    }

    [Test]
    [TestCase("NuclearReactorSmall", "NuclearReactorNormal")]
    [TestCase("NuclearReactorSmallSalvage", "NuclearReactorNormalSalvage")]
    public async Task SmallReactorMeltdownIsWeaker(string small, string big)
    {
        var server = Pair.Server;
        var factory = server.EntMan.ComponentFactory;

        await server.WaitAssertion(() =>
        {
            var smallProto = server.ProtoMan.Index<EntityPrototype>(small);
            var bigProto = server.ProtoMan.Index<EntityPrototype>(big);

            Assert.That(smallProto.TryGetComponent<NuclearReactorComponent>(out var smallReactor, factory));
            Assert.That(bigProto.TryGetComponent<NuclearReactorComponent>(out var bigReactor, factory));
            Assert.That(smallProto.TryGetComponent<ReactorMeltdownEffectsComponent>(out var smallEffects, factory));
            Assert.That(bigProto.TryGetComponent<ReactorMeltdownEffectsComponent>(out var bigEffects, factory));
            Assert.That(smallProto.TryGetComponent<ReactorMeltdownVisualsComponent>(out var smallVisuals, factory));
            Assert.That(bigProto.TryGetComponent<ReactorMeltdownVisualsComponent>(out var bigVisuals, factory));

            Assert.Multiple(() =>
            {
                // The small reactor's own fields survive merging with the second parent.
                Assert.That(smallReactor!.ReactorGridWidth, Is.EqualTo(5));
                Assert.That(smallReactor.MeltdownExplosionIntensityPerBadness, Is.LessThan(bigReactor!.MeltdownExplosionIntensityPerBadness));
                Assert.That(smallReactor.MeltdownExplosionMaxTileIntensity, Is.LessThan(bigReactor.MeltdownExplosionMaxTileIntensity));
                Assert.That(smallReactor.MeltdownMaxDebris, Is.LessThan(bigReactor.MeltdownMaxDebris));
                Assert.That(smallReactor.MeltdownMaxTritium, Is.LessThan(bigReactor.MeltdownMaxTritium));
                Assert.That(smallEffects!.ShockwaveRadius, Is.LessThan(bigEffects!.ShockwaveRadius));
                Assert.That(smallEffects.MaxCorium, Is.LessThan(bigEffects.MaxCorium));
                Assert.That(smallEffects.InitialCorium, Is.LessThan(bigEffects.InitialCorium));
                Assert.That(smallEffects.CriticalDuration, Is.LessThan(bigEffects.CriticalDuration));
                Assert.That(smallEffects.SmokeColumnPrototype, Is.Not.EqualTo(bigEffects.SmokeColumnPrototype));
                Assert.That(smallVisuals!.MeltdownGlowRadius, Is.LessThan(bigVisuals!.MeltdownGlowRadius));
            });
        });
    }

    [Test]
    public async Task MeltdownSpawnsSmokeAndCorium()
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var reactor = await SpawnReactor();

        await server.WaitPost(() =>
        {
            entMan.GetComponent<NuclearReactorComponent>(reactor).Melted = true;
            var ev = new ReactorMeltdownEvent();
            entMan.EventBus.RaiseLocalEvent(reactor, ref ev);
        });
        await RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var visuals = entMan.GetComponent<ReactorMeltdownVisualsComponent>(reactor);
            Assert.That(visuals.MeltdownTime, Is.Not.Null);
            Assert.That(entMan.EntityExists(visuals.SmokeColumn), "The smoke column wasn't spawned.");
        });

        await Pair.RunSeconds(60);

        await server.WaitAssertion(() =>
        {
            var effects = entMan.GetComponent<ReactorMeltdownEffectsComponent>(reactor);
            Assert.That(effects.Corium, Has.Count.GreaterThan(effects.InitialCorium), "The corium didn't pour out of the reactor and spread.");

            var lookup = entMan.System<EntityLookupSystem>();
            var reactorBox = lookup.GetWorldAABB(reactor);
            var transform = entMan.System<SharedTransformSystem>();

            foreach (var corium in effects.Corium)
            {
                var xform = entMan.GetComponent<TransformComponent>(corium);
                Assert.That(xform.Anchored, $"{entMan.ToPrettyString(corium)} isn't anchored.");
                Assert.That(reactorBox.Contains(transform.GetWorldPosition(xform)), Is.False,
                    $"{entMan.ToPrettyString(corium)} spawned under the reactor.");
                Assert.That(entMan.GetComponent<CoriumComponent>(corium).Stage, Is.EqualTo(CoriumStage.Molten));
                Assert.That(entMan.GetComponent<RadiationSourceComponent>(corium).Intensity, Is.GreaterThan(0f));
            }
        });
    }
}
