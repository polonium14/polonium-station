using System.Numerics;
using Content.Client._Polonium.Survey;
using Content.Client.GameTicking.Managers;
using Content.Shared._Polonium.Survey;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.RoundEnd;

[UsedImplicitly]
public sealed partial class RoundEndSummaryUIController : UIController,
    IOnSystemLoaded<ClientGameTicker>,
    IOnSystemChanged<RoundSurveySystem> // Polonium
{
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPrototypeManager _proto = default!; // Polonium
    [Dependency] private IConfigurationManager _cfg = default!; // Polonium

    private RoundEndSummaryWindow? _window;

    // POLONIUM START
    private const int MaxTabHighlights = 3;

    private RoundSurveySystem? _survey;
    private RoundSurveyTab? _surveyTab;
    private DefaultWindow? _surveyWindow;
    private int? _highlightedRound;

    public void OnSystemLoaded(RoundSurveySystem system)
    {
        _survey = system;
        system.OfferReceived += ShowSurvey;
    }

    public void OnSystemUnloaded(RoundSurveySystem system)
    {
        system.OfferReceived -= ShowSurvey;
        _survey = null;
        RemoveSurvey();
    }

    private void RemoveSurvey()
    {
        _surveyTab?.Orphan();
        _surveyTab = null;
        _surveyWindow?.Close();
        _surveyWindow = null;
    }

    private void ShowSurvey()
    {
        RemoveSurvey();

        if (_survey?.Offer is not { } offer)
            return;

        var questions = new List<RoundSurveyQuestionPrototype>();
        foreach (var id in offer.Questions)
        {
            if (_proto.TryIndex(id, out var question))
                questions.Add(question);
        }

        if (questions.Count == 0)
            return;

        _surveyTab = new RoundSurveyTab(offer, questions, _survey);
        if (_window?.RoundId == offer.RoundId)
        {
            _window.AddTab(_surveyTab, TryHighlight(offer.RoundId));
            return;
        }

        // Those who reconnect do not get the round end window again.
        _surveyWindow = new DefaultWindow
        {
            Title = Loc.GetString("round-survey-tab-title"),
            MinSize = new Vector2(520, 620),
        };

        _surveyWindow.Contents.AddChild(_surveyTab);
        _surveyWindow.OpenCentered();
    }

    private bool TryHighlight(int roundId)
    {
        if (_highlightedRound == roundId)
            return false;

        var shown = _cfg.GetCVar(CCVars.SurveyTabHighlights);
        if (shown >= MaxTabHighlights)
            return false;

        _highlightedRound = roundId;
        _cfg.SetCVar(CCVars.SurveyTabHighlights, shown + 1);
        _cfg.SaveToFile();
        return true;
    }
    // POLONIUM END

    private void ToggleScoreboardWindow(ICommonSession? session = null)
    {
        if (_window == null)
            return;

        if (_window.IsOpen)
        {
            _window.Close();
        }
        else
        {
            _window.OpenCenteredRight();
            _window.MoveToFront();
        }
    }

    public void OpenRoundEndSummaryWindow(RoundEndMessageEvent message)
    {
        // Don't open duplicate windows (mainly for replays).
        if (_window?.RoundId == message.RoundId)
            return;

        _window = new RoundEndSummaryWindow(message.GamemodeTitle, message.RoundEndText,
            message.RoundDuration, message.RoundId, message.AllPlayersEndInfo);
        // POLONIUM START
        if (_survey?.Offer?.RoundId == message.RoundId)
            ShowSurvey();
        // POLONIUM END
    }

    public void OnSystemLoaded(ClientGameTicker system)
    {
        _input.SetInputCommand(ContentKeyFunctions.ToggleRoundEndSummaryWindow,
            InputCmdHandler.FromDelegate(ToggleScoreboardWindow));
    }
}
