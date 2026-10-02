using Content.Client._Polonium.Survey;
using Content.Client.GameTicking.Managers;
using Content.Shared._Polonium.Survey;
using Content.Shared.GameTicking;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controllers;
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

    private RoundEndSummaryWindow? _window;

    // POLONIUM START
    private RoundSurveySystem? _survey;
    private RoundEndSummaryWindow? _surveyWindow;

    public void OnSystemLoaded(RoundSurveySystem system)
    {
        _survey = system;
        system.OfferReceived += AddSurveyTab;
    }

    public void OnSystemUnloaded(RoundSurveySystem system)
    {
        system.OfferReceived -= AddSurveyTab;
        _survey = null;
    }

    private void AddSurveyTab()
    {
        if (_window == null || _survey?.Offer is not { } offer)
            return;

        if (offer.RoundId != _window.RoundId || _window == _surveyWindow)
            return;

        var questions = new List<RoundSurveyQuestionPrototype>();
        foreach (var id in offer.Questions)
        {
            if (_proto.TryIndex(id, out var question))
                questions.Add(question);
        }

        if (questions.Count == 0)
            return;

        _surveyWindow = _window;
        _window.AddTab(new RoundSurveyTab(offer, questions, _survey));
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
        AddSurveyTab(); // Polonium
    }

    public void OnSystemLoaded(ClientGameTicker system)
    {
        _input.SetInputCommand(ContentKeyFunctions.ToggleRoundEndSummaryWindow,
            InputCmdHandler.FromDelegate(ToggleScoreboardWindow));
    }
}
