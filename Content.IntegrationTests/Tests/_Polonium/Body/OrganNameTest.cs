using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Body;

/// <summary>
/// The health analyzer lists organs by name. Generated locale entries used to inherit the name
/// from an abstract, nameless parent (the species "Visual" organs), leaving eyes blank.
/// </summary>
[TestFixture]
public sealed class OrganNameTest : GameTest
{
    [Test]
    public async Task EverySpawnedOrganHasAName()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var blank = new System.Collections.Generic.List<string>();
            foreach (var species in server.ProtoMan.EnumeratePrototypes<Content.Shared.Humanoid.Prototypes.SpeciesPrototype>().Where(s => s.RoundStart))
            {
                var mob = entMan.SpawnEntity(species.Prototype, map.GridCoords);
                foreach (var organ in entMan.GetComponent<BodyComponent>(mob).Organs!.ContainedEntities)
                {
                    if (string.IsNullOrWhiteSpace(entMan.GetComponent<MetaDataComponent>(organ).EntityName))
                        blank.Add($"{species.ID}:{entMan.GetComponent<MetaDataComponent>(organ).EntityPrototype?.ID}");
                }
                entMan.DeleteEntity(mob);
            }

            Assert.That(blank, Is.Empty, $"Spawned organs without a name: {string.Join(", ", blank)}");
        });
    }
}
