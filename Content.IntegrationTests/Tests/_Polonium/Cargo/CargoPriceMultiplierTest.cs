using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Polonium.Cargo;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.CCVar;

namespace Content.IntegrationTests.Tests._Polonium.Cargo;

public sealed class CargoPriceMultiplierTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CargoPriceMultiplierTestStation
  components:
  - type: StationBankAccount
";

    [Test]
    public async Task MultipliersFixedWhenStationCreated()
    {
        var server = Pair.Server;
        var multiplierSystem = server.System<CargoPriceMultiplierSystem>();

        await OverrideCVar(Side.Server, CCVars.CargoBuyPriceMultiplier, 2f);
        await OverrideCVar(Side.Server, CCVars.CargoSellPriceMultiplier, 0.5f);

        var station = await Spawn("CargoPriceMultiplierTestStation");

        await server.WaitAssertion(() =>
        {
            var product = SProtoMan.EnumeratePrototypes<CargoProductPrototype>().First(p => p.Cost > 0);
            var multiplier = SComp<CargoPriceMultiplierComponent>(station);

            Assert.Multiple(() =>
            {
                Assert.That(multiplier.BuyMultiplier, Is.EqualTo(2f));
                Assert.That(multiplier.SellMultiplier, Is.EqualTo(0.5f));
                Assert.That(multiplierSystem.GetBuyCost(station, product), Is.EqualTo(product.Cost * 2));
                Assert.That(multiplierSystem.GetSellPrice(station, 100), Is.EqualTo(50));
            });
        });

        // A mid-round change must not affect a station that already exists.
        await OverrideCVar(Side.Server, CCVars.CargoBuyPriceMultiplier, 3f);
        await OverrideCVar(Side.Server, CCVars.CargoSellPriceMultiplier, 3f);

        await server.WaitAssertion(() =>
        {
            var multiplier = SComp<CargoPriceMultiplierComponent>(station);

            Assert.Multiple(() =>
            {
                Assert.That(multiplier.BuyMultiplier, Is.EqualTo(2f));
                Assert.That(multiplier.SellMultiplier, Is.EqualTo(0.5f));
            });
        });
    }

    [Test]
    public async Task NegativeMultipliersClamped()
    {
        await OverrideCVar(Side.Server, CCVars.CargoBuyPriceMultiplier, -1f);
        await OverrideCVar(Side.Server, CCVars.CargoSellPriceMultiplier, -1f);

        var station = await Spawn("CargoPriceMultiplierTestStation");

        await Pair.Server.WaitAssertion(() =>
        {
            var multiplier = SComp<CargoPriceMultiplierComponent>(station);

            Assert.Multiple(() =>
            {
                Assert.That(multiplier.BuyMultiplier, Is.Zero);
                Assert.That(multiplier.SellMultiplier, Is.Zero);
            });
        });
    }
}
