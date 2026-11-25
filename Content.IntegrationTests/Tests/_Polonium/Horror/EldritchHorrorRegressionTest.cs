#nullable enable
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Horror;
using Content.Shared._Polonium.Horror.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Doors.Components;
using Content.Shared.Emoting;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Polonium.Horror;

/// <summary>
/// Regresje z przeglądu: cele sługi z transportera, emotki z głowy i przeciskanie przez śluzę.
/// </summary>
[TestOf(typeof(EldritchHorrorSystem))]
public sealed class EldritchHorrorRegressionTest : GameTest
{
    private async Task<EntityCoordinates> Setup()
    {
        var map = await Pair.CreateTestMap();
        return map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Rola ducha przenosi umysł przed nadaniem roli, więc cele muszą wejść dopiero razem z rolą.
    /// </summary>
    [Test]
    public async Task ServantGetsObjectivesOnceTheRoleLands()
    {
        var coords = await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            var horror = SSpawnAtPosition("MobEldritchHorror", coords);
            var servant = SEntMan.EnsureComponent<HorrorServantComponent>(horror);
            servant.PendingObjectives = new() { "HorrorServeObjective", "HorrorSurviveObjective" };

            var mindSys = SEntMan.System<SharedMindSystem>();
            var mind = mindSys.CreateMind(null, "sługa");
            mindSys.TransferTo(mind, horror);
            Assert.That(mind.Comp.Objectives, Is.Empty, "Cele przed nadaniem roli");

            SEntMan.System<SharedRoleSystem>().MindAddRole(mind, "MindRoleEldritchHorror", mind.Comp, true);
            Assert.That(mind.Comp.Objectives, Has.Count.EqualTo(2), "Sługa nie dostał celów po nadaniu roli");
            Assert.That(servant.PendingObjectives, Is.Empty);
        });
    }

    [Test]
    public async Task HorrorInHeadCantEmote()
    {
        var coords = await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            var horror = SSpawnAtPosition("MobEldritchHorror", coords);
            var human = SSpawnAtPosition("MobHuman", coords);

            var free = new EmoteAttemptEvent(horror);
            SEntMan.EventBus.RaiseLocalEvent(horror, free);
            Assert.That(free.Cancelled, Is.False, "Wolny horror nie powinien mieć blokady od systemu horroru");

            var system = SEntMan.System<EldritchHorrorSystem>();
            Assert.That(system.Infest((horror, SComp<EldritchHorrorComponent>(horror)), human), Is.True);

            var inHead = new EmoteAttemptEvent(horror);
            SEntMan.EventBus.RaiseLocalEvent(horror, inHead);
            Assert.That(inHead.Cancelled, Is.True, "Horror w głowie robi emotki na głos");
            Assert.That(SEntMan.System<ActionBlockerSystem>().CanEmote(horror), Is.False);
        });
    }

    [Test]
    public async Task SqueezeComesOutOnTheFarSide()
    {
        var coords = await Setup();
        EntityUid horror = default;
        EntityUid door = default;

        await Pair.Server.WaitPost(() =>
        {
            door = SSpawnAtPosition("Airlock", coords.Offset(new Vector2(1f, 0f)));
            horror = SSpawnAtPosition("MobEldritchHorror", coords);
            SComp<EldritchHorrorComponent>(horror).SqueezeTime = TimeSpan.FromSeconds(0.5);
            Assert.That(SComp<DoorComponent>(door).State, Is.EqualTo(DoorState.Closed));

            var attempt = new AttackAttemptEvent(horror, door, (horror, SComp<MeleeWeaponComponent>(horror)));
            SEntMan.EventBus.RaiseLocalEvent(horror, attempt);
            Assert.That(attempt.Cancelled, Is.True, "Ugryzienie śluzy nie zostało zamienione na przeciskanie");
        });

        await RunSeconds(2);

        await Pair.Server.WaitAssertion(() =>
        {
            var xform = SEntMan.System<SharedTransformSystem>();
            var x = xform.GetWorldPosition(horror).X;
            var doorX = xform.GetWorldPosition(door).X;
            Assert.That(x, Is.GreaterThan(doorX + 0.5f), "Horror nie wyszedł po drugiej stronie śluzy");
        });
    }
}
