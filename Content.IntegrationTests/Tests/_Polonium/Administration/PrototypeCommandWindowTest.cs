#nullable enable
using Content.Client._Polonium.Administration.UI;
using Content.IntegrationTests.Fixtures;

namespace Content.IntegrationTests.Tests._Polonium.Administration;

[TestOf(typeof(PrototypeCommandWindow))]
public sealed class PrototypeCommandWindowTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    [Test]
    public async Task ListsPrototypesForAdminButtons()
    {
        await Client.WaitAssertion(() =>
        {
            var alert = new PrototypeCommandWindow("", "alertLevel", "setalertlevel {0}");
            var ert = new PrototypeCommandWindow("", "ertCall", "callert {0}");
            var rules = new PrototypeCommandWindow("", "entity", "addgamerule {0}", "GameRule");

            Assert.Multiple(() =>
            {
                Assert.That(alert.PrototypeOptions.ItemCount, Is.GreaterThanOrEqualTo(3), "alert levels");
                Assert.That(ert.PrototypeOptions.ItemCount, Is.GreaterThanOrEqualTo(10), "ERT presets");
                Assert.That(rules.PrototypeOptions.ItemCount, Is.GreaterThanOrEqualTo(50), "game rules");
                Assert.That(rules.SearchLineEdit.Visible, "the long game rule list has no search field");
            });

            rules.SearchLineEdit.SetText("MeteorSwarmSmall", invokeEvent: true);
            Assert.That(rules.PrototypeOptions.ItemCount, Is.EqualTo(1), "searching did not narrow the list to the one event");

            alert.Dispose();
            ert.Dispose();
            rules.Dispose();
        });
    }
}
