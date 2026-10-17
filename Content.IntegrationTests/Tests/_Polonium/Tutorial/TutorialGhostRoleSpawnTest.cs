using Content.IntegrationTests.Fixtures;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Ghost.Roles.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Tutorial;

/// <summary>
/// A mob with a ghost role spawned mid-tutorial used to lose those components while it was still
/// starting up, which trips the entity lifecycle asserts.
/// </summary>
public sealed class TutorialGhostRoleSpawnTest : GameTest
{
    [Test]
    public async Task SlimeSpawnsOnTutorialMap()
    {
        var pair = Pair;
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entManager = server.ResolveDependency<IEntityManager>();

        EntityUid slime = default;
        await server.WaitAssertion(() =>
        {
            entManager.AddComponent<TutorialMapComponent>(testMap.MapUid);
            slime = entManager.SpawnEntity("TutorialSlime", testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entManager.HasComponent<GhostRoleComponent>(slime), Is.False);
            Assert.That(entManager.HasComponent<GhostTakeoverAvailableComponent>(slime), Is.False);
        });
    }
}
