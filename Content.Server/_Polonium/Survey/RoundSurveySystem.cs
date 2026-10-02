using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Content.Server._Polonium.GameTicking;
using Content.Server.Database;
using Content.Server.Discord;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Polonium.Survey;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Random.Helpers;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Server;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Survey;

/// <summary>
/// Offers players a few questions when the round ends, stores the answers and keeps a summary of them on Discord.
/// </summary>
public sealed partial class RoundSurveySystem : EntitySystem
{
    [Dependency] private IBaseServer _server = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private DiscordWebhook _discord = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private RoundMoodSystem _mood = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _roles = default!;

    public const int MaxQuestions = 3;

    private const int MaxChangesPerQuestion = 5;
    private const int MaxResponseFailures = 3;
    private const int SummaryColor = 0x4F8FD1;
    private const string Bars = "▁▂▃▄▅▆▇█";
    private static readonly TimeSpan SummaryInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ResponseDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ResponseInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan ResponseRetryDelay = TimeSpan.FromSeconds(5);

    private readonly Dictionary<NetUserId, TimeSpan> _joined = new();
    private readonly List<Survey> _closing = new();
    private WebhookIdentifier? _webhook;
    private WebhookIdentifier? _responsesWebhook;
    private Survey? _survey;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeNetworkEvent<RoundSurveyAnswerEvent>(OnAnswer);

        Subs.CVar(_cfg, CCVars.DiscordSurveyWebhook, OnWebhookChanged, true);
        Subs.CVar(_cfg, CCVars.DiscordSurveyResponsesWebhook, OnResponsesWebhookChanged, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_survey is { ClosesAt: { } closesAt } && _timing.CurTime >= closesAt)
            Close(_survey);

        if (_survey != null)
            Deliver(_survey);

