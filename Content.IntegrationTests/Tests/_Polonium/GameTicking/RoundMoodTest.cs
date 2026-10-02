#nullable enable
using System.Linq;
using Content.Client.GameTicking.Managers;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.GameTicking;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared._Polonium.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Voting;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.GameTicking;

public sealed class RoundMoodTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: roundMood
  id: MoodTestSolo
  name: ui-vote-mood-hot
  order: 100

- type: roundMood
  id: MoodTestPair
  name: ui-vote-mood-hot
  order: 101

- type: roundMood
  id: MoodTestCrowded
  name: ui-vote-mood-hot
  order: 102

- type: roundMood
  id: MoodTestMixed
  name: ui-vote-mood-hot
  order: 103

- type: roundMood
  id: MoodTestBig
  name: ui-vote-mood-hot
  order: 104
  minPlayers: 2

- type: gamePreset
  id: MoodTestSolo
  name: Mood Test Solo
  description: """"
  showInVote: false
  moods:
    MoodTestSolo: 1
    MoodTestPair: 3
    MoodTestMixed: 1
    MoodTestBig: 1
  rules:
  - MoodTestRuleSolo

- type: gamePreset
  id: MoodTestShared
  name: Mood Test Shared
  description: """"
  showInVote: false
  moods:
    MoodTestPair: 1
  rules:
  - MoodTestRuleShared

- type: gamePreset
  id: MoodTestCrowded
  name: Mood Test Crowded
  description: """"
  showInVote: false
  minPlayers: 99
  moods:
    MoodTestCrowded: 1
    MoodTestMixed: 99
  rules:
  - MoodTestRuleCrowded

- type: gamePreset
  id: MoodTestForgotten
  name: Mood Test Forgotten
  description: """"
  showInVote: false
  rules:
  - MoodTestRuleSolo

- type: entity
  id: MoodTestRuleSolo
  parent: BaseGameRule
  categories: [ GameRules ]

- type: entity
  id: MoodTestRuleShared
  parent: BaseGameRule
  categories: [ GameRules ]

- type: entity
  id: MoodTestRuleCrowded
  parent: BaseGameRule
  categories: [ GameRules ]
