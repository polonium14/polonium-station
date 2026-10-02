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
using Robust.Shared.Enums;
using Robust.Shared.Localization;
using Robust.Shared.Network;
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

                var sizes = server.ProtoMan.EnumeratePrototypes<RoundSurveyPlayerGroupPrototype>().Select(size => size.Min).ToList();
                Assert.That(sizes, Is.Unique);
                Assert.That(sizes, Is.All.GreaterThanOrEqualTo(0));
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

    [Test]
    public async Task SurveyReachesThoseWhoComeBack()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var ticker = server.System<GameTicker>();
        var survey = server.System<ServerSurveySystem>();
        var loc = server.ResolveDependency<ILocalizationManager>();

        server.CfgMan.SetCVar(CCVars.SurveyEnabled, true);
        server.CfgMan.SetCVar(CCVars.SurveyCloseDelay, 60f);

        await StartRound();

        var user = Pair.Player!.UserId;
        var name = Pair.Player!.Name;

        await Disconnect();
        await Pair.WaitCommand("endround");
        await Pair.RunTicksSync(10);

        var roundId = ticker.RoundId;
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound));
                Assert.That(survey.GetOffered(user).Select(question => question.Id), Is.EqualTo(new[] { TestQuestion }));
                Assert.That(survey.GetSummary()!.Value.Embeds![0].Description,
                    Does.Contain(loc.GetString("round-survey-discord-responses", ("offered", "0"), ("answered", "0"))));
            });
        });

        var clientSurvey = await Connect(name);

        Assert.Multiple(() =>
        {
            Assert.That(Pair.Player!.UserId, Is.EqualTo(user));
            Assert.That(clientSurvey.Offer?.RoundId, Is.EqualTo(roundId));
            Assert.That(clientSurvey.Offer?.Questions.Select(question => question.Id), Is.EqualTo(new[] { TestQuestion }));
        });

        // There is no round end window to put the survey in.
        await client.WaitAssertion(() =>
        {
            var ui = client.ResolveDependency<IUserInterfaceManager>();
            Assert.That(HasSurveyTab(ui.WindowRoot), "The survey was not shown after reconnecting");
        });

        await client.WaitPost(() => clientSurvey.Answer(roundId, TestQuestion, 4));
        var rows = await WaitForAnswer(roundId, 4);
        Assert.That(rows[0].PlayerUserId, Is.EqualTo(user.UserId));

        await server.WaitAssertion(() =>
        {
            Assert.That(survey.GetSummary()!.Value.Embeds![0].Description,
                Does.Contain(loc.GetString("round-survey-discord-responses", ("offered", "1"), ("answered", "1"))));
        });

        await Pair.WaitCommand("restartroundnow");
        await Pair.RunTicksSync(10);
        await StartRound();

        // Coming back after the clock has started brings the deadline along.
        await Disconnect();
        clientSurvey = await Connect(name);

        await client.WaitAssertion(() =>
        {
            var left = clientSurvey.GetTimeLeft(roundId, out var exact);
            Assert.Multiple(() =>
            {
                Assert.That(clientSurvey.Offer?.Questions.Select(question => question.Id), Is.EqualTo(new[] { TestQuestion }));
                Assert.That(exact, Is.True);
                Assert.That(left, Is.GreaterThan(TimeSpan.Zero));
            });
        });

        await Disconnect();
        clientSurvey = await Connect("SurveyStranger");

        Assert.Multiple(() =>
        {
            Assert.That(Pair.Player!.UserId, Is.Not.EqualTo(user));
            Assert.That(survey.GetOffered(Pair.Player!.UserId), Is.Empty);
            Assert.That(clientSurvey.Offer, Is.Null, "Offered the survey to a player who was not in the round");
        });
    }

    [Test]
    public async Task LatestAnswerIsKept()
    {
        var server = Pair.Server;
        var db = server.ResolveDependency<IServerDbManager>();

        server.CfgMan.SetCVar(CCVars.SurveyEnabled, true);

        await PlayRound();

        var user = Pair.Player!.UserId;
        var roundId = server.System<GameTicker>().RoundId;
        var now = DateTime.UtcNow.AddMinutes(-1);

        SurveyResponse Answer(int value, int second)
        {
            return new SurveyResponse
            {
                RoundId = roundId,
                PlayerUserId = user,
                Question = TestQuestion,
                Value = value,
                Time = now.AddSeconds(second),
                Preset = TestPreset,
            };
        }

        // The writes are started together and the newest answer is not the last of them.
        await Task.WhenAll(
            db.SetSurveyResponse(Answer(1, 1)),
            db.SetSurveyResponse(Answer(5, 4)),
            db.SetSurveyResponse(Answer(2, 2)),
            db.SetSurveyResponse(Answer(3, 3)));

        var rows = await db.GetSurveyResponses(roundId);
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(rows[0].Value, Is.EqualTo(5));
            Assert.That(rows[0].Time, Is.EqualTo(now.AddSeconds(4)));
        });
    }

    [Test]
    public async Task DigestCountsEachPlayerOnce()
    {
        var server = Pair.Server;
        var digests = server.System<RoundSurveyDigestSystem>();
        var loc = server.ResolveDependency<ILocalizationManager>();

        var from = new DateTime(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc);
        var regular = Guid.NewGuid();
        var guest = Guid.NewGuid();
        var stranger = Guid.NewGuid();

        SurveyResponse Answer(int round, Guid player, int value, string preset = TestPreset, int players = 20, int? left = 5)
        {
            return new SurveyResponse
            {
                RoundId = round,
                PlayerUserId = player,
                Question = TestQuestion,
                Value = value,
                Time = from.AddDays(1),
                Preset = preset,
                PlayerCount = players,
                LeftCount = left,
            };
        }

        var responses = new List<SurveyResponse>
        {
            Answer(1, regular, 5),
            Answer(2, regular, 5),
            Answer(3, regular, 5),
            Answer(1, guest, 1),
            Answer(4, stranger, 3, string.Empty, 50, null),
        };

        await server.WaitAssertion(() =>
        {
            var digest = digests.BuildDigest(responses, from, from.AddDays(7))!;
            var medium = loc.GetString("round-survey-digest-players-range", ("min", "15"), ("max", "29"));
            var large = loc.GetString("round-survey-digest-players-from", ("min", "45"));

            Assert.That(digest.Sections, Has.Count.EqualTo(2));
            var rounds = digest.Sections[0].Table;
            var answers = digest.Sections[1].Table;

            Assert.Multiple(() =>
            {
                Assert.That(digest.Title, Does.Contain("21.09").And.Contain("27.09"));
                Assert.That(digest.Summary,
                    Is.EqualTo(loc.GetString("round-survey-digest-summary", ("rounds", "4"), ("people", "3"), ("answers", "5"))));

                Assert.That(Row(rounds, TestPreset), Is.EqualTo(new[] { "3", "20", "25%" }));
                Assert.That(Row(rounds, "—"), Is.EqualTo(new[] { "1", "50", "—" }));
                Assert.That(Row(rounds, medium), Is.EqualTo(new[] { "3", "20", "25%" }));
                Assert.That(Row(rounds, large), Is.EqualTo(new[] { "1", "50", "—" }));

                Assert.That(Row(answers, TestPreset), Is.EqualTo(new[] { "2", "3.0", "50%", "50%", "-1.0" }));
                Assert.That(Row(answers, "—"), Is.EqualTo(new[] { "1", "3.0", "0%", "0%", "0.0" }));
                Assert.That(Row(answers, medium), Is.EqualTo(new[] { "2", "3.0", "50%", "50%", "-1.0" }));
            });

            var payloads = digests.GetPayloads(digest);
            Assert.That(payloads, Has.Count.EqualTo(1));

            var embed = payloads[0].Embeds![0];
            Assert.Multiple(() =>
            {
                Assert.That(embed.Description, Is.EqualTo(digest.Summary));
                Assert.That(embed.Fields, Has.Count.EqualTo(2));
                Assert.That(embed.Fields[1].Value, Does.StartWith("```").And.EndWith("```"));
            });

            Assert.That(digests.BuildDigest(responses, from, from.AddDays(1))!.Title,
                Is.EqualTo(loc.GetString("round-survey-digest-title-day", ("day", "21.09"))));
            Assert.That(digests.BuildDigest([], from, from.AddDays(7)), Is.Null);
        });
    }

    [Test]
    public async Task DigestIsMarkedOncePerPeriod()
    {
        var db = Pair.Server.ResolveDependency<IServerDbManager>();
        var monday = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
        var wednesday = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

        Assert.Multiple(() =>
        {
            Assert.That(RoundSurveyDigestSystem.GetPeriodStart(monday, 7), Is.EqualTo(monday));
            Assert.That(RoundSurveyDigestSystem.GetPeriodStart(wednesday, 7), Is.EqualTo(monday));
            Assert.That(RoundSurveyDigestSystem.GetPeriodStart(monday.AddDays(7).AddMinutes(-1), 7), Is.EqualTo(monday));
            Assert.That(RoundSurveyDigestSystem.GetPeriodStart(monday.AddDays(7), 7), Is.EqualTo(monday.AddDays(7)));
            Assert.That(RoundSurveyDigestSystem.GetPeriodStart(wednesday, 1), Is.EqualTo(monday.AddDays(2)));
            Assert.That(RoundSurveyDigestSystem.GetPeriodStart(wednesday, 30), Is.InRange(wednesday.AddDays(-30), wednesday));

            Assert.That(RoundSurveyDigestSystem.ParsePeriods("1, 7,30,7,x,0,"), Is.EqualTo(new[] { 1, 7, 30 }));
            Assert.That(RoundSurveyDigestSystem.ParsePeriods(string.Empty), Is.Empty);
        });

        Assert.That(await db.AddSurveyDigest(monday, 7), Is.True);
        Assert.That(await db.AddSurveyDigest(monday, 7), Is.False);
        Assert.That(await db.AddSurveyDigest(monday, 1), Is.True);
        Assert.That(await db.AddSurveyDigest(monday.AddDays(7), 7), Is.True);

        await db.RemoveSurveyDigest(monday, 7);
        Assert.That(await db.AddSurveyDigest(monday, 7), Is.True);
        Assert.That(await db.AddSurveyDigest(monday, 1), Is.False);
    }

    [Test]
    public async Task DigestCoversPlayedRounds()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var digests = server.System<RoundSurveyDigestSystem>();

        server.CfgMan.SetCVar(CCVars.SurveyEnabled, true);

        // The pair may come with answers from other tests, so only this round is looked at
        var started = DateTime.UtcNow;
        await PlayRound();

        var roundId = server.System<GameTicker>().RoundId;
        var clientSurvey = client.System<ClientSurveySystem>();
        await client.WaitPost(() => clientSurvey.Answer(roundId, TestQuestion, 4));

        var rows = await WaitForAnswer(roundId, 4);
        Assert.That(rows[0].LeftCount, Is.EqualTo(0));

        var now = DateTime.UtcNow;
        var digest = await digests.GetDigest(started, now.AddDays(1));

        Assert.That(digest, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(Row(digest!.Sections[0].Table, TestPreset), Is.EqualTo(new[] { "1", "1", "0%" }));
            Assert.That(Row(digest.Sections[1].Table, TestPreset), Is.EqualTo(new[] { "1", "4.0", "0%", "100%", "0.0" }));
        });

        Assert.That(await digests.GetDigest(now.AddDays(-3), now.AddDays(-2)), Is.Null);

        // There is no digest channel in tests
        Assert.That(await digests.Post(digest!), Is.False);
    }

    private static string[] Row(string table, string label)
    {
        var row = table.Split('\n').Single(line => line.StartsWith(label + " "));
        return row[label.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private async Task Disconnect()
    {
        var net = Pair.Client.ResolveDependency<IClientNetManager>();

        await Pair.Client.WaitPost(() => net.ClientDisconnect("Survey test"));
        await Pair.RunTicksSync(5);
        Assert.That(Pair.Server.PlayerMan.Sessions, Is.Empty);
    }

    private async Task<ClientSurveySystem> Connect(string name)
    {
        var net = Pair.Client.ResolveDependency<IClientNetManager>();

        await Pair.Client.WaitIdleAsync();
        Pair.Client.SetConnectTarget(Pair.Server);
        await Pair.Client.WaitPost(() => net.ClientConnect(null!, 0, name));
        await Pair.RunTicksSync(10);
        Assert.That(Pair.Player?.Status, Is.EqualTo(SessionStatus.InGame));

        // The client builds its systems anew for every connection.
        return Pair.Client.System<ClientSurveySystem>();
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
