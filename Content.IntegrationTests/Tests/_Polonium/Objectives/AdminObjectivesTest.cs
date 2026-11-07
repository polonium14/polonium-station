using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Objectives;

namespace Content.IntegrationTests.Tests._Polonium.Objectives;

public sealed class AdminObjectivesTest : GameTest
{
    /// <summary>
    /// Objectives whose title is only set on assignment need an admin-objectives-name-ID key,
    /// otherwise the admin objectives window shows their raw prototype ID.
    /// </summary>
    [Test]
    public async Task StandardObjectivesHaveListNames()
    {
        var server = Pair.Server;
        var adminObjectives = server.System<AdminObjectivesSystem>();

        await server.WaitAssertion(() =>
        {
            var objectives = adminObjectives.GetStandardObjectives();
            Assert.That(objectives, Is.Not.Empty);

            var unnamed = objectives
                .Where(o => o.Name == o.Id && !Pair.IsTestEntityPrototype(o.Id))
                .Select(o => o.Id)
                .ToList();

            Assert.That(unnamed, Is.Empty,
                $"Add admin-objectives-name-{string.Join(", ", unnamed)} to Resources/Locale/{{en-US,pl-PL}}/_Polonium/objectives/admin-objectives.ftl");
        });
    }
}
