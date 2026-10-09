using System.Linq;
using Content.Shared._Polonium.Survey;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Survey;

/// <summary>
/// Lets the player tick the reasons for an answer. Hidden until the answer calls for it.
/// </summary>
public sealed class RoundSurveyFollowUpBox : BoxContainer
{
    public readonly RoundSurveyFollowUp FollowUp;

    private readonly List<(ProtoId<RoundSurveyReasonPrototype> Reason, CheckBox Box)> _options = new();

    public event Action? Changed;

    public RoundSurveyFollowUpBox(RoundSurveyFollowUp followUp, IPrototypeManager proto)
    {
        FollowUp = followUp;

        Orientation = LayoutOrientation.Vertical;
        Margin = new Thickness(0, 8, 0, 0);
        Visible = false;

        AddChild(new Label
        {
            Text = Loc.GetString(followUp.Text),
        });

        var grid = new GridContainer
        {
            Columns = 2,
            HSeparationOverride = 16,
        };

        foreach (var id in followUp.Reasons)
        {
            if (!proto.TryIndex(id, out var reason))
                continue;

            var box = new CheckBox
            {
                Text = Loc.GetString(reason.Name),
            };

            box.OnToggled += _ => Changed?.Invoke();
            grid.AddChild(box);
            _options.Add((id, box));
        }

        AddChild(grid);
    }

    public IEnumerable<CheckBox> Options => _options.Select(option => option.Box);

    public List<ProtoId<RoundSurveyReasonPrototype>> Picked => _options
        .Where(option => option.Box.Pressed)
        .Select(option => option.Reason)
        .ToList();

    public void Clear()
    {
        foreach (var (_, box) in _options)
        {
            box.Pressed = false;
        }
    }
}
