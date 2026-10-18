using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared._Polonium.Survey;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Survey;

public sealed class RoundSurveyTab : BoxContainer
{
    private readonly RoundSurveySystem _survey;
    private readonly int _roundId;
    private readonly TimeSpan _closeDelay;
    private readonly RichTextLabel _status = new();
    private readonly List<BaseButton> _answers = new();
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

        var proto = IoCManager.Resolve<IPrototypeManager>();

        foreach (var question in questions)
        {
            var followUps = new List<RoundSurveyFollowUpBox>();
            foreach (var followUp in question.FollowUps)
            {
                var box = new RoundSurveyFollowUpBox(followUp, proto);
                box.Changed += () => _survey.SetReasons(_roundId, question.ID, box.Picked);
                followUps.Add(box);
                _answers.AddRange(box.Options);
            }

            list.AddChild(new Label
            {
                Text = Loc.GetString(question.Text),
                StyleClasses = { StyleClass.LabelHeading },
                Margin = new Thickness(0, 16, 0, 4),
            });

            var buttons = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
            };

            var group = new ButtonGroup();
            for (var value = RoundSurveyQuestionPrototype.MinAnswer; value <= RoundSurveyQuestionPrototype.MaxAnswer; value++)
            {
                var low = value == RoundSurveyQuestionPrototype.MinAnswer;
                var high = value == RoundSurveyQuestionPrototype.MaxAnswer;
                if (question.YesNo && !low && !high)
                    continue;

                var answer = value;
                var button = new Button
                {
                    Text = question.YesNo ? Loc.GetString(high ? question.High : question.Low) : value.ToString(),
                    Group = group,
                    MinSize = new Vector2(question.YesNo ? 96 : 48, 0),
                };

                button.AddStyleClass(low ? StyleClass.ButtonOpenRight : high ? StyleClass.ButtonOpenLeft : StyleClass.ButtonOpenBoth);

                button.OnPressed += _ =>
                {
                    _survey.Answer(_roundId, question.ID, answer);
                    ShowFollowUp(followUps, question.GetFollowUp(answer));
                };

                buttons.AddChild(button);
                _answers.Add(button);
            }

            if (question.YesNo)
            {
                buttons.HorizontalAlignment = HAlignment.Left;
                list.AddChild(buttons);
                AddFollowUps(list, followUps);
                continue;
            }

            var ends = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 0),
            };

            ends.AddChild(new Label
            {
                Text = Loc.GetString(question.Low),
                StyleClasses = { StyleClass.LabelSubText },
            });

            ends.AddChild(new Control
            {
                HorizontalExpand = true,
                MinSize = new Vector2(16, 0),
            });

            ends.AddChild(new Label
            {
                Text = Loc.GetString(question.High),
                StyleClasses = { StyleClass.LabelSubText },
            });

            var scale = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalAlignment = HAlignment.Left,
            };

            scale.AddChild(buttons);
            scale.AddChild(ends);
            list.AddChild(scale);
            AddFollowUps(list, followUps);
        }

        var comment = new RoundSurveyCommentBox(survey, proto, _roundId);
        if (comment.Visible)
        {
            list.AddChild(new Label
            {
                Text = Loc.GetString("round-survey-comment-heading"),
                StyleClasses = { StyleClass.LabelHeading },
                Margin = new Thickness(0, 16, 0, 4),
            });

            list.AddChild(comment);
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

    private static void AddFollowUps(BoxContainer list, List<RoundSurveyFollowUpBox> followUps)
    {
        foreach (var box in followUps)
        {
            list.AddChild(box);
        }
    }

    // The server drops the reasons too once the answer calls for another follow-up.
    private static void ShowFollowUp(List<RoundSurveyFollowUpBox> followUps, RoundSurveyFollowUp? shown)
    {
        foreach (var box in followUps)
        {
            if (box.Visible == (box.FollowUp == shown))
                continue;

            box.Visible = box.FollowUp == shown;
            box.Clear();
        }
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
