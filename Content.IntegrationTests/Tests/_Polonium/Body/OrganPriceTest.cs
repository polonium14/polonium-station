using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Cargo.Systems;
using Content.Shared.Body;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Body;

/// <summary>
/// An organ sells only after it has been in a body, so the biofab is not a money printer.
/// </summary>
[TestFixture]
public sealed class OrganPriceTest : GameTest
{
    [Test]
    public async Task OnlyHarvestedOrgansHaveAPrice()
    {
        var map = await Pair.CreateTestMap();
        var pricing = SEntMan.System<PricingSystem>();

        await Server.WaitAssertion(() =>
        {
            var printed = SEntMan.SpawnAtPosition("OrganHumanHeart", map.GridCoords);
            Assert.That(pricing.GetPrice(printed), Is.Zero, "Straight from the biofab.");

            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var organs = SEntMan.GetComponent<BodyComponent>(body).Organs!;
            var whole = pricing.GetPrice(body);
            var heart = organs.ContainedEntities.Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == "Heart");
            var arm = organs.ContainedEntities.Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == "ArmLeft");

            var containers = SEntMan.System<SharedContainerSystem>();
            Assert.That(containers.Remove(heart, organs));
            Assert.That(containers.Remove(arm, organs));

            Assert.Multiple(() =>
            {
                Assert.That(pricing.GetPrice(heart), Is.EqualTo(50));
                Assert.That(pricing.GetPrice(arm, includeContents: false), Is.EqualTo(100));
                Assert.That(pricing.GetPrice(body), Is.EqualTo(whole), "Organs inside a body add nothing to it.");
            });
        });
    }
}
