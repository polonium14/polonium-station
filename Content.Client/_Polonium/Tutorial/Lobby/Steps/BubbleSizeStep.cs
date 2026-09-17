using Content.Client._Polonium.Tutorial.Lobby.UI;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client._Polonium.Tutorial.Lobby.Steps;

/// <summary>
/// First thing in the lobby: the player sizes the tutorial bubbles on their own screen. A sample
/// sits where the tour will put its bubbles, so they can see what it covers. No way around it.
/// </summary>
public sealed partial class BubbleSizeStep : ClientsideNavTutorialStep
{
    [Dependency] private IConfigurationManager _cfg = default!;

    public override string StepId => "bubble_size";

    private const float SliderStep = 0.05f;

    private LobbyGui _lobby = default!;
    private float _multiplier;
    private bool _confirmed;
    private Label? _percent;

    public override bool Execute()
    {
        if (StateMan.CurrentState is not LobbyState { Lobby: { } lobby })
            return false;

        _lobby = lobby;
        _confirmed = false;
        _multiplier = TutorialBubbleScale.Multiplier(_cfg);

        var overlay = TutorialUi.PlanOverlay(
            StepId,
            lobby.RightSide,
            Color.FromHex("#65B8E2"),
            highlightMargin: 4f,
            ignoreHighlightClicks: true);

        overlay.InternalOverlayClosedEvent += () =>
        {
            if (_confirmed && Tutorial.IsTutorialActive && Tutorial.ActiveStep?.StepId == StepId)
                Tutorial.NextStep();
        };

        TutorialUi.PlanBubble(MakeSample(), TutorialHighlightOverlay.OverlayControlPosition.CenterLeft, lobby.RightSide, overlayId: StepId);
        overlay.AddControlRelative(MakePanel(), TutorialHighlightOverlay.OverlayControlPosition.BottomLeft, 40f);
        return true;
    }

    public override bool CanExecute()
    {
        return StateMan.CurrentState is LobbyState;
    }

    private TutorialBubble MakeSample()
    {
        return new TutorialBubble(Loc.GetString("intro-bubble-size-sample"))
        {
            ClickAction = TutorialBubble.ClickBehaviour.Ignore,
            TippyVariant = TutorialBubble.Tippy.ClownRegular,
            ScaleMultiplier = _multiplier,
        };
    }

    private Control MakePanel()
    {
        var panel = new PanelContainer
        {
            MaxWidth = 460,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#0E1A1FEE"),
                BorderColor = Color.FromHex("#65B8E2"),
                BorderThickness = new Thickness(2),
                ContentMarginLeftOverride = 16f,
                ContentMarginTopOverride = 12f,
                ContentMarginRightOverride = 16f,
                ContentMarginBottomOverride = 12f,
            },
        };

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
        };
        panel.AddChild(column);

        column.AddChild(new RichTextLabel { Text = Loc.GetString("intro-bubble-size-title") });
        column.AddChild(new RichTextLabel { Text = Loc.GetString("intro-bubble-size-warning") });

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
        };
        column.AddChild(row);

        var slider = new Slider
        {
            MinValue = TutorialBubbleScale.MinMultiplier,
            MaxValue = TutorialBubbleScale.MaxMultiplier,
            HorizontalExpand = true,
            MinWidth = 200,
            VerticalAlignment = Control.VAlignment.Center,
        };
        slider.SetValueWithoutEvent(_multiplier);

        var smaller = new Button { Text = "−", MinWidth = 32 };
        var bigger = new Button { Text = "+", MinWidth = 32 };
        _percent = new Label { MinWidth = 48, Align = Label.AlignMode.Right };

        row.AddChild(smaller);
        row.AddChild(slider);
        row.AddChild(bigger);
        row.AddChild(_percent);

        smaller.OnPressed += _ => slider.Value = _multiplier - SliderStep;
        bigger.OnPressed += _ => slider.Value = _multiplier + SliderStep;
        slider.OnValueChanged += range => SetMultiplier(range.Value);

        var done = TutorialBubble.MakeButton(Loc.GetString("intro-bubble-size-done"));
        done.OnPressed += _ => Confirm();
        column.AddChild(done);

        UpdatePercent();
        return panel;
    }

    private void SetMultiplier(float value)
    {
        // snap to the step so a drag does not rebuild the sample for every pixel
        var snapped = MathF.Round(value / SliderStep) * SliderStep;
        snapped = Math.Clamp(snapped, TutorialBubbleScale.MinMultiplier, TutorialBubbleScale.MaxMultiplier);
        if (MathF.Abs(snapped - _multiplier) < 0.001f)
            return;

        _multiplier = snapped;
        UpdatePercent();
        TutorialUi.SwapBubble(MakeSample(), TutorialHighlightOverlay.OverlayControlPosition.CenterLeft, _lobby.RightSide);
    }

    private void UpdatePercent()
    {
        if (_percent != null)
            _percent.Text = $"{MathF.Round(_multiplier * 100f)}%";
    }

    private void Confirm()
    {
        if (_confirmed)
            return;

        _confirmed = true;
        _cfg.SetCVar(CCVars.TutorialBubbleScale, _multiplier);
        _cfg.SaveToFile();
        TutorialUi.RequestClose(false);
    }
}
