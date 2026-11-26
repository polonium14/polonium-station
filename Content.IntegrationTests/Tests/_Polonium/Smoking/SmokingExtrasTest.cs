using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests._Polonium.Medical;
using Content.Server.Nutrition.EntitySystems;
using Content.Server._Polonium.Smoking;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Nutrition.Components;
using Content.Shared.Smoking;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Polonium.Smoking;

[TestFixture]
[TestOf(typeof(SmokingExtrasSystem))]
public sealed class SmokingExtrasTest : GameTest
{
    private const string Cigarette = "Cigarette";

    private async Task<EntityUid> SpawnCigarette(string reagent = "", int amount = 0)
    {
        // The test map has no air, which is what the oxygen checks need.
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        var cigarette = EntityUid.Invalid;

        await Server.WaitPost(() =>
        {
            cigarette = SEntMan.Spawn(Cigarette, coords);
            if (reagent == "")
                return;

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            var smokable = SEntMan.GetComponent<SmokableComponent>(cigarette);
            Assert.That(solutions.TryGetSolution(cigarette, smokable.Solution, out var solution, out _));
            Assert.That(solutions.TryAddReagent(solution!.Value, reagent, FixedPoint2.New(amount)));
        });

        return cigarette;
    }

    private async Task Light(EntityUid cigarette)
    {
        await Server.WaitPost(() => SEntMan.System<SmokingSystem>().SetSmokableState(cigarette, SmokableState.Lit));
    }

    private bool LightOn(EntityUid cigarette)
    {
        return SEntMan.System<SharedPointLightSystem>().TryGetLight(cigarette, out var light) && light.Enabled;
    }

    private SmokableState State(EntityUid cigarette)
    {
        return SEntMan.GetComponent<SmokableComponent>(cigarette).State;
    }

    [Test]
    public async Task GoesOutWithoutOxygen()
    {
        var cigarette = await SpawnCigarette();
        await Light(cigarette);
        await Pair.RunSeconds(2);

        Assert.That(State(cigarette), Is.EqualTo(SmokableState.Unlit), "A cigarette in vacuum should go out.");
        Assert.That(LightOn(cigarette), Is.False, "A cigarette that went out should stop glowing.");
    }

    [Test]
    public async Task OxygenInsideKeepsItLit()
    {
        var cigarette = await SpawnCigarette("Oxygen", 5);
        await Light(cigarette);
        await Pair.RunSeconds(2);

        Assert.That(State(cigarette), Is.EqualTo(SmokableState.Lit), "Oxygen in the cigarette should keep it lit in vacuum.");
        Assert.That(LightOn(cigarette), "A lit cigarette should glow.");
    }

    [Test]
    public async Task RiggedCigaretteExplodes()
    {
        var cigarette = await SpawnCigarette("Plasma", 5);
        await Light(cigarette);
        await Pair.RunTicksSync(5);

        Assert.That(SEntMan.Deleted(cigarette), "A cigarette with plasma should blow up when lit.");
    }

    /// <summary>
    /// A smoker in breathable air with an unlit cigarette in the mouth, and a lit one in a second smoker's hand.
    /// </summary>
    private async Task<(EntityUid Smoker, EntityUid Worn, EntityUid Helper, EntityUid Lighter)> SpawnSmokersInAir()
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        EntityUid smoker = default, worn = default, helper = default, lighter = default;

        await Server.WaitPost(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            smoker = SEntMan.Spawn("MobHuman", coords);
            helper = SEntMan.Spawn("MobHuman", coords);
            worn = SEntMan.Spawn(Cigarette, coords);
            lighter = SEntMan.Spawn(Cigarette, coords);

            Assert.That(SEntMan.System<InventorySystem>().TryEquip(smoker, worn, "mask", silent: true, force: true));
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(helper, lighter));
            SEntMan.System<SmokingSystem>().SetSmokableState(lighter, SmokableState.Lit);
        });
        await Pair.RunTicksSync(5);

        return (smoker, worn, helper, lighter);
    }

    [Test]
    public async Task ClickWithLitCigaretteLightsTheWornOne()
    {
        var (smoker, worn, helper, lighter) = await SpawnSmokersInAir();

        await Server.WaitPost(() => SEntMan.System<SharedInteractionSystem>()
            .InteractUsing(helper, lighter, smoker, SEntMan.GetComponent<TransformComponent>(smoker).Coordinates));
        await Pair.RunTicksSync(5);

        Assert.That(State(worn), Is.EqualTo(SmokableState.Lit), "Clicking someone with a lit cigarette should light the one in their mouth.");
    }
}
