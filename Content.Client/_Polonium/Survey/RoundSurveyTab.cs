using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared._Polonium.Survey;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Survey;

public sealed class RoundSurveyTab : BoxContainer
{
    private readonly RoundSurveySystem _survey;
    private readonly int _roundId;
    private readonly TimeSpan _closeDelay;
    private readonly RichTextLabel _status = new();
    private readonly List<Button> _answers = new();
    private string _statusText = string.Empty;

    public RoundSurveyTab(RoundSurveyOfferEvent offer, IEnumerable<RoundSurveyQuestionPrototype> questions, RoundSurveySystem survey)
    {
        _survey = survey;
        _roundId = offer.RoundId;
        _closeDelay = offer.CloseDelay;

        Orientation = LayoutOrientation.Vertical;
        TabContainer.SetTabTitle(this, Loc.GetString("round-survey-tab-title"));

        var list = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
        };

        var intro = new RichTextLabel();
        intro.SetMessage(Loc.GetString("round-survey-intro"));
        list.AddChild(intro);

        _status.Margin = new Thickness(0, 8, 0, 0);
        list.AddChild(_status);

        foreach (var question in questions)
        {
            list.AddChild(new Label
            {
                Text = Loc.GetString(question.Text),
                StyleClasses = { StyleClass.LabelHeading },
                Margin = new Thickness(0, 16, 0, 4),
            });

            var scale = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
            };

            scale.AddChild(new Label
            {
                Text = Loc.GetString(question.Low),
                StyleClasses = { StyleClass.LabelSubText },
                Margin = new Thickness(0, 0, 8, 0),
            });

            var group = new ButtonGroup();
            for (var value = RoundSurveyQuestionPrototype.MinAnswer; value <= RoundSurveyQuestionPrototype.MaxAnswer; value++)
            {
                var answer = value;
                var button = new Button
                {
                    Text = value.ToString(),
                    Group = group,
                    MinSize = new Vector2(44, 0),
                };

                button.OnPressed += _ => _survey.Answer(_roundId, question.ID, answer);
                scale.AddChild(button);
                _answers.Add(button);
            }

            scale.AddChild(new Label
            {
                Text = Loc.GetString(question.High),
                StyleClasses = { StyleClass.LabelSubText },
                Margin = new Thickness(8, 0, 0, 0),
            });

            list.AddChild(scale);
        }

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            Margin = new Thickness(10),
        };

        scroll.AddChild(list);
        AddChild(scroll);

        UpdateStatus();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var open = _survey.IsOpen(_roundId);
        SetStatus(open ? DescribeTimeLeft() : Loc.GetString("round-survey-closed"));

        if (open)
            return;

        foreach (var button in _answers)
        {
            button.Disabled = true;
        }
    }

    private string DescribeTimeLeft()
    {
        if (_survey.GetTimeLeft(_roundId, out var exact) is { } left)
            return Loc.GetString(exact ? "round-survey-time-left" : "round-survey-time-left-about", ("time", Clock(left)));

        return _closeDelay > TimeSpan.Zero
            ? Loc.GetString("round-survey-time-left-unknown", ("delay", Clock(_closeDelay)))
            : Loc.GetString("round-survey-time-left-unknown-no-delay");
    }

    private void SetStatus(string text)
    {
        if (text == _statusText)
            return;

        _statusText = text;
        _status.SetMessage(text);
    }

    private static string Clock(TimeSpan time)
    {
        return $"{(int) time.TotalMinutes}:{time.Seconds:00}";
    }
}
