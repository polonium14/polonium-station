using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Polonium.Morph;
using Content.Server.Objectives.Components;
using Content.Shared._Polonium.Morph;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Devour.Components;
using Content.Shared.Mind;
using Content.Shared.Movement.Components;
using Content.Shared.Polymorph.Components;
using Content.Shared.Roles;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
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

        await Client.WaitAssertion(() =>
        {
            var disguised = CEntMan.GetComponent<ChameleonDisguisedComponent>(CPlayer);
            var disguise = disguised.Disguise;
            Assert.That(CEntMan.EntityExists(disguise), "Client has no disguise entity.");
            var sprite = CEntMan.GetComponent<SpriteComponent>(disguise);
            var layers = sprite.AllLayers.ToList();
            TestContext.Out.WriteLine($"disguise {CEntMan.ToPrettyString(disguise)} visible={sprite.Visible} layers={layers.Count} rsi={sprite.BaseRSI?.Path} drawDepth={sprite.DrawDepth}");
            foreach (var layer in layers)
                TestContext.Out.WriteLine($"  layer visible={layer.Visible} rsi={layer.ActualRsi?.Path} state={layer.RsiState.Name}");
            var itemSprite = CEntMan.GetComponent<SpriteComponent>(CTarget!.Value);
            TestContext.Out.WriteLine($"item layers={itemSprite.AllLayers.Count()} rsi={itemSprite.BaseRSI?.Path}");
            Assert.That(sprite.Visible, "Disguise sprite is not visible.");
            Assert.That(layers.Any(l => l.Visible && l.ActualRsi != null), "Disguise sprite has no visible layers.");
        });
    }

    [Test]
    public async Task MorphCannotSpeak()
    {
        await Server.WaitAssertion(() =>
        {
            var blocker = SEntMan.System<ActionBlockerSystem>();
            Assert.That(blocker.CanSpeak(SPlayer), Is.False, "Morph can speak.");
        });
    }

    [Test]
    public async Task AssumeDevouredForm()
    {
        var body = await SpawnTarget("MobHuman");
        var sBody = ToServer(body);

        await Server.WaitPost(() =>
        {
            var container = SEntMan.System<SharedContainerSystem>();
            var stomach = container.EnsureContainer<Container>(SPlayer, DevourerComponent.StomachContainerId);
            container.Insert(sBody, stomach);
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, new MorphStomachSelectMessage(body, MorphStomachAction.AssumeForm));
        });
        await RunTicks(15);

        var morph = SEntMan.GetComponent<MorphComponent>(SPlayer);
        Assert.That(morph.HumanoidForm, Is.EqualTo(sBody), "Morph did not take the devoured form.");
        Assert.That(SEntMan.HasComponent<ChameleonDisguisedComponent>(SPlayer));
        Assert.That(SEntMan.GetComponent<MovementSpeedModifierComponent>(SPlayer).WalkSpeedModifier, Is.EqualTo(1f).Within(0.01f),
            "Humanoid form should not get the disguise speed bonus.");

        await Client.WaitAssertion(() =>
        {
            var disguise = CEntMan.GetComponent<ChameleonDisguisedComponent>(CPlayer).Disguise;
            var sprite = CEntMan.GetComponent<SpriteComponent>(disguise);
            Assert.That(sprite.AllLayers.Any(l => l.Visible && l.ActualRsi != null), "Humanoid disguise is invisible.");
            Assert.That(sprite.Rotation.Theta, Is.EqualTo(0).Within(0.01), "Humanoid disguise is lying down.");
        });

        // the form is gone once the body leaves the stomach
        await Server.WaitPost(() =>
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, new MorphStomachSelectMessage(body, MorphStomachAction.Drop)));
        await RunTicks(15);

        Assert.That(SEntMan.HasComponent<ChameleonDisguisedComponent>(SPlayer), Is.False, "Morph kept the form of a spat out body.");
        Assert.That(morph.HumanoidForm, Is.Null);
    }

    [Test]
    public async Task DevourObjectiveCountsCrewOnce()
    {
        var body = await SpawnTarget("MobHuman");
        var sBody = ToServer(body);
        var mindSys = SEntMan.System<SharedMindSystem>();
        EntityUid objective = default;

        await Server.WaitPost(() =>
        {
            // separate morph, the test player only visits its own
            var morphUid = SEntMan.SpawnEntity("MobMorph", SEntMan.GetCoordinates(PlayerCoords));
            var morphMind = mindSys.CreateMind(null, "morph");
            mindSys.TransferTo(morphMind, morphUid, createGhost: false);
            var (mindId, mind) = morphMind;
            SEntMan.System<SharedRoleSystem>().MindAddRole(mindId, "MindRoleMorph", mind, silent: true);
            Assert.That(mindSys.TryAddObjective(mindId, mind, "MorphDevourObjective"), "Could not give the devour objective.");
            objective = mindSys.EnumerateObjectives<MorphDevourConditionComponent>((mindId, mind)).Single();

            var victimMind = mindSys.CreateMind(null, "victim");
            mindSys.TransferTo(victimMind, sBody, createGhost: false);

            var container = SEntMan.System<SharedContainerSystem>();
            var stomach = container.EnsureContainer<Container>(morphUid, DevourerComponent.StomachContainerId);
            container.Insert(sBody, stomach);
            container.Remove(sBody, stomach);
            container.Insert(sBody, stomach);
        });
        await RunTicks(5);

        Assert.That(SEntMan.GetComponent<CounterConditionComponent>(objective).Count, Is.EqualTo(1),
            "Devouring the same crewmember twice should count once.");
    }

    [Test]
    public async Task DisguiseAsStructure()
    {
        var chair = await SpawnTarget("Chair");
        await PerformDisguise(chair);

        Assert.That(SEntMan.HasComponent<ChameleonDisguisedComponent>(SPlayer), "Morph did not disguise as a chair.");
    }
}
