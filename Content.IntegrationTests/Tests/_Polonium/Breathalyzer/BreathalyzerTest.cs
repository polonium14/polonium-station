using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Polonium.Breathalyzer;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Cuffs;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._Polonium.Breathalyzer;

[TestOf(typeof(BreathalyzerSystem))]
public sealed class BreathalyzerTest : InteractionTest
{
    private const string BreathalyzerProto = "Breathalyzer";
    private const string HandcuffsProto = "Handcuffs";
    private const string HumanProto = "MobHuman";
    private const string Ethanol = "Ethanol";
    private const string ReportProto = "BreathalyzerReportPaper";
    private const int TestsPerMediumCell = 17;

    protected override string PlayerPrototype => HumanProto;

    private async Task AddEthanol(EntityUid uid, float amount)
    {
        await Server.WaitPost(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            var bloodstream = SEntMan.GetComponent<BloodstreamComponent>(uid);
            Assert.That(solutions.TryGetSolution(uid, bloodstream.MetabolitesSolutionName, out var solution, out _));
            Assert.That(solutions.TryAddReagent(solution!.Value, Ethanol, amount, out _));
        });
    }

    private async Task<EntityUid> SpawnAdjacentHuman()
    {
        var coords = Transform.WithEntityId(MapData.GridCoords.Offset(new Vector2(1.2f, 0.5f)), MapData.MapUid);
        await SpawnTarget(HumanProto, SEntMan.GetNetCoordinates(coords));
        return STarget!.Value;
    }

    private async Task<BreathalyzerComponent> FinishTest(BreathalyzerComponent comp)
    {
        Assert.That(comp.State, Is.EqualTo(BreathalyzerState.Blowing));

        await AwaitDoAfters();
        Assert.That(comp.State, Is.EqualTo(BreathalyzerState.Analyzing));

        await RunTicks((int) (((float) comp.AnalysisDuration.TotalSeconds + 0.5f) / TickPeriod));
        Assert.That(comp.State, Is.EqualTo(BreathalyzerState.Result));
        return comp;
    }

    private async Task<Entity<BreathalyzerComponent>> BlowSelf()
    {
        var device = ToServer(await PlaceInHands(BreathalyzerProto));

        await Server.WaitPost(() => InteractSys.UseInHandInteraction(SPlayer, device));
        await RunTicks(1);

        return (device, await FinishTest(SEntMan.GetComponent<BreathalyzerComponent>(device)));
    }

    private float GetCharge(EntityUid device)
    {
        Assert.That(SEntMan.System<PowerCellSystem>().TryGetBatteryFromSlot(device, out var battery));
        return SEntMan.System<SharedBatterySystem>().GetCharge(battery!.Value.Owner).Charge;
    }

    [Test]
    public async Task SoberReading()
    {
        var comp = (await BlowSelf()).Comp;
        var system = SEntMan.System<BreathalyzerSystem>();

        Assert.That(comp.LastResult, Is.EqualTo(0f));
        Assert.That(system.GetLevel(comp, comp.LastResult!.Value), Is.EqualTo(comp.Levels[0]));
    }

    [Test]
    public async Task DrunkReading()
    {
        await AddEthanol(SPlayer, 10);

        var comp = (await BlowSelf()).Comp;

        Assert.That(comp.LastResult, Is.GreaterThan(1f));
        Assert.That(SEntMan.System<BreathalyzerSystem>().GetLevel(comp, comp.LastResult!.Value)!.Threshold, Is.GreaterThanOrEqualTo(0.5f));
    }

    [Test]
    public async Task ForcedTestOnCuffedTarget()
    {
        var target = await SpawnAdjacentHuman();
        await AddEthanol(target, 10);

        await Server.WaitPost(() =>
        {
            var cuffs = SEntMan.SpawnAtPosition(HandcuffsProto, SEntMan.GetComponent<TransformComponent>(target).Coordinates);
            Assert.That(SEntMan.System<SharedCuffableSystem>().TryAddNewCuffs(target, SPlayer, cuffs));
        });

        var device = ToServer(await PlaceInHands(BreathalyzerProto));
        await Interact(awaitDoAfters: false);

        var comp = await FinishTest(SEntMan.GetComponent<BreathalyzerComponent>(device));
        Assert.That(comp.LastResult, Is.GreaterThan(1f));
    }

    [Test]
    public async Task NoForcedTestOnFreeTarget()
    {
        await SpawnAdjacentHuman();

        var device = ToServer(await PlaceInHands(BreathalyzerProto));
        await Interact(awaitDoAfters: false);

        var comp = SEntMan.GetComponent<BreathalyzerComponent>(device);
        Assert.That(comp.State, Is.EqualTo(BreathalyzerState.Idle));
        Assert.That(ActiveDoAfters, Is.Empty);
    }

    [Test]
    public async Task MediumCellLastsSeventeenTests()
    {
        var device = ToServer(await PlaceInHands(BreathalyzerProto));
        var comp = SEntMan.GetComponent<BreathalyzerComponent>(device);

        Assert.That(SEntMan.System<PowerCellSystem>().GetRemainingUses(device, comp.ChargePerTest), Is.EqualTo(TestsPerMediumCell));
        var before = GetCharge(device);

        await Server.WaitPost(() => InteractSys.UseInHandInteraction(SPlayer, device));
        await RunTicks(1);
        await FinishTest(comp);

        Assert.That(GetCharge(device), Is.EqualTo(before - comp.ChargePerTest).Within(0.01f));
    }

    [Test]
    public async Task PrintReport()
    {
        var device = await BlowSelf();
        var before = GetCharge(device);

        await SendBui(BreathalyzerUiKey.Key, new BreathalyzerPrintMessage(), SEntMan.GetNetEntity(device.Owner));

        var report = SEntMan.System<SharedHandsSystem>()
            .EnumerateHeld(SPlayer)
            .FirstOrDefault(held => SEntMan.GetComponent<MetaDataComponent>(held).EntityPrototype?.ID == ReportProto);

        Assert.That(report, Is.Not.EqualTo(default(EntityUid)));
        var result = device.Comp.LastResult!.Value;
        var culture = Server.ResolveDependency<ILocalizationManager>().DefaultCulture;
        Assert.That(SEntMan.GetComponent<PaperComponent>(report).Content, Does.Contain(result.ToString("0.00", culture)));
        Assert.That(device.Comp.PrintReadyAt, Is.GreaterThan(STiming.CurTime));
        Assert.That(GetCharge(device), Is.EqualTo(before - device.Comp.ChargePerPrint).Within(0.01f));
    }
}