";

    private static readonly ProtoId<RoundMoodPrototype> MadnessMood = "Madness";
    private const string SecretPreset = "Secret";
    private const string ExtendedPreset = "Extended";
    private const string SoloPreset = "MoodTestSolo";
    private const string SharedPreset = "MoodTestShared";
    private const string CrowdedPreset = "MoodTestCrowded";
    private const string ForgottenPreset = "MoodTestForgotten";
    private const string SoloMood = "MoodTestSolo";
    private const string PairMood = "MoodTestPair";
    private const string CrowdedMood = "MoodTestCrowded";
    private const string MixedMood = "MoodTestMixed";
    private const string BigMood = "MoodTestBig";
    private const string SoloRule = "MoodTestRuleSolo";
    private const string CrowdedRule = "MoodTestRuleCrowded";

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true
    };

    [Test]
    public async Task WeightsFollowVotes()
    {
        var server = Pair.Server;
        var mood = server.System<RoundMoodSystem>();

        await server.WaitAssertion(() =>
        {
            var standard = mood.GetDefaultMood();
            Assert.That(standard, Is.Not.Null);

            var standardPresets = server.ProtoMan.EnumeratePrototypes<GamePresetPrototype>()
                .Where(preset => preset.Moods.ContainsKey(standard!.ID))
                .Select(preset => preset.ID)
                .ToArray();
            Assert.That(standardPresets, Is.Not.Empty);

            mood.Clear();
            var weights = mood.GetWeights();
            Assert.Multiple(() =>
            {
                Assert.That(weights.Keys, Is.EquivalentTo(standardPresets));
                Assert.That(weights.Values.Sum(), Is.EqualTo(1f).Within(0.0001f));
            });

            mood.SetVotes(new() { [PairMood] = 3 }, 1);
            weights = mood.GetWeights();
            Assert.Multiple(() =>
            {
                Assert.That(mood.GetVotes(standard!.ID), Is.EqualTo(1));
                Assert.That(weights[SoloPreset], Is.EqualTo(0.5625f).Within(0.0001f));
                Assert.That(weights[SharedPreset], Is.EqualTo(0.1875f).Within(0.0001f));
                Assert.That(weights.Values.Sum(), Is.EqualTo(1f).Within(0.0001f));
            });

            // Losing one of its presets doesn't shrink the mood's share.
            weights = mood.GetWeights(preset => preset.ID != SoloPreset);
            Assert.Multiple(() =>
            {
                Assert.That(weights.ContainsKey(SoloPreset), Is.False);
                Assert.That(weights[SharedPreset], Is.EqualTo(0.75f).Within(0.0001f));
            });

            // A mood with nothing left to offer hands its votes to the default one.
            mood.SetVotes(new() { [CrowdedMood] = 2 });
            weights = mood.GetWeights(preset => preset.ID != CrowdedPreset);
            Assert.Multiple(() =>
            {
                Assert.That(weights.Keys, Is.EquivalentTo(standardPresets));
                Assert.That(weights.Values.Sum(), Is.EqualTo(1f).Within(0.0001f));
            });

            mood.Clear();
        });
    }

    [Test]
    public async Task PresetsWithoutMoodsAreReported()
    {
        var server = Pair.Server;
        var mood = server.System<RoundMoodSystem>();

        await server.WaitAssertion(() =>
        {
            var missing = mood.GetPresetsWithoutMoods(server.ProtoMan.EnumeratePrototypes<GamePresetPrototype>());
            Assert.Multiple(() =>
            {
                Assert.That(missing, Does.Contain(ForgottenPreset));
                Assert.That(missing, Does.Not.Contain(SoloPreset));
                Assert.That(missing, Does.Not.Contain(SecretPreset));
            });
        });
    }

    [Test]
    public async Task MoodVoteStoresVotesAndKeepsPreset()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();
        var mood = server.System<RoundMoodSystem>();
        var votes = server.ResolveDependency<IVoteManager>();

        server.CfgMan.SetCVar(CCVars.VotePresetMood, true);
        server.CfgMan.SetCVar(CCVars.VoteTimerPreset, 0);

        await Pair.WaitCommand($"setgamepreset {SecretPreset}");

        await server.WaitAssertion(() =>
        {
            var moods = mood.GetVotableMoods(1).Select(option => option.ID).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(moods, Does.Contain(mood.GetDefaultMood()?.ID));
                Assert.That(moods, Does.Contain(SoloMood));
                Assert.That(moods, Does.Not.Contain(BigMood));
                Assert.That(moods, Does.Not.Contain(MadnessMood.Id));
            });

            foreach (var active in votes.ActiveVotes.ToArray())
            {
                active.Cancel();
            }

            mood.Clear();
            votes.CreateStandardVote(null, StandardVoteType.Preset);

            var vote = votes.ActiveVotes.Single(v => !v.Finished);
            Assert.Multiple(() =>
            {
                Assert.That(vote.Type, Is.EqualTo(StandardVoteType.Preset));
                Assert.That(vote.IsValidOption(moods.Count - 1), Is.True);
                Assert.That(vote.IsValidOption(moods.Count), Is.False);
            });

            vote.CastVote(Pair.Player!, moods.IndexOf(SoloMood));
        });

        await Pair.RunTicksSync(10);

        Assert.Multiple(() =>
        {
            Assert.That(mood.GetVotes(SoloMood), Is.EqualTo(1));
            Assert.That(mood.GetVotes(MixedMood), Is.Zero);
            Assert.That(ticker.Preset?.ID, Is.EqualTo(SecretPreset));
        });
    }

    [Test]
    public async Task SecretPickFollowsMoodAndRoundStartClearsIt()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();
        var mood = server.System<RoundMoodSystem>();

        await server.WaitPost(() => mood.SetVotes(new() { [SoloMood] = 3 }));

        await Pair.WaitClientCommand("toggleready True");
        await Pair.WaitCommand($"setgamepreset {SecretPreset}");
        await Pair.WaitCommand("startround");
        await Pair.RunTicksSync(10);

        Assert.Multiple(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(ticker.IsGameRuleAdded(SoloRule), Is.True);
            Assert.That(mood.GetVotes(SoloMood), Is.Zero);
        });
    }

    [Test]
    public async Task SecretPickSkipsPresetsWithoutEnoughPlayers()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();
        var mood = server.System<RoundMoodSystem>();

        await server.WaitPost(() => mood.SetVotes(new() { [MixedMood] = 3 }));

        await Pair.WaitClientCommand("toggleready True");
        await Pair.WaitCommand($"setgamepreset {SecretPreset}");
        await Pair.WaitCommand("startround");
        await Pair.RunTicksSync(10);

        Assert.Multiple(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(ticker.IsGameRuleAdded(SoloRule), Is.True);
            Assert.That(ticker.IsGameRuleAdded(CrowdedRule), Is.False);
        });
    }

    [Test]
    public async Task HiddenPresetShowsSecretInLobby()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var clientTicker = client.System<ClientGameTicker>();

        var secretTitle = string.Empty;
        var extendedTitle = string.Empty;
        await server.WaitPost(() =>
        {
            var loc = server.ResolveDependency<ILocalizationManager>();
            secretTitle = loc.GetString(server.ProtoMan.Index<GamePresetPrototype>(SecretPreset).ModeTitle);
            extendedTitle = loc.GetString(server.ProtoMan.Index<GamePresetPrototype>(ExtendedPreset).ModeTitle);
        });

        server.CfgMan.SetCVar(CCVars.GamePresetHidden, true);
        await Pair.WaitCommand($"setgamepreset {ExtendedPreset}");
        await Pair.RunTicksSync(10);

        Assert.Multiple(() =>
        {
            Assert.That(clientTicker.ServerInfoBlob, Does.Contain(secretTitle));
            Assert.That(clientTicker.ServerInfoBlob, Does.Not.Contain(extendedTitle));
        });

        server.CfgMan.SetCVar(CCVars.GamePresetHidden, false);
        await Pair.RunTicksSync(10);

        Assert.That(clientTicker.ServerInfoBlob, Does.Contain(extendedTitle));
    }

    [Test]
    public async Task HiddenPresetBeatsDecoy()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var clientTicker = client.System<ClientGameTicker>();

        var secretTitle = string.Empty;
        var extendedTitle = string.Empty;
        await server.WaitPost(() =>
        {
            var loc = server.ResolveDependency<ILocalizationManager>();
            secretTitle = loc.GetString(server.ProtoMan.Index<GamePresetPrototype>(SecretPreset).ModeTitle.Value);
            extendedTitle = loc.GetString(server.ProtoMan.Index<GamePresetPrototype>(ExtendedPreset).ModeTitle.Value);
        });

        server.CfgMan.SetCVar(CCVars.GamePresetHidden, true);
        await Pair.WaitCommand($"setgamepreset {SoloPreset} 1 {ExtendedPreset}");
        await Pair.RunTicksSync(10);

        Assert.Multiple(() =>
        {
            Assert.That(clientTicker.ServerInfoBlob, Does.Contain(secretTitle));
            Assert.That(clientTicker.ServerInfoBlob, Does.Not.Contain(extendedTitle));
        });

        server.CfgMan.SetCVar(CCVars.GamePresetHidden, false);
        await Pair.RunTicksSync(10);

        Assert.That(clientTicker.ServerInfoBlob, Does.Contain(extendedTitle));
    }
}
