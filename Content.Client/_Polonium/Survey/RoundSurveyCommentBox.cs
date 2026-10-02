using System.Linq;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared._Polonium.Survey;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Survey;

/// <summary>
/// Lets the player pick a topic and write a few words about it.
/// </summary>
public sealed class RoundSurveyCommentBox : BoxContainer
{
    private readonly RoundSurveySystem _survey;
    private readonly int? _roundId;
    private readonly List<RoundSurveyTopicPrototype> _topics;
    private readonly OptionButton _topic = new();
    private readonly TextEdit _text = new();
    private readonly Button _send = new();
    private readonly Label _status = new();
    private bool _waiting;

    /// <param name="roundId">The round whose survey the box is a part of, if any.</param>
    public RoundSurveyCommentBox(RoundSurveySystem survey, IPrototypeManager proto, int? roundId)
    {
        _survey = survey;
        _roundId = roundId;
        _topics = proto.EnumeratePrototypes<RoundSurveyTopicPrototype>()
            .OrderBy(topic => topic.Order)
            .ThenBy(topic => topic.ID)
            .ToList();

        Orientation = LayoutOrientation.Vertical;
        Visible = _topics.Count > 0;

        for (var i = 0; i < _topics.Count; i++)
        {
            _topic.AddItem(Loc.GetString(_topics[i].Name), i);
        }

        _topic.HorizontalAlignment = HAlignment.Left;
        _topic.OnItemSelected += args => _topic.SelectId(args.Id);
        AddChild(_topic);

        _text.MinSize = new Vector2(0, 90);
        _text.Margin = new Thickness(6, 4);
        _text.Placeholder = new Rope.Leaf(Loc.GetString("round-survey-comment-placeholder"));
        _text.OnTextChanged += _ => ShowLength();

        var field = new PanelContainer
        {
            StyleClasses = { StyleClass.PanelDark },
            Margin = new Thickness(0, 6),
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        field.AddChild(_text);
        AddChild(field);

        _send.Text = Loc.GetString("round-survey-comment-send");
        _send.OnPressed += _ => Send();

        _status.StyleClasses.Add(StyleClass.LabelSubText);
        _status.Margin = new Thickness(8, 0, 0, 0);

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
        };

        row.AddChild(_send);
        row.AddChild(_status);
        AddChild(row);

        ShowLength();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _survey.CommentResult += OnResult;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _survey.CommentResult -= OnResult;
    }

    private void ShowLength()
    {
        var length = Rope.CalcTotalLength(_text.TextRope);

        _status.Text = Loc.GetString("round-survey-comment-length",
            ("length", length.ToString()),
            ("max", RoundSurveyCommentEvent.MaxLength.ToString()));

        _send.Disabled = _waiting || length == 0 || length > RoundSurveyCommentEvent.MaxLength;
    }

    private void Send()
    {
        var text = Rope.Collapse(_text.TextRope).Trim();
        if (_waiting || text.Length == 0 || text.Length > RoundSurveyCommentEvent.MaxLength)
            return;

        _waiting = true;
        _send.Disabled = true;
        _survey.Comment(_roundId, _topics[_topic.SelectedId].ID, text);
    }

    private void OnResult(bool accepted)
    {
        if (!_waiting)
            return;

        _waiting = false;

        if (accepted)
            _text.TextRope = Rope.Leaf.Empty;

        ShowLength();
        _status.Text = Loc.GetString(accepted ? "round-survey-comment-sent" : "round-survey-comment-rejected");
    }
}