        for (var i = _closing.Count - 1; i >= 0; i--)
        {
            Deliver(_closing[i]);

            if (IsDelivered(_closing[i]))
                _closing.RemoveAt(i);
        }
    }

    private void OnWebhookChanged(string url)
    {
        _webhook = null;

        if (!string.IsNullOrWhiteSpace(url))
            _discord.GetWebhook(url, data => _webhook = data.ToIdentifier());
    }

    private void OnResponsesWebhookChanged(string url)
    {
        _responsesWebhook = null;

        if (!string.IsNullOrWhiteSpace(url))
            _discord.GetWebhook(url, data => _responsesWebhook = data.ToIdentifier());
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (_ticker.RunLevel == GameRunLevel.PostRound || HasComp<GhostComponent>(ev.Entity))
            return;

        _joined.TryAdd(ev.Player.UserId, _timing.CurTime);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _joined.Clear();
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        if (_survey is { ClosesAt: null } survey)
            SetDeadline(survey, _timing.CurTime + survey.CloseDelay);
    }

    private void SetDeadline(Survey survey, TimeSpan closesAt)
    {
        survey.ClosesAt = closesAt;

        var ev = new RoundSurveyDeadlineEvent(survey.RoundId, closesAt);
        foreach (var user in survey.Respondents.Keys)
        {
            if (_player.TryGetSessionById(user, out var session) && session.Status == SessionStatus.InGame)
                RaiseNetworkEvent(ev, session);
        }
    }

    private void Close(Survey survey)
    {
        if (survey.ClosesAt == null || _timing.CurTime < survey.ClosesAt)
            SetDeadline(survey, _timing.CurTime);

        if (_survey == survey)
            _survey = null;

        survey.Closed = true;
        _closing.Add(survey);
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        if (!_cfg.GetCVar(CCVars.SurveyEnabled) || _ticker.RunLevel != GameRunLevel.PostRound || _survey?.RoundId == ev.RoundId)
            return;

        if (_survey != null)
            Close(_survey);

        var closeDelay = TimeSpan.FromSeconds(Math.Max(_cfg.GetCVar(CCVars.SurveyCloseDelay), 0f));
        var expectedStart = _timing.CurTime + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.RoundRestartTime)) + _ticker.LobbyDuration;

        var survey = new Survey
        {
            RoundId = ev.RoundId,
            Preset = GetPlayedPreset(),
            Duration = ev.RoundDuration,
            Players = _joined.Count,
            Moods = string.Join(" · ", _mood.GetRoundVotes().Select(vote => $"{Loc.GetString(vote.Mood.Name)} {vote.Votes}")),
            CloseDelay = closeDelay,
        };

        var players = DescribePlayers();
        var antags = players.Values.Any(player => player.Antag != null);
        foreach (var (user, joinedAt) in _joined)
        {
            if (!_player.TryGetSessionById(user, out var session) || session.Status != SessionStatus.InGame)
            {
                survey.Left++;
                continue;
            }

            var who = players.GetValueOrDefault(user) with { TimeInRound = _timing.CurTime - joinedAt };
            if (_playTime.TryGetTrackerTime(session, PlayTimeTrackingShared.TrackerOverall, out var playtime))
                who.Playtime = playtime.Value;

            var questions = PickQuestions(who, antags);
            if (questions.Count == 0)
                continue;

            survey.Respondents[user] = new Respondent(session.Name, who, questions);
            RaiseNetworkEvent(new RoundSurveyOfferEvent(ev.RoundId, questions, expectedStart, closeDelay), session);
        }

        _survey = survey;
    }

    private GamePresetPrototype? GetPlayedPreset()
    {
        var secrets = EntityQueryEnumerator<SecretRuleComponent>();
        while (secrets.MoveNext(out var secret))
        {
            if (_proto.TryIndex(secret.SelectedPreset, out var picked))
                return picked;
        }

        return _ticker.CurrentPreset;
    }

    private Dictionary<NetUserId, RoundSurveyRespondent> DescribePlayers()
    {
        var players = new Dictionary<NetUserId, RoundSurveyRespondent>();
        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var mindId, out var mind))
        {
            if ((mind.UserId ?? mind.OriginalOwnerUserId) is not { } user)
                continue;

            var who = players.GetValueOrDefault(user);
            var known = who.Job != null || who.Antag != null;

            _jobs.MindTryGetJobId(mindId, out var job);
            who.Job ??= job?.Id;

            if (_roles.MindIsAntagonist(mindId))
                who.Antag ??= _roles.MindGetAllRoleInfo(mindId).FirstOrDefault(role => role.Antagonist).Prototype;

            // A later ghost role must not hide how the character the player came with ended up.
            if (!known)
                who.Dead = _mind.IsCharacterDeadIc(mind);

            players[user] = who;
        }

        return players;
    }

    /// <summary>
    /// Questions to ask this player: the ones marked as always asked, the rest drawn by weight.
    /// </summary>
    public List<ProtoId<RoundSurveyQuestionPrototype>> PickQuestions(RoundSurveyRespondent who, bool antags)
    {
        var picked = new List<RoundSurveyQuestionPrototype>();
        var pool = new Dictionary<RoundSurveyQuestionPrototype, float>();

        foreach (var question in _proto.EnumeratePrototypes<RoundSurveyQuestionPrototype>())
        {
            if (!CanAsk(question, who, antags))
                continue;

            if (question.Always)
                picked.Add(question);
            else if (question.Weight > 0f)
                pool.Add(question, question.Weight);
        }

        picked = InOrder(picked).Take(MaxQuestions).ToList();
        while (picked.Count < MaxQuestions && pool.Count > 0)
        {
            var question = _random.Pick(pool);
            pool.Remove(question);
            picked.Add(question);
        }

        return InOrder(picked).Select(question => new ProtoId<RoundSurveyQuestionPrototype>(question.ID)).ToList();
    }

    private bool CanAsk(RoundSurveyQuestionPrototype question, RoundSurveyRespondent who, bool antags)
    {
        if (who.TimeInRound < question.MinTimeInRound || question.NeedsAntags && !antags)
            return false;

        switch (question.Audience)
        {
            case RoundSurveyAudience.Crew when who.Job == null || who.Antag != null:
            case RoundSurveyAudience.Antag when who.Antag == null:
                return false;
        }

        return question.RuleWhitelist == null || _ticker.IsGameRuleAdded(question.RuleWhitelist);
    }

    private static IEnumerable<RoundSurveyQuestionPrototype> InOrder(IEnumerable<RoundSurveyQuestionPrototype> questions)
    {
        return questions.OrderBy(question => question.Order).ThenBy(question => question.ID);
    }

    private void OnAnswer(RoundSurveyAnswerEvent ev, EntitySessionEventArgs args)
    {
        TryAnswer(args.SenderSession, ev.RoundId, ev.Question, ev.Value);
    }

    /// <summary>
    /// Records an answer if this player was asked this question in the survey that is open right now.
    /// </summary>
    public bool TryAnswer(ICommonSession session, int roundId, ProtoId<RoundSurveyQuestionPrototype> question, int value)
    {
        if (_survey is not { } survey || survey.RoundId != roundId)
            return false;

        if (value is < RoundSurveyQuestionPrototype.MinAnswer or > RoundSurveyQuestionPrototype.MaxAnswer)
            return false;

        if (!survey.Respondents.TryGetValue(session.UserId, out var respondent) || !respondent.Questions.Contains(question))
            return false;

        if (respondent.Answers.TryGetValue(question, out var old) && old == value)
            return true;

        if (respondent.Changes >= respondent.Questions.Count * MaxChangesPerQuestion)
            return false;

        respondent.Changes++;
        respondent.Answers[question] = value;
        respondent.Dirty = true;
        respondent.ChangedAt = _timing.RealTime;
        survey.Dirty = true;

        Save(survey, session.UserId, respondent.Who, question, value);
        return true;
    }

    private async void Save(Survey survey, NetUserId user, RoundSurveyRespondent who, string question, int value)
    {
        try
        {
            await _db.SetSurveyResponse(new SurveyResponse
            {
                RoundId = survey.RoundId,
                PlayerUserId = user,
                Question = question,
                Value = value,
                Time = DateTime.UtcNow,
                Preset = survey.Preset?.ID ?? string.Empty,
                RoundDuration = survey.Duration,
                PlayerCount = survey.Players,
                Job = who.Job,
                Antag = who.Antag,
                Dead = who.Dead,
                TimeInRound = who.TimeInRound,
                Playtime = who.Playtime,
            });
        }
        catch (Exception e)
        {
            Log.Error($"Error while saving a round survey response: {e}");
        }
    }

    public List<ProtoId<RoundSurveyQuestionPrototype>> GetOffered(NetUserId user)
    {
        return _survey != null && _survey.Respondents.TryGetValue(user, out var respondent) ? respondent.Questions : [];
    }

    public WebhookPayload? GetSummary()
    {
        return _survey == null ? null : BuildSummary(_survey);
    }

    public WebhookPayload? GetResponse(NetUserId user)
    {
        return _survey != null && _survey.Respondents.TryGetValue(user, out var respondent)
            ? BuildResponse(_survey, respondent)
            : null;
    }

    private void Deliver(Survey survey)
    {
        var now = _timing.RealTime;

        if (survey is { Dirty: true, Sending: false, Failed: false } && _webhook is { } summary && (survey.Closed || now >= survey.NextFlush))
            FlushSummary(survey, summary);

        if (survey.PostingResponse || survey.ResponseFailures >= MaxResponseFailures || now < survey.NextResponse)
            return;

        if (_responsesWebhook is not { } responses)
            return;

        foreach (var respondent in survey.Respondents.Values)
        {
            // Wait for the player to stop clicking, so that one message carries all of their answers.
            if (!respondent.Dirty || !survey.Closed && now < respondent.ChangedAt + ResponseDelay)
                continue;

            PostResponse(survey, respondent, responses);
            return;
        }
    }

    private bool IsDelivered(Survey survey)
    {
        if (survey.Sending || survey.PostingResponse)
            return false;

        if (survey is { Dirty: true, Failed: false } && _webhook != null)
            return false;

        return _responsesWebhook == null
               || survey.ResponseFailures >= MaxResponseFailures
               || !survey.Respondents.Values.Any(respondent => respondent.Dirty);
    }

    private async void FlushSummary(Survey survey, WebhookIdentifier webhook)
    {
        survey.Sending = true;
        survey.Dirty = false;
        survey.NextFlush = _timing.RealTime + SummaryInterval;

        try
        {
            var payload = BuildSummary(survey);

            if (survey.MessageId != 0)
                await _discord.EditMessage(webhook, survey.MessageId, payload);
            else if (await CreateMessage(webhook, payload) is { } id)
                survey.MessageId = id;
            else
                survey.Failed = true;
        }
        catch (Exception e)
        {
            survey.Failed = survey.MessageId == 0;
            Log.Error($"Error while sending the round survey summary to Discord: {e}");
        }
        finally
        {
            survey.Sending = false;
        }
    }

    private async void PostResponse(Survey survey, Respondent respondent, WebhookIdentifier webhook)
    {
        survey.PostingResponse = true;
        survey.NextResponse = _timing.RealTime + ResponseInterval;
        respondent.Dirty = false;

        try
        {
            var payload = BuildResponse(survey, respondent);

            if (respondent.MessageId != 0)
            {
                await _discord.EditMessage(webhook, respondent.MessageId, payload);
            }
            else if (await CreateMessage(webhook, payload) is { } id)
            {
                respondent.MessageId = id;
            }
            else
            {
                respondent.Dirty = true;
                survey.ResponseFailures++;
                survey.NextResponse = _timing.RealTime + ResponseRetryDelay;
            }
        }
        catch (Exception e)
        {
            survey.ResponseFailures++;
            Log.Error($"Error while sending a round survey response to Discord: {e}");
        }
        finally
        {
            survey.PostingResponse = false;
        }
    }

    private async Task<ulong?> CreateMessage(WebhookIdentifier webhook, WebhookPayload payload)
    {
        var response = await _discord.CreateMessage(webhook, payload);
        if (!response.IsSuccessStatusCode)
            return null;

        var content = await response.Content.ReadAsStringAsync();
        return ulong.Parse(JsonNode.Parse(content)?["id"]!.GetValue<string>()!);
    }

    private WebhookPayload BuildResponse(Survey survey, Respondent respondent)
    {
        var who = respondent.Who;
        var roles = new List<string>();

        if (who.Job != null && _proto.TryIndex<JobPrototype>(who.Job, out var job))
            roles.Add(job.LocalizedName);

        if (who.Antag != null && _proto.TryIndex<AntagPrototype>(who.Antag, out var antag))
            roles.Add(Loc.GetString(antag.Name));

        var fields = new List<WebhookEmbedField>();
        foreach (var id in respondent.Questions)
        {
            if (!_proto.TryIndex(id, out var question))
                continue;

            fields.Add(new WebhookEmbedField
            {
                Name = DescribeQuestion(question),
                Value = respondent.Answers.TryGetValue(id, out var value)
                    ? $"**{value}**"
                    : Loc.GetString("round-survey-discord-empty"),
                Inline = false,
            });
        }

        return new WebhookPayload
        {
            Embeds =
            [
                new WebhookEmbed
                {
                    Title = respondent.Name,
                    Description = Loc.GetString("round-survey-discord-respondent",
                        ("round", survey.RoundId.ToString()),
                        ("preset", survey.Preset?.ID ?? "?"),
                        ("role", roles.Count == 0 ? Loc.GetString("round-survey-discord-no-role") : string.Join(" / ", roles)),
                        ("fate", Loc.GetString(who.Dead ? "round-survey-discord-dead" : "round-survey-discord-alive")),
                        ("time", Clock(who.TimeInRound)),
                        ("playtime", ((int) who.Playtime.TotalHours).ToString())),
                    Color = SummaryColor,
                    Footer = new WebhookEmbedFooter { Text = _server.ServerName },
                    Fields = fields,
                },
            ],
        };
    }

    private string DescribeQuestion(RoundSurveyQuestionPrototype question)
    {
        return Loc.GetString("round-survey-discord-question",
            ("text", Loc.GetString(question.Text)),
            ("low", Loc.GetString(question.Low)),
            ("high", Loc.GetString(question.High)));
    }

    private static string Clock(TimeSpan time)
    {
        return $"{(int) time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}";
    }

    private WebhookPayload BuildSummary(Survey survey)
    {
        var description = new StringBuilder();
        description.AppendLine(Loc.GetString("round-survey-discord-round",
            ("preset", survey.Preset?.ID ?? "?"),
            ("duration", Clock(survey.Duration)),
            ("players", survey.Players.ToString()),
            ("left", survey.Left.ToString())));
        description.Append(Loc.GetString("round-survey-discord-responses",
            ("offered", survey.Respondents.Count.ToString()),
            ("answered", survey.Respondents.Values.Count(respondent => respondent.Answers.Count > 0).ToString())));

        if (survey.Moods != string.Empty)
        {
            description.AppendLine();
            description.Append(Loc.GetString("round-survey-discord-moods", ("moods", survey.Moods)));
        }

        var asked = new List<RoundSurveyQuestionPrototype>();
        foreach (var id in survey.Respondents.Values.SelectMany(respondent => respondent.Questions).Distinct())
        {
            if (_proto.TryIndex(id, out var question))
                asked.Add(question);
        }

        var fields = new List<WebhookEmbedField>();
        foreach (var question in InOrder(asked))
        {
            var counts = new int[RoundSurveyQuestionPrototype.MaxAnswer - RoundSurveyQuestionPrototype.MinAnswer + 1];
            foreach (var respondent in survey.Respondents.Values)
            {
                if (respondent.Answers.TryGetValue(question.ID, out var value))
                    counts[value - RoundSurveyQuestionPrototype.MinAnswer]++;
            }

            fields.Add(new WebhookEmbedField
            {
                Name = DescribeQuestion(question),
                Value = DescribeAnswers(counts, GetTarget(survey.Preset, question)),
                Inline = false,
            });
        }

        return new WebhookPayload
        {
            Embeds =
            [
                new WebhookEmbed
                {
                    Title = Loc.GetString("round-survey-discord-title", ("round", survey.RoundId.ToString())),
                    Description = description.ToString(),
                    Color = SummaryColor,
                    Footer = new WebhookEmbedFooter { Text = _server.ServerName },
                    Fields = fields,
                },
            ],
        };
    }

    public static float? GetTarget(GamePresetPrototype? preset, RoundSurveyQuestionPrototype question)
    {
        if (question.Target == null)
            return null;

        return preset != null && preset.SurveyTargets.TryGetValue(question.ID, out var target) ? target : question.Target;
    }

    private string DescribeAnswers(int[] counts, float? target)
    {
        var total = counts.Sum();
        if (total == 0)
            return Loc.GetString("round-survey-discord-empty");

        var most = counts.Max();
        var bars = string.Concat(counts.Select(count => Bars[(int) MathF.Round(count * (Bars.Length - 1f) / most)]));
        var average = counts.Select((count, index) => count * (index + RoundSurveyQuestionPrototype.MinAnswer)).Sum() / (float) total;

        var text = new StringBuilder();
        text.AppendLine($"`{bars}` {string.Join(" / ", counts)}");
        text.Append(Loc.GetString("round-survey-discord-result", ("average", Number(average)), ("count", total.ToString())));

        if (target != null)
        {
            text.Append(" · ");
            text.Append(Loc.GetString("round-survey-discord-target",
                ("target", Number(target.Value)),
                ("offset", (average - target.Value).ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture))));
        }

        return text.ToString();
    }

    private static string Number(float value)
    {
        return value.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private sealed class Survey
    {
        public required int RoundId;
        public required GamePresetPrototype? Preset;
        public required TimeSpan Duration;
        public required int Players;
        public required string Moods;
        public required TimeSpan CloseDelay;
        public TimeSpan? ClosesAt;
        public int Left;
        public readonly Dictionary<NetUserId, Respondent> Respondents = new();

        public ulong MessageId;
        public TimeSpan NextFlush;
        public bool Dirty = true;
        public bool Sending;
        public bool Failed;
        public bool Closed;

        public TimeSpan NextResponse;
        public bool PostingResponse;
        public int ResponseFailures;
    }

    private sealed class Respondent(string name, RoundSurveyRespondent who, List<ProtoId<RoundSurveyQuestionPrototype>> questions)
    {
        public readonly string Name = name;
        public readonly RoundSurveyRespondent Who = who;
        public readonly List<ProtoId<RoundSurveyQuestionPrototype>> Questions = questions;
        public readonly Dictionary<ProtoId<RoundSurveyQuestionPrototype>, int> Answers = new();
        public int Changes;

        public ulong MessageId;
        public TimeSpan ChangedAt;
        public bool Dirty;
    }
}

public record struct RoundSurveyRespondent(string? Job, string? Antag, bool Dead, TimeSpan TimeInRound, TimeSpan Playtime);
