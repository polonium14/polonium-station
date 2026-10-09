using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Polonium.Morph;
using Content.Shared.Actions;
using Content.Shared.Polymorph.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Morph;

[TestOf(typeof(SharedMorphSystem))]
public sealed class MorphTest : InteractionTest
{
    protected override string PlayerPrototype => "MobMorph";

    private async Task PerformDisguise(NetEntity target)
    {
        var morph = SEntMan.GetComponent<MorphComponent>(SPlayer);
        var action = SEntMan.GetNetEntity(morph.DisguiseActionEntity!.Value);
        await Client.WaitPost(() => CEntMan.RaisePredictiveEvent(new RequestPerformActionEvent(action, target)));
        await RunTicks(15);
    }

    [Test]
    public async Task DisguiseAsItem()
    {
        var item = await SpawnTarget("Crowbar");
        await PerformDisguise(item);

        Assert.That(SEntMan.HasComponent<ChameleonDisguisedComponent>(SPlayer), "Morph did not disguise as an item.");
        Assert.That(SEntMan.GetComponent<MorphComponent>(SPlayer).Disguised);
    }

    [Test]
    public async Task DisguiseAsStructure()
    {
        var chair = await SpawnTarget("Chair");
        await PerformDisguise(chair);

        Assert.That(SEntMan.HasComponent<ChameleonDisguisedComponent>(SPlayer), "Morph did not disguise as a chair.");
    }
}
