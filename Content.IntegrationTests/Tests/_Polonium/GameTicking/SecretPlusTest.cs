#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Antag.Components;
using Content.Server.Antag.Selectors;
using Content.Server.GameTicking;
using Content.Server.StationEvents.SecretPlus;
using Content.Shared._Goobstation.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Polonium.GameTicking;

public sealed class SecretPlusTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: weightedRandom
  id: SecretPlusTestPrimary
  weights:
    SecretPlusTestLoudA: 1
    SecretPlusTestLoudB: 1

- type: weightedRandom
  id: SecretPlusTestSupport
  weights:
    SecretPlusTestQuiet: 1

- type: weightedRandom
  id: SecretPlusTestLate
  weights:
    SecretPlusTestLateRule: 1

- type: entity
  id: SecretPlusTestLoudA
  parent: BaseGameRule
  categories: [ GameRules ]
  components:
  - type: GameRule
    chaosScore: 100

- type: entity
  id: SecretPlusTestLoudB
  parent: SecretPlusTestLoudA
  categories: [ GameRules ]

- type: entity
  id: SecretPlusTestQuiet
  parent: SecretPlusTestLoudA
  categories: [ GameRules ]

- type: entity
  id: SecretPlusTestLateRule
  parent: BaseGameRule
  categories: [ GameRules ]
  components:
  - type: AntagSelection
    lateJoinAdditional: true
    antags:
    - !type:LinearAntagCount
      proto: Traitor
      chaosScore: 100
      range:
        min: 1
        max: 4

- type: entity
  id: SecretPlusTestAntags
  parent: BaseGameRule
  categories: [ GameRules ]
  components:
  - type: SecretPlus
    minStartingChaos: 1000
    maxStartingChaos: 1000
    primaryAntagsWeightTable: SecretPlusTestPrimary
    roundStartAntagsWeightTable: SecretPlusTestSupport
    primaryAntagPicks:
      0: 2
    guaranteedPrimaryAntags: true
    primaryAntagChaosShare: 0.5
    exclusiveRules:
      SecretPlusTestLoudA: 10
      SecretPlusTestLoudB: 10

- type: entity
  id: SecretPlusTestNoAntags
  parent: SecretPlusTestAntags
  categories: [ GameRules ]
  components:
  - type: SecretPlus
    noRoundstartAntagsChance: 1

- type: entity
  id: SecretPlusTestLateAntags
  parent: BaseGameRule
  categories: [ GameRules ]
  components:
  - type: SecretPlus
    minStartingChaos: 1000
    maxStartingChaos: 1000
    primaryAntagsWeightTable: SecretPlusTestLate
    roundStartAntagsWeightTable: SecretPlusTestLate
    guaranteedPrimaryAntags: true
    lateAntagsFrom: 0
    lateAntagsUntil: 4

