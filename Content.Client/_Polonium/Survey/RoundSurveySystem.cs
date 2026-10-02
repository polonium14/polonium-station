using Content.Client.GameTicking.Managers;
using Content.Shared._Polonium.Survey;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Survey;

public sealed partial class RoundSurveySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ClientGameTicker _ticker = default!;

    public RoundSurveyOfferEvent? Offer { get; private set; }

    public event Action? OfferReceived;

    private TimeSpan? _closesAt;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RoundSurveyOfferEvent>(OnOffer);
        SubscribeNetworkEvent<RoundSurveyDeadlineEvent>(OnDeadline);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        Offer = null;
        _closesAt = null;
    }

    private void OnOffer(RoundSurveyOfferEvent ev)
    {
        Offer = ev;
        _closesAt = null;
        OfferReceived?.Invoke();
    }

    private void OnDeadline(RoundSurveyDeadlineEvent ev)
    {
        if (Offer?.RoundId == ev.RoundId)
            _closesAt = ev.ClosesAt;
    }

    public bool IsOpen(int roundId)
    {
        return Offer?.RoundId == roundId && (_closesAt == null || _timing.CurTime < _closesAt);
    }

    /// <summary>
    /// How long the survey of this round stays open. Before the next round starts this is only an estimate,
    /// and there is none at all while nothing tells when that round will start.
    /// </summary>
    public TimeSpan? GetTimeLeft(int roundId, out bool exact)
    {
        exact = false;

        if (Offer is not { } offer || offer.RoundId != roundId)
            return null;

        var now = _timing.CurTime;
        if (_closesAt is { } closesAt)
        {
            exact = true;
            return closesAt > now ? closesAt - now : TimeSpan.Zero;
        }

        TimeSpan start;
        if (_ticker.IsGameStarted)
            start = offer.ExpectedStart;
        else if (!_ticker.Paused)
            start = _ticker.StartTime;
        else
            return null;

        return start > now ? start - now + offer.CloseDelay : null;
    }

    public void Answer(int roundId, ProtoId<RoundSurveyQuestionPrototype> question, int value)
    {
        if (IsOpen(roundId))
            RaiseNetworkEvent(new RoundSurveyAnswerEvent(roundId, question, value));
    }
}
