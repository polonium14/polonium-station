using System.Numerics;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Survey;

/// <summary>
/// The window for writing a comment about the game at any moment, not only in the survey of a round.
/// </summary>
[UsedImplicitly]
public sealed partial class RoundSurveyCommentUIController : UIController, IOnSystemChanged<RoundSurveySystem>
{
    [Dependency] private IPrototypeManager _proto = default!;

    private RoundSurveySystem? _survey;
    private DefaultWindow? _window;

    public void OnSystemLoaded(RoundSurveySystem system)
    {
        _survey = system;
    }

    public void OnSystemUnloaded(RoundSurveySystem system)
    {
        _survey = null;
        _window?.Close();
        _window = null;
    }

    public void ToggleWindow()
    {
        if (_survey == null)
            return;

        if (_window == null)
        {
            _window = new DefaultWindow
            {
                Title = Loc.GetString("round-survey-comment-window-title"),
                MinSize = new Vector2(440, 260),
            };

            _window.Contents.AddChild(new RoundSurveyCommentBox(_survey, _proto, null)
            {
                Margin = new Thickness(8),
            });
        }

        if (_window.IsOpen)
            _window.Close();
        else
            _window.OpenCentered();
    }
}
