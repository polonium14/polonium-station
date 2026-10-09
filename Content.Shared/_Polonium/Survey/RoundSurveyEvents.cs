using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Survey;

[Serializable, NetSerializable]
public sealed class RoundSurveyOfferEvent : EntityEventArgs
{
    public int RoundId;
    public List<ProtoId<RoundSurveyQuestionPrototype>> Questions;

    /// <summary>
    /// When the server expects the next round to start. Only a guess, the real deadline comes
    /// with <see cref="RoundSurveyDeadlineEvent"/>.
    /// </summary>
    public TimeSpan ExpectedStart;

    /// <summary>
    /// How long the survey stays open after the next round has started.
    /// </summary>
    public TimeSpan CloseDelay;

    public RoundSurveyOfferEvent(
        int roundId,
        List<ProtoId<RoundSurveyQuestionPrototype>> questions,
        TimeSpan expectedStart,
        TimeSpan closeDelay)
    {
        RoundId = roundId;
        Questions = questions;
        ExpectedStart = expectedStart;
        CloseDelay = closeDelay;
    }
}

/// <summary>
/// Sent once the server knows when the survey closes, which may be right now.
/// </summary>
[Serializable, NetSerializable]
public sealed class RoundSurveyDeadlineEvent : EntityEventArgs
{
    public int RoundId;
    public TimeSpan ClosesAt;

    public RoundSurveyDeadlineEvent(int roundId, TimeSpan closesAt)
    {
        RoundId = roundId;
        ClosesAt = closesAt;
    }
}

[Serializable, NetSerializable]
public sealed class RoundSurveyAnswerEvent : EntityEventArgs
{
    public int RoundId;
    public ProtoId<RoundSurveyQuestionPrototype> Question;
    public int Value;

    public RoundSurveyAnswerEvent(int roundId, ProtoId<RoundSurveyQuestionPrototype> question, int value)
    {
        RoundId = roundId;
        Question = question;
        Value = value;
    }
}

/// <summary>
/// Everything the player has ticked under the answer they gave to a question.
/// </summary>
[Serializable, NetSerializable]
public sealed class RoundSurveyReasonsEvent : EntityEventArgs
{
    public int RoundId;
    public ProtoId<RoundSurveyQuestionPrototype> Question;
    public List<ProtoId<RoundSurveyReasonPrototype>> Reasons;

    public RoundSurveyReasonsEvent(
        int roundId,
        ProtoId<RoundSurveyQuestionPrototype> question,
        List<ProtoId<RoundSurveyReasonPrototype>> reasons)
    {
        RoundId = roundId;
        Question = question;
        Reasons = reasons;
    }
}

[Serializable, NetSerializable]
public sealed class RoundSurveyCommentEvent : EntityEventArgs
{
    public const int MaxLength = 500;

    /// <summary>
    /// The round whose survey the comment was written in. Null if it was written outside of a survey.
    /// </summary>
    public int? RoundId;

    public ProtoId<RoundSurveyTopicPrototype> Topic;
    public string Text;

    public RoundSurveyCommentEvent(int? roundId, ProtoId<RoundSurveyTopicPrototype> topic, string text)
    {
        RoundId = roundId;
        Topic = topic;
        Text = text;
    }
}

/// <summary>
/// Tells the player whether the comment they have just sent was taken.
/// </summary>
[Serializable, NetSerializable]
public sealed class RoundSurveyCommentResultEvent : EntityEventArgs
{
    public bool Accepted;

    public RoundSurveyCommentResultEvent(bool accepted)
    {
        Accepted = accepted;
    }
}
