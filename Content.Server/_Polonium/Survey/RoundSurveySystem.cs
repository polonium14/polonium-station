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
    private const int MaxReasonChangesPerQuestion = 20;
    private const int MaxResponseFailures = 3;
    private const int SummaryColor = 0x4F8FD1;
    private const string Bars = "▁▂▃▄▅▆▇█";
    private static readonly TimeSpan SummaryInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ResponseDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ResponseInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan ResponseRetryDelay = TimeSpan.FromSeconds(5);

    private readonly Dictionary<NetUserId, TimeSpan> _joined = new();
    private readonly Dictionary<NetUserId, int> _surveys = new();
    private readonly Dictionary<(NetUserId User, string Question), int> _drawn = new();
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
        SubscribeNetworkEvent<RoundSurveyReasonsEvent>(OnReasons);

        Subs.CVar(_cfg, CCVars.SurveyEnabled, OnEnabledChanged);
        Subs.CVar(_cfg, CCVars.DiscordSurveyWebhook, OnWebhookChanged, true);
        Subs.CVar(_cfg, CCVars.DiscordSurveyResponsesWebhook, OnResponsesWebhookChanged, true);

        InitializeComments();

        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
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

    private void OnEnabledChanged(bool enabled)
    {
        if (!enabled && _survey != null)
            Close(_survey);
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

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.InGame || _survey is not { } survey)
            return;

        if (survey.Respondents.TryGetValue(args.Session.UserId, out var respondent))
            Offer(survey, respondent, args.Session);
    }

    private void Offer(Survey survey, Respondent respondent, ICommonSession session)
    {
        respondent.Offered = true;
        respondent.Name = session.Name;

        RaiseNetworkEvent(new RoundSurveyOfferEvent(survey.RoundId, respondent.Questions, survey.ExpectedStart, survey.CloseDelay), session);

        if (survey.ClosesAt is { } closesAt)
            RaiseNetworkEvent(new RoundSurveyDeadlineEvent(survey.RoundId, closesAt), session);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _joined.Clear();
        _comments.Clear();
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

        var survey = new Survey
        {
            RoundId = ev.RoundId,
            Preset = GetPlayedPreset(),
            Duration = ev.RoundDuration,
            Players = _joined.Count,
            Moods = string.Join(" · ", _mood.GetRoundVotes().Select(vote => $"{Loc.GetString(vote.Mood.Name)} {vote.Votes}")),
            ExpectedStart = _timing.CurTime + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.RoundRestartTime)) + _ticker.LobbyDuration,
            CloseDelay = TimeSpan.FromSeconds(Math.Max(_cfg.GetCVar(CCVars.SurveyCloseDelay), 0f)),
        };

        var players = DescribePlayers();
        var antags = players.Values.Any(player => player.Antag != null);
        foreach (var (user, joinedAt) in _joined)
        {
            // Those who left stay on the list, the survey reaches them if they come back while it is open.
            var session = _player.TryGetSessionById(user, out var found) && found.Status == SessionStatus.InGame ? found : null;
            if (session == null)
                survey.Left++;

            var who = players.GetValueOrDefault(user) with { TimeInRound = _timing.CurTime - joinedAt };
            var questions = PickQuestions(who, antags, user);
            if (questions.Count == 0)
                continue;

            Remember(user, questions);

            var name = _player.TryGetPlayerData(user, out var data) ? data.UserName : user.ToString();
            var respondent = new Respondent(name, who, questions);
            survey.Respondents[user] = respondent;

            if (session != null)
                Offer(survey, respondent, session);
        }

        _survey = survey;
    }

    private TimeSpan GetPlaytime(ICommonSession session)
    {
        return _playTime.TryGetTrackerTime(session, PlayTimeTrackingShared.TrackerOverall, out var playtime)
            ? playtime.Value
            : TimeSpan.Zero;
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
    /// If the player is named, the questions drawn for them lately are left out.
    /// </summary>
    public List<ProtoId<RoundSurveyQuestionPrototype>> PickQuestions(RoundSurveyRespondent who, bool antags, NetUserId? user = null)
    {
        var picked = new List<RoundSurveyQuestionPrototype>();
        var pool = new Dictionary<RoundSurveyQuestionPrototype, float>();

        foreach (var question in _proto.EnumeratePrototypes<RoundSurveyQuestionPrototype>())
        {
            if (!CanAsk(question, who, antags))
                continue;

            if (question.Always)
                picked.Add(question);
            else if (question.Weight > 0f && !IsResting(question, user))
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

    /// <summary>
    /// Notes the questions a player has got, so that the drawn ones are not repeated right away.
    /// </summary>
    public void Remember(NetUserId user, List<ProtoId<RoundSurveyQuestionPrototype>> questions)
    {
        var number = _surveys.GetValueOrDefault(user) + 1;
        _surveys[user] = number;

        foreach (var id in questions)
        {
            if (_proto.TryIndex(id, out var question) && !question.Always)
                _drawn[(user, id.Id)] = number;
        }
    }

    private bool IsResting(RoundSurveyQuestionPrototype question, NetUserId? user)
    {
        return user != null
               && _drawn.TryGetValue((user.Value, question.ID), out var last)
               && _surveys.GetValueOrDefault(user.Value) - last < question.Cooldown;
    }

    private bool CanAsk(RoundSurveyQuestionPrototype question, RoundSurveyRespondent who, bool antags)
    {
        if (who.TimeInRound < question.MinTimeInRound || question.NeedsAntags && !antags)
            return false;

        var today = DateTime.UtcNow.Date;
        if (question.From?.Date > today || question.Until?.Date < today)
            return false;

        switch (question.Audience)
        {
            case RoundSurveyAudience.Crew when who.Job == null || who.Antag != null:
            case RoundSurveyAudience.Antag when who.Antag == null:
                return false;
        }

        if (!HasFittingRole(question, who))
            return false;

        return question.RuleWhitelist == null || _ticker.IsGameRuleAdded(question.RuleWhitelist);
    }

    private bool HasFittingRole(RoundSurveyQuestionPrototype question, RoundSurveyRespondent who)
    {
        if (question.Jobs == null && question.Departments == null && question.Antags == null)
            return true;

        if (who.Antag != null && question.Antags?.Contains(who.Antag) == true)
            return true;

        if (who.Job == null)
            return false;

        if (question.Jobs?.Contains(who.Job) == true)
            return true;

        foreach (var id in question.Departments ?? [])
        {
            if (_proto.TryIndex(id, out var department) && department.Roles.Contains(who.Job))
                return true;
        }

        return false;
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

        if (!_proto.TryIndex(question, out var asked))
            return false;

        if (asked.YesNo && value is not (RoundSurveyQuestionPrototype.MinAnswer or RoundSurveyQuestionPrototype.MaxAnswer))
            return false;

        if (respondent.Answers.TryGetValue(question, out var old) && old == value)
            return true;

        if (respondent.Changes >= respondent.Questions.Count * MaxChangesPerQuestion)
            return false;

        if (respondent.Who.Playtime == TimeSpan.Zero)
            respondent.Who = respondent.Who with { Playtime = GetPlaytime(session) };

        // The reasons were given for another kind of answer.
        if (asked.GetFollowUp(value) != asked.GetFollowUp(old))
            respondent.Reasons.Remove(question);

        respondent.Changes++;
        respondent.Answers[question] = value;
        respondent.Dirty = true;
        respondent.ChangedAt = _timing.RealTime;
        survey.Dirty = true;

        Save(survey, session.UserId, respondent, question);
        return true;
    }

    private void OnReasons(RoundSurveyReasonsEvent ev, EntitySessionEventArgs args)
    {
        TrySetReasons(args.SenderSession, ev.RoundId, ev.Question, ev.Reasons);
    }

    /// <summary>
    /// Records what the player ticked under an answer, if that answer has a follow-up offering all of it.
    /// </summary>
    public bool TrySetReasons(
        ICommonSession session,
        int roundId,
        ProtoId<RoundSurveyQuestionPrototype> question,
        List<ProtoId<RoundSurveyReasonPrototype>>? reasons)
    {
        if (reasons == null || _survey is not { } survey || survey.RoundId != roundId)
            return false;

        if (!survey.Respondents.TryGetValue(session.UserId, out var respondent) || !respondent.Answers.TryGetValue(question, out var value))
            return false;

        if (!_proto.TryIndex(question, out var asked) || asked.GetFollowUp(value) is not { } followUp)
            return false;

        if (reasons.Count > followUp.Reasons.Count)
            return false;

        var picked = followUp.Reasons.Where(reasons.Contains).ToList();
        if (picked.Count != reasons.Count)
            return false;

        var old = respondent.Reasons.GetValueOrDefault(question) ?? [];
        if (old.SequenceEqual(picked))
            return true;

        if (respondent.ReasonChanges >= respondent.Questions.Count * MaxReasonChangesPerQuestion)
            return false;

        respondent.ReasonChanges++;
        respondent.Reasons[question] = picked;
        respondent.Dirty = true;
        respondent.ChangedAt = _timing.RealTime;
        survey.Dirty = true;

        Save(survey, session.UserId, respondent, question);
        return true;
    }

    private async void Save(Survey survey, NetUserId user, Respondent respondent, ProtoId<RoundSurveyQuestionPrototype> question)
    {
        var who = respondent.Who;

        try
        {
            await _db.SetSurveyResponse(new SurveyResponse
            {
                RoundId = survey.RoundId,
                PlayerUserId = user,
                Question = question,
                Value = respondent.Answers[question],
                Reasons = string.Join(',', (respondent.Reasons.GetValueOrDefault(question) ?? []).Select(reason => reason.Id)),
                Time = DateTime.UtcNow,
                Preset = survey.Preset?.ID ?? string.Empty,
                RoundDuration = survey.Duration,
                PlayerCount = survey.Players,
                LeftCount = survey.Left,
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
            respondent.Dirty = true;
            survey.ResponseFailures++;
            survey.NextResponse = _timing.RealTime + ResponseRetryDelay;
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

        var fields = new List<WebhookEmbedField>();
        foreach (var id in respondent.Questions)
        {
            if (!_proto.TryIndex(id, out var question))
                continue;

            var answer = respondent.Answers.TryGetValue(id, out var value)
                ? $"**{DescribeAnswer(question, value)}**"
                : Loc.GetString("round-survey-discord-empty");

            if (respondent.Reasons.TryGetValue(id, out var reasons) && reasons.Count > 0)
                answer += "\n" + string.Join(", ", reasons.Select(reason => DescribeReason(reason)));

            fields.Add(new WebhookEmbedField
            {
                Name = DescribeQuestion(question),
                Value = answer,
                Inline = false,
            });
        }

        foreach (var comment in respondent.Comments)
        {
            fields.Add(new WebhookEmbedField
            {
                Name = Loc.GetString("round-survey-discord-comment-field", ("topic", DescribeTopic(comment.Topic))),
                Value = comment.Text,
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
                        ("role", DescribeRole(who.Job, who.Antag)),
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

    public string DescribeQuestion(RoundSurveyQuestionPrototype question)
    {
        if (question.YesNo)
            return Loc.GetString(question.Text);

        return Loc.GetString("round-survey-discord-question",
            ("text", Loc.GetString(question.Text)),
            ("low", Loc.GetString(question.Low)),
            ("high", Loc.GetString(question.High)));
    }

    private string DescribeAnswer(RoundSurveyQuestionPrototype question, int value)
    {
        if (!question.YesNo)
            return value.ToString();

        return Loc.GetString(value == RoundSurveyQuestionPrototype.MaxAnswer ? question.High : question.Low);
    }

    public string DescribeReason(string id)
    {
        return _proto.TryIndex<RoundSurveyReasonPrototype>(id, out var reason) ? Loc.GetString(reason.Name) : id;
    }

    private string DescribeRole(string? jobId, string? antagId)
    {
        var roles = new List<string>();

        if (jobId != null && _proto.TryIndex<JobPrototype>(jobId, out var job))
            roles.Add(job.LocalizedName);

        if (antagId != null && _proto.TryIndex<AntagPrototype>(antagId, out var antag))
            roles.Add(Loc.GetString(antag.Name));

        return roles.Count == 0 ? Loc.GetString("round-survey-discord-no-role") : string.Join(" / ", roles);
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
            ("offered", survey.Respondents.Values.Count(respondent => respondent.Offered).ToString()),
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
                Value = (question.YesNo
                    ? DescribeYesNo(question, counts)
                    : DescribeAnswers(counts, GetTarget(survey.Preset, question))) + DescribeReasons(survey, question),
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

    private string DescribeReasons(Survey survey, RoundSurveyQuestionPrototype question)
    {
        var text = new StringBuilder();
        foreach (var followUp in question.FollowUps)
        {
            var counts = new Dictionary<ProtoId<RoundSurveyReasonPrototype>, int>();
            foreach (var respondent in survey.Respondents.Values)
            {
                if (!respondent.Answers.TryGetValue(question.ID, out var value) || question.GetFollowUp(value) != followUp)
                    continue;

                foreach (var reason in respondent.Reasons.GetValueOrDefault(question.ID) ?? [])
                {
                    counts[reason] = counts.GetValueOrDefault(reason) + 1;
                }
            }

            if (counts.Count == 0)
                continue;

            var picked = followUp.Reasons
                .Where(counts.ContainsKey)
                .OrderByDescending(reason => counts[reason])
                .Select(reason => $"{DescribeReason(reason)} {counts[reason]}");

            text.Append('\n');
            text.Append(Loc.GetString("round-survey-discord-reasons",
                ("text", Loc.GetString(followUp.Text)),
                ("reasons", string.Join(" · ", picked))));
        }

        return text.ToString();
    }

    private string DescribeYesNo(RoundSurveyQuestionPrototype question, int[] counts)
    {
        var total = counts.Sum();
        if (total == 0)
            return Loc.GetString("round-survey-discord-empty");

        var yes = counts[^1];
        return Loc.GetString("round-survey-discord-yes-no",
            ("answer", Loc.GetString(question.High)),
            ("share", ((int) MathF.Round(yes * 100f / total)).ToString()),
            ("yes", yes.ToString()),
            ("count", total.ToString()));
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
        public required TimeSpan ExpectedStart;
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
        public string Name = name;
        public RoundSurveyRespondent Who = who;
        public readonly List<ProtoId<RoundSurveyQuestionPrototype>> Questions = questions;
        public readonly Dictionary<ProtoId<RoundSurveyQuestionPrototype>, int> Answers = new();
        public readonly Dictionary<ProtoId<RoundSurveyQuestionPrototype>, List<ProtoId<RoundSurveyReasonPrototype>>> Reasons = new();
        public readonly List<SurveyComment> Comments = new();
        public int Changes;
        public int ReasonChanges;
        public bool Offered;

        public ulong MessageId;
        public TimeSpan ChangedAt;
        public bool Dirty;
    }
}

public record struct RoundSurveyRespondent(string? Job, string? Antag, bool Dead, TimeSpan TimeInRound, TimeSpan Playtime);
