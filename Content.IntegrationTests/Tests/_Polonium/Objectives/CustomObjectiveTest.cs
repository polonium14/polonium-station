using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Objectives;
using Content.Shared._Polonium.Objectives;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._Polonium.Objectives;

public sealed class CustomObjectiveTest : GameTest
{
    private const string DummyUsername = "CustomObjectiveTestUser";
    private const string Title = "Burn the cargo bay";
    private const string Description = "Start a fire in cargo and leave a calling card.";
    private const string Issuer = "CentralCommand";

    public override PoolSettings PoolSettings => new()
    {
        Connected = false
    };

    [Test]
    public async Task AddSetStatusRemoveTest()
    {
        var server = Pair.Server;
        var playerMan = server.ResolveDependency<ISharedPlayerManager>();
        var mindSys = server.System<SharedMindSystem>();
        var customObjectives = server.System<CustomObjectiveSystem>();
        var objectivesSys = server.System<SharedObjectivesSystem>();

        await server.AddDummySession(DummyUsername);
        await server.WaitRunTicks(5);

        var session = playerMan.Sessions.Single();

        Entity<MindComponent>? mind = null;
        await server.WaitPost(() => mind = mindSys.CreateMind(session.UserId));
        Assert.That(mind, Is.Not.Null);

        var mindEnt = mind!.Value;
        EntityUid first = default;
        EntityUid second = default;

        await server.WaitAssertion(() =>
        {
            Assert.That(customObjectives.TryAdd(mindEnt, Title, Description, Issuer, out first), Is.True);
            Assert.That(customObjectives.TryAdd(mindEnt, Title, Description, Issuer, out second), Is.True,
                "Custom objectives must not be unique per prototype.");

            Assert.That(mindEnt.Comp.Objectives, Has.Count.EqualTo(2));
            Assert.That(server.EntMan.GetComponent<MetaDataComponent>(first).EntityName, Is.EqualTo(Title));
            Assert.That(server.EntMan.GetComponent<MetaDataComponent>(first).EntityDescription, Is.EqualTo(Description));
            Assert.That(server.EntMan.GetComponent<ObjectiveComponent>(first).Issuer.Id, Is.EqualTo(Issuer));
            Assert.That(objectivesSys.GetProgress(first, mindEnt), Is.EqualTo(0f));

            Assert.That(customObjectives.TrySetStatus(mindEnt, first, CustomObjectiveStatus.Partial), Is.True);
            Assert.That(objectivesSys.GetProgress(first, mindEnt), Is.EqualTo(0.5f));
            Assert.That(objectivesSys.IsCompleted(first, mindEnt), Is.False);

            Assert.That(customObjectives.TrySetStatus(mindEnt, first, CustomObjectiveStatus.Completed), Is.True);
            Assert.That(objectivesSys.IsCompleted(first, mindEnt), Is.True);
            Assert.That(objectivesSys.IsCompleted(second, mindEnt), Is.False);

            Assert.That(mindSys.TryRemoveObjective(mindEnt, mindEnt.Comp, mindEnt.Comp.Objectives.IndexOf(first)), Is.True);
            Assert.That(mindEnt.Comp.Objectives, Has.Count.EqualTo(1));
            Assert.That(server.EntMan.EntityExists(first), Is.False);
            Assert.That(customObjectives.TrySetStatus(mindEnt, first, CustomObjectiveStatus.Completed), Is.False,
                "A removed objective must not accept a status.");
        });
    }
}
