using Content.Server.Database;
using Content.Server.Discord;
using Content.Shared._Polonium.Survey;
using Content.Shared.CCVar;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Survey;

public sealed partial class RoundSurveySystem
{
    public const int MaxCommentsPerRound = 3;

    private readonly Dictionary<NetUserId, int> _comments = new();

    private void InitializeComments()
    {
        SubscribeNetworkEvent<RoundSurveyCommentEvent>(OnComment);
    }

    private void OnComment(RoundSurveyCommentEvent ev, EntitySessionEventArgs args)
    {
        var accepted = TryComment(args.SenderSession, ev.RoundId, ev.Topic, ev.Text);
        RaiseNetworkEvent(new RoundSurveyCommentResultEvent(accepted), args.SenderSession);
    }

    /// <summary>
    /// Stores a comment and passes it on to Discord. A player can leave only a few of them in a round.
    /// </summary>
    public bool TryComment(ICommonSession session, int? roundId, ProtoId<RoundSurveyTopicPrototype> topic, string? text)
    {
        if (!_cfg.GetCVar(CCVars.SurveyEnabled) || string.IsNullOrEmpty(topic.Id) || !_proto.HasIndex(topic))
            return false;

        text = text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > RoundSurveyCommentEvent.MaxLength)
            return false;

        var user = session.UserId;
        var sent = _comments.GetValueOrDefault(user);
        if (sent >= MaxCommentsPerRound)
            return false;

        _comments[user] = sent + 1;

        var comment = new SurveyComment
        {
            PlayerUserId = user,
            Topic = topic,
            Text = text,
            Time = DateTime.UtcNow,
        };

        // A comment written in the survey of a round is about that round and goes along with the player's answers.
        if (_survey is { } survey && survey.RoundId == roundId && survey.Respondents.TryGetValue(user, out var respondent))
        {
            comment.RoundId = survey.RoundId;
            comment.Preset = survey.Preset?.ID ?? string.Empty;
            comment.Job = respondent.Who.Job;
            comment.Antag = respondent.Who.Antag;

            respondent.Comments.Add(comment);
            respondent.Dirty = true;
            respondent.ChangedAt = _timing.RealTime;
        }
        else
        {
            var who = DescribePlayers().GetValueOrDefault(user);
            comment.RoundId = _ticker.RoundId;
            comment.Preset = GetPlayedPreset()?.ID ?? string.Empty;
            comment.Job = who.Job;
            comment.Antag = who.Antag;

            PostComment(comment, session.Name);
        }

        SaveComment(comment);
        return true;
    }

    private async void SaveComment(SurveyComment comment)
    {
        try
        {
            await _db.AddSurveyComment(comment);
        }
        catch (Exception e)
        {
            Log.Error($"Error while saving a round survey comment: {e}");
        }
    }

    private async void PostComment(SurveyComment comment, string author)
    {
        if (_responsesWebhook is not { } webhook)
            return;

        try
        {
            await _discord.CreateMessage(webhook, BuildComment(comment, author));
        }
        catch (Exception e)
        {
            Log.Error($"Error while sending a round survey comment to Discord: {e}");
        }
    }

    /// <summary>
    /// The message for a comment that was written outside of a survey and so has no answers to go along with.
    /// </summary>
    public WebhookPayload BuildComment(SurveyComment comment, string author)
    {
        return new WebhookPayload
        {
            Embeds =
            [
                new WebhookEmbed
                {
                    Title = author,
                    Description = Loc.GetString("round-survey-discord-comment",
                        ("round", comment.RoundId.ToString()),
                        ("preset", comment.Preset == string.Empty ? "?" : comment.Preset),
                        ("role", DescribeRole(comment.Job, comment.Antag)),
                        ("topic", DescribeTopic(comment.Topic)),
                        ("text", comment.Text)),
                    Color = SummaryColor,
                    Footer = new WebhookEmbedFooter { Text = _server.ServerName },
                    Fields = [],
                },
            ],
        };
    }

    private string DescribeTopic(string id)
    {
        return _proto.TryIndex<RoundSurveyTopicPrototype>(id, out var topic) ? Loc.GetString(topic.Name) : id;
    }
}
