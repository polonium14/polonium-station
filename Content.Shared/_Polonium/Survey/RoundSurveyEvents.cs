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
