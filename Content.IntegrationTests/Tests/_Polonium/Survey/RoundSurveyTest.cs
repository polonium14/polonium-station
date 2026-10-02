#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Client._Polonium.Survey;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.GameTicking;
using Content.Server._Polonium.Survey;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared._Polonium.Survey;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;
using Robust.Shared.Localization;
using ClientSurveySystem = Content.Client._Polonium.Survey.RoundSurveySystem;
using ServerSurveySystem = Content.Server._Polonium.Survey.RoundSurveySystem;

namespace Content.IntegrationTests.Tests._Polonium.Survey;

public sealed class RoundSurveyTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: roundSurveyQuestion
  id: SurveyTestQuestion
  text: round-survey-question-pace
  low: round-survey-answer-calm
  high: round-survey-answer-chaotic
  target: 3
  minTimeInRound: 0
  order: 100

- type: roundMood
  id: SurveyTestMood
  name: ui-vote-mood-hot
  order: 200

- type: gamePreset
  id: SurveyTestPreset
  name: Survey Test
  description: """"
  showInVote: false
  moods:
    SurveyTestMood: 1
  surveyTargets:
    SurveyTestQuestion: 4
  rules:
  - SurveyTestRule

- type: entity
  id: SurveyTestRule
  parent: BaseGameRule
  categories: [ GameRules ]
";

    private const string SecretPreset = "Secret";
    private const string TestPreset = "SurveyTestPreset";
    private const string TestMood = "SurveyTestMood";
    private const string TestQuestion = "SurveyTestQuestion";
    private const string Rating = "RoundRating";
    private const string Pace = "RoundPace";
    private const string AntagStrength = "AntagStrength";
    private const string OwnAntagStrength = "OwnAntagStrength";
    private const string DepartmentNeeded = "DepartmentNeeded";
    private const float CloseDelay = 3f;

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true
    };

    [Test]
    public async Task PrototypesAreConsistent()
    {
        var server = Pair.Server;

        await server.WaitAssertion(() =>
        {
            var loc = server.ResolveDependency<ILocalizationManager>();

            Assert.Multiple(() =>
            {
                foreach (var question in server.ProtoMan.EnumeratePrototypes<RoundSurveyQuestionPrototype>())
                {
                    Assert.That(loc.HasString(question.Text), $"{question.ID} has no text");
                    Assert.That(loc.HasString(question.Low), $"{question.ID} has no low label");
                    Assert.That(loc.HasString(question.High), $"{question.ID} has no high label");

                    if (question.Target != null)
                        Assert.That(question.Target, Is.InRange(RoundSurveyQuestionPrototype.MinAnswer, RoundSurveyQuestionPrototype.MaxAnswer), question.ID);
                }

                foreach (var preset in server.ProtoMan.EnumeratePrototypes<GamePresetPrototype>())
                {
                    foreach (var (id, target) in preset.SurveyTargets)
                    {
                        Assert.That(server.ProtoMan.TryIndex(id, out var question), $"{preset.ID} targets unknown question {id}");
                        Assert.That(question?.Target, Is.Not.Null, $"{preset.ID} sets a target for {id}, which has no two-sided scale");
                        Assert.That(target, Is.InRange(RoundSurveyQuestionPrototype.MinAnswer, RoundSurveyQuestionPrototype.MaxAnswer), preset.ID);
                    }
                }
            });
        });
    }

    [Test]
    public async Task QuestionsFollowAudience()
    {
        var server = Pair.Server;
        var survey = server.System<ServerSurveySystem>();

        await server.WaitAssertion(() =>
        {
            var crew = new RoundSurveyRespondent("Passenger", null, false, TimeSpan.FromMinutes(30), TimeSpan.Zero);
            var antag = crew with { Antag = "Traitor" };
            var newcomer = crew with { TimeInRound = TimeSpan.FromSeconds(1) };

            Assert.Multiple(() =>
            {
                for (var i = 0; i < 50; i++)
                {
                    var asked = survey.PickQuestions(crew, true).Select(question => question.Id).ToList();
                    Assert.That(asked, Has.Count.EqualTo(ServerSurveySystem.MaxQuestions));
                    Assert.That(asked.Take(2), Is.EqualTo(new[] { Rating, Pace }));
                    Assert.That(asked, Does.Not.Contain(OwnAntagStrength));

                    Assert.That(survey.PickQuestions(crew, false).Select(question => question.Id), Does.Not.Contain(AntagStrength));

                    asked = survey.PickQuestions(antag, true).Select(question => question.Id).ToList();
                    Assert.That(asked, Does.Not.Contain(AntagStrength));
                    Assert.That(asked, Does.Not.Contain(DepartmentNeeded));

                    Assert.That(survey.PickQuestions(newcomer, true).Select(question => question.Id), Is.EqualTo(new[] { TestQuestion }));
                }
            });
        });
    }

    [Test]
    public async Task SurveyIsOffByDefault()
    {
        var server = Pair.Server;
        var survey = server.System<ServerSurveySystem>();
        var clientSurvey = Pair.Client.System<ClientSurveySystem>();

        await PlayRound();

        Assert.Multiple(() =>
        {
            Assert.That(survey.GetSummary(), Is.Null);
            Assert.That(survey.GetOffered(Pair.Player!.UserId), Is.Empty);
            Assert.That(clientSurvey.Offer, Is.Null);
        });
    }

    [Test]
    public async Task SurveyIsOfferedStoredAndSummarised()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var ticker = server.System<GameTicker>();
        var survey = server.System<ServerSurveySystem>();
        var clientSurvey = client.System<ClientSurveySystem>();

        server.CfgMan.SetCVar(CCVars.SurveyEnabled, true);
        server.CfgMan.SetCVar(CCVars.SurveyCloseDelay, CloseDelay);

        await PlayRound();

        var user = Pair.Player!.UserId;
        var roundId = ticker.RoundId;

        Assert.Multiple(() =>
        {
            Assert.That(survey.GetOffered(user).Select(question => question.Id), Is.EqualTo(new[] { TestQuestion }));
            Assert.That(clientSurvey.Offer?.RoundId, Is.EqualTo(roundId));
            Assert.That(clientSurvey.Offer?.Questions.Select(question => question.Id), Is.EqualTo(new[] { TestQuestion }));
        });

        await client.WaitAssertion(() =>
        {
            var ui = client.ResolveDependency<IUserInterfaceManager>();
            Assert.That(HasSurveyTab(ui.WindowRoot), "The round end window got no survey tab");

            // Until the next round starts there is only a guess.
            var left = clientSurvey.GetTimeLeft(roundId, out var exact);
            Assert.Multiple(() =>
            {
                Assert.That(exact, Is.False);
                Assert.That(left, Is.GreaterThan(TimeSpan.FromSeconds(CloseDelay)));
            });
        });

        await client.WaitPost(() => clientSurvey.Answer(roundId, TestQuestion, 4));
        await Pair.RunTicksSync(10);
        await client.WaitPost(() => clientSurvey.Answer(roundId, TestQuestion, 2));

        var rows = await WaitForAnswer(roundId, 2);
        Assert.Multiple(() =>
        {
            Assert.That(rows[0].PlayerUserId, Is.EqualTo(user.UserId));
            Assert.That(rows[0].Question, Is.EqualTo(TestQuestion));
            Assert.That(rows[0].Preset, Is.EqualTo(TestPreset));
            Assert.That(rows[0].Job, Is.Not.Null);
            Assert.That(rows[0].Antag, Is.Null);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(survey.TryAnswer(Pair.Player!, roundId, Rating, 3), Is.False, "Accepted a question that was not asked");
                Assert.That(survey.TryAnswer(Pair.Player!, roundId, TestQuestion, 6), Is.False, "Accepted an answer off the scale");
                Assert.That(survey.TryAnswer(Pair.Player!, roundId + 1, TestQuestion, 3), Is.False, "Accepted an answer for another round");
            });

            var mood = server.ResolveDependency<ILocalizationManager>().GetString("ui-vote-mood-hot");
            var embed = survey.GetSummary()!.Value.Embeds![0];
            Assert.Multiple(() =>
            {
                Assert.That(embed.Description, Does.Contain(TestPreset));
                Assert.That(embed.Description, Does.Contain($"{mood} 3"));
                Assert.That(embed.Fields, Has.Count.EqualTo(1));
                Assert.That(embed.Fields[0].Value, Does.Contain("0 / 1 / 0 / 0 / 0"));
                Assert.That(embed.Fields[0].Value, Does.Contain("2.0"));
                Assert.That(embed.Fields[0].Value, Does.Contain("4.0 (-2.0)"));
            });

            var response = survey.GetResponse(user)!.Value.Embeds![0];
            Assert.Multiple(() =>
            {
                Assert.That(response.Title, Is.EqualTo(Pair.Player!.Name));
                Assert.That(response.Description, Does.Contain(TestPreset));
                Assert.That(response.Fields, Has.Count.EqualTo(1));
                Assert.That(response.Fields[0].Value, Is.EqualTo("**2**"));
            });
        });

        // The survey lives on through the lobby.
        await Pair.WaitCommand("restartroundnow");
        await Pair.RunTicksSync(10);

        Assert.Multiple(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
            Assert.That(survey.GetSummary(), Is.Not.Null);
            Assert.That(clientSurvey.IsOpen(roundId), Is.True);
        });

        await client.WaitPost(() => clientSurvey.Answer(roundId, TestQuestion, 5));
        await WaitForAnswer(roundId, 5);

        // The next round starts the clock.
        await StartRound();

        await client.WaitAssertion(() =>
        {
            var left = clientSurvey.GetTimeLeft(roundId, out var exact);
            Assert.Multiple(() =>
            {
                Assert.That(exact, Is.True);
                Assert.That(left, Is.Not.Null);
                Assert.That(left, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(CloseDelay)));
                Assert.That(clientSurvey.IsOpen(roundId), Is.True);
            });
        });

        await Pair.RunSeconds(CloseDelay + 1f);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(survey.GetSummary(), Is.Null);
                Assert.That(survey.TryAnswer(Pair.Player!, roundId, TestQuestion, 1), Is.False, "Accepted an answer after the survey closed");
            });
        });

        Assert.That(clientSurvey.IsOpen(roundId), Is.False);
    }

    private async Task<List<SurveyResponse>> WaitForAnswer(int roundId, int value)
    {
        var db = Pair.Server.ResolveDependency<IServerDbManager>();
        var rows = new List<SurveyResponse>();

        for (var i = 0; i < 20 && (rows.Count != 1 || rows[0].Value != value); i++)
        {
            await Pair.RunTicksSync(5);
            rows = await db.GetSurveyResponses(roundId);
        }

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Value, Is.EqualTo(value));
        return rows;
    }

    private async Task PlayRound()
    {
        var ticker = Pair.Server.System<GameTicker>();

        await StartRound();

        await Pair.WaitCommand("endround");
        await Pair.RunTicksSync(10);
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound));
    }

    private async Task StartRound()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();

        await server.WaitPost(() => server.System<RoundMoodSystem>().SetVotes(new() { [TestMood] = 3 }));
        await Pair.WaitClientCommand("toggleready True");
        await Pair.WaitCommand($"setgamepreset {SecretPreset}");
        await Pair.WaitCommand("startround");
        await Pair.RunTicksSync(10);
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
    }

    private static bool HasSurveyTab(Control control)
    {
        return control is RoundSurveyTab || control.Children.Any(HasSurveyTab);
    }
}