- type: gamePreset
  id: SecretPlusTestLateAntags
  name: Secret Plus Test Late Antags
  description: """"
  showInVote: false
  rules:
  - SecretPlusTestLateAntags

- type: entityTable
  id: SecretPlusTestEvents
  table: !type:AllSelector
    children:
    - id: SecretPlusTestEvent

- type: entityTable
  id: SecretPlusTestGhostAntags
  table: !type:AllSelector
    children:
    - id: SecretPlusTestGhostA
    - id: SecretPlusTestGhostB

- type: entity
  id: SecretPlusTestEvent
  parent: BaseGameRule
  categories: [ GameRules ]
  components:
  - type: StationEvent
    earliestStart: 0
    minimumPlayers: 2
  - type: GameRule
    chaosScore: 10

- type: entity
  id: SecretPlusTestGhostA
  parent: BaseGameRule
  categories: [ GameRules ]
  components:
  - type: StationEvent
    earliestStart: 0
  - type: GameRule
    chaosScore: 10

- type: entity
  id: SecretPlusTestGhostB
  parent: SecretPlusTestGhostA
  categories: [ GameRules ]

- type: entity
  id: SecretPlusTestScheduler
  parent: BaseGameRule
  categories: [ GameRules ]
  components:
  - type: SecretPlus
    minStartingChaos: 100
    maxStartingChaos: 100
    noRoundstartAntags: true
    eventIntervalMin: 0.1
    eventIntervalMax: 0.1
    eventsCountAllPlayers: true
    ghostAntagRules: !type:NestedSelector
      tableId: SecretPlusTestGhostAntags
    ghostAntagLimit:
      0: 2
      2: 1
  - type: SelectedGameRules
    scheduledGameRules: !type:NestedSelector
      tableId: SecretPlusTestEvents

- type: entity
  id: SecretPlusTestSchedulerLiving
  parent: SecretPlusTestScheduler
  categories: [ GameRules ]
  components:
  - type: SecretPlus
    eventsCountAllPlayers: false

- type: entityTable
  id: SecretPlusTestRowEvents
  table: !type:AllSelector
    children:
    - !type:EventSelector
      id: SecretPlusTestStockEvent
      chaosScore: 37
      minimumPlayers: 0
    - !type:EventSelector
      id: SecretPlusTestEvent
      minimumPlayers: 50

- type: entity
  id: SecretPlusTestStockEvent
  parent: BaseGameRule
  categories: [ GameRules ]
  components:
  - type: StationEvent
    earliestStart: 0
    minimumPlayers: 50

- type: entity
  id: SecretPlusTestRowScheduler
  parent: BaseGameRule
  categories: [ GameRules ]
  components:
  - type: SecretPlus
    minStartingChaos: 100
    maxStartingChaos: 100
    noRoundstartAntags: true
    livingChaosChange: 0
    deadChaosChange: 0
    eventIntervalMin: 0.1
    eventIntervalMax: 0.1
    eventsCountAllPlayers: true
  - type: SelectedGameRules
    scheduledGameRules: !type:NestedSelector
      tableId: SecretPlusTestRowEvents

- type: entity
  id: SecretPlusTestSpeedScheduler
  parent: SecretPlusTestRowScheduler
  categories: [ GameRules ]
  components:
  - type: SecretPlus
    eventIntervalMin: 1000
    eventIntervalMax: 1000
    eventSpeedByPlayers:
      0: 1
      2: 100

- type: entity
  id: SecretPlusTestSpeedSchedulerSlow
  parent: SecretPlusTestSpeedScheduler
  categories: [ GameRules ]
  components:
  - type: SecretPlus
    eventSpeedByPlayers:
      0: 1
      3: 100
";

    // Plain strings because the YAML linter doesn't see test prototypes.
    private const string LateAntagsPreset = "SecretPlusTestLateAntags";
    private const string AntagsRule = "SecretPlusTestAntags";
    private const string NoAntagsRule = "SecretPlusTestNoAntags";
    private const string LoudRuleA = "SecretPlusTestLoudA";
    private const string LoudRuleB = "SecretPlusTestLoudB";
    private const string QuietRule = "SecretPlusTestQuiet";
    private const string LateRule = "SecretPlusTestLateRule";
    private const string Scheduler = "SecretPlusTestScheduler";
    private const string SchedulerLiving = "SecretPlusTestSchedulerLiving";
    private const string RowScheduler = "SecretPlusTestRowScheduler";
    private const string SpeedScheduler = "SecretPlusTestSpeedScheduler";
    private const string SpeedSchedulerSlow = "SecretPlusTestSpeedSchedulerSlow";
    private const string Event = "SecretPlusTestEvent";
    private const string StockEvent = "SecretPlusTestStockEvent";
    private const string GhostAntagA = "SecretPlusTestGhostA";
    private const string GhostAntagB = "SecretPlusTestGhostB";

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true
    };

    [Test]
    public async Task ExclusiveRulesDoNotStartTogether()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(GoobCVars.StationEventPlayerBias, 1);

        await server.WaitAssertion(() =>
        {
            var scheduler = server.EntMan.GetComponent<SecretPlusComponent>(ticker.AddGameRule(AntagsRule));
            Assert.Multiple(() =>
            {
                Assert.That(scheduler.StartingPlayers, Is.LessThan(10));
                Assert.That(ticker.IsGameRuleAdded(LoudRuleA), Is.Not.EqualTo(ticker.IsGameRuleAdded(LoudRuleB)));
                Assert.That(ticker.IsGameRuleAdded(QuietRule), Is.True);
                // The loud rule costs half its score here, the quiet one costs all of it.
                Assert.That(scheduler.ChaosScore, Is.EqualTo(-1000f * scheduler.StartingPlayers + 150f).Within(0.01f));
            });
        });
    }

    [Test]
    public async Task ExclusiveRulesStartTogetherWithEnoughPlayers()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(GoobCVars.StationEventPlayerBias, 10);

        await server.WaitAssertion(() =>
        {
            var scheduler = server.EntMan.GetComponent<SecretPlusComponent>(ticker.AddGameRule(AntagsRule));
            Assert.Multiple(() =>
            {
                Assert.That(scheduler.StartingPlayers, Is.AtLeast(10));
                Assert.That(ticker.IsGameRuleAdded(LoudRuleA), Is.True);
                Assert.That(ticker.IsGameRuleAdded(LoudRuleB), Is.True);
                Assert.That(ticker.IsGameRuleAdded(QuietRule), Is.True);
                Assert.That(scheduler.ChaosScore, Is.EqualTo(-1000f * scheduler.StartingPlayers + 200f).Within(0.01f));
            });
        });
    }

    [Test]
    public async Task RoundMayStartWithoutAntags()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(GoobCVars.StationEventPlayerBias, 10);

        await server.WaitAssertion(() =>
        {
            var scheduler = server.EntMan.GetComponent<SecretPlusComponent>(ticker.AddGameRule(NoAntagsRule));
            Assert.Multiple(() =>
            {
                Assert.That(ticker.IsGameRuleAdded(LoudRuleA), Is.False);
                Assert.That(ticker.IsGameRuleAdded(LoudRuleB), Is.False);
                Assert.That(ticker.IsGameRuleAdded(QuietRule), Is.False);
                Assert.That(scheduler.ChaosScore, Is.EqualTo(-1000f * scheduler.StartingPlayers).Within(0.01f));
            });
        });
    }

    [Test]
    public async Task GhostAntagsAreLimitedByConnectedPlayers()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(GoobCVars.StationEventPlayerBias, 1);
        server.CfgMan.SetCVar(GoobCVars.MinimumTimeUntilFirstEvent, 0f);

        await server.WaitPost(() => ticker.StartGameRule(Scheduler));
        await Pair.RunTicksSync(120);

        Assert.Multiple(() =>
        {
            Assert.That(ticker.IsGameRuleAdded(Event), Is.True);
            Assert.That(ticker.IsGameRuleAdded(GhostAntagA), Is.Not.EqualTo(ticker.IsGameRuleAdded(GhostAntagB)));
        });
    }

    [Test]
    public async Task EventsCountLivingPlayersByDefault()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(GoobCVars.StationEventPlayerBias, 1);
        server.CfgMan.SetCVar(GoobCVars.MinimumTimeUntilFirstEvent, 0f);

        await server.WaitPost(() => ticker.StartGameRule(SchedulerLiving));
        await Pair.RunTicksSync(120);

        Assert.Multiple(() =>
        {
            Assert.That(ticker.IsGameRuleAdded(Event), Is.False);
            Assert.That(ticker.IsGameRuleAdded(GhostAntagA), Is.True);
            Assert.That(ticker.IsGameRuleAdded(GhostAntagB), Is.True);
        });
    }

    [Test]
    public async Task TableRowOverridesEventValues()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(GoobCVars.StationEventPlayerBias, 1);
        server.CfgMan.SetCVar(GoobCVars.MinimumTimeUntilFirstEvent, 0f);

        var uid = EntityUid.Invalid;
        await server.WaitPost(() => ticker.StartGameRule(RowScheduler, out uid));
        await Pair.RunTicksSync(120);

        var scheduler = server.EntMan.GetComponent<SecretPlusComponent>(uid);
        var started = ticker.GetAddedGameRules()
            .Count(rule => server.EntMan.GetComponent<MetaDataComponent>(rule).EntityPrototype?.ID == StockEvent);
        Assert.Multiple(() =>
        {
            Assert.That(started, Is.Positive);
            Assert.That(ticker.IsGameRuleAdded(Event), Is.False);
            Assert.That(scheduler.ChaosScore, Is.EqualTo(-100f * scheduler.StartingPlayers + 37f * started).Within(0.01f));
        });
    }

    [Test]
    public async Task EventsComeFasterWithMorePlayers()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();
        var timing = server.ResolveDependency<IGameTiming>();

        server.CfgMan.SetCVar(GoobCVars.StationEventPlayerBias, 1);
        server.CfgMan.SetCVar(GoobCVars.MinimumTimeUntilFirstEvent, 0f);

        var fast = EntityUid.Invalid;
        var slow = EntityUid.Invalid;
        await server.WaitPost(() =>
        {
            ticker.StartGameRule(SpeedScheduler, out fast);
            ticker.StartGameRule(SpeedSchedulerSlow, out slow);
        });
        await Pair.RunTicksSync(30);

        var untilFast = server.EntMan.GetComponent<SecretPlusComponent>(fast).TimeNextEvent - timing.CurTime;
        var untilSlow = server.EntMan.GetComponent<SecretPlusComponent>(slow).TimeNextEvent - timing.CurTime;
        Assert.Multiple(() =>
        {
            Assert.That(untilFast, Is.GreaterThan(TimeSpan.Zero).And.LessThan(TimeSpan.FromSeconds(11)));
            Assert.That(untilSlow, Is.GreaterThan(TimeSpan.FromSeconds(900)));
        });
    }

    [Test]
    public async Task LateAntagsFollowCrewGrowthUntilTheWindowCloses()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();

        await Pair.WaitClientCommand("toggleready True");
        await Pair.WaitCommand($"setgamepreset {LateAntagsPreset}");
        await Pair.WaitCommand("startround");
        await Pair.RunTicksSync(10);

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
        var antag = ticker.GetAddedGameRules<AntagSelectionComponent>()
            .Where(rule => server.EntMan.GetComponent<MetaDataComponent>(rule).EntityPrototype?.ID == LateRule)
            .Select(rule => rule.Comp.Antags[0])
            .OfType<MinMaxAntagCountSelector>()
            .Single();
        Assert.Multiple(() =>
        {
            Assert.That(antag.Range.Min, Is.EqualTo(1f));
            Assert.That(antag.Range.Max, Is.EqualTo(1f));
        });

        server.CfgMan.SetCVar(GoobCVars.StationEventPlayerBias, 25);
        await Pair.RunTicksSync(5);

        Assert.Multiple(() =>
        {
            Assert.That(antag.Range.Min, Is.EqualTo(1f));
            Assert.That(antag.Range.Max, Is.EqualTo(3f));
        });

        for (var i = 0; i < 40 && ticker.RoundDuration() < TimeSpan.FromSeconds(5); i++)
        {
            await Pair.RunTicksSync(30);
        }

        Assert.Multiple(() =>
        {
            Assert.That(ticker.RoundDuration(), Is.GreaterThan(TimeSpan.FromSeconds(4)));
            Assert.That(antag.Range.Min, Is.EqualTo(1f));
            Assert.That(antag.Range.Max, Is.EqualTo(1f));
        });
    }
}
