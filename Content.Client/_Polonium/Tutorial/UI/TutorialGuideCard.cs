using System.Numerics;
using Content.Client._Polonium.UserInterface;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Tutorial.UI;

/// <summary>
/// A card that sits beside a game window and walks the trainee through it: numbered steps with a tool
/// icon, the current one lit and explained, an optional legend, and a status box at the bottom for
/// what is happening right now. Same colors as the tutorial hint panel.
/// </summary>
public sealed class TutorialGuideCard : PanelContainer
{
    private static readonly Color Accent = Color.FromHex("#65B8E2");
    private static readonly Color Muted = Color.FromHex("#8A979D");
    private static readonly Color DoneColor = Color.FromHex("#7FC59A");
    private static readonly Color WarningColor = Color.FromHex("#FFB347");

    private const float Gap = 10f;

    private readonly BoxContainer _steps;
    private readonly BoxContainer _legend;
    private readonly PanelContainer _legendSeparator;
    private readonly RichTextLabel _status;
    private readonly RichTextLabel _progress;
    private readonly RichTextLabel _warning;
    private readonly List<StepRow> _rows = new();

    public TutorialGuideCard(IResourceCache cache, string title, string goal)
    {
        SetWidth = 310;
        MouseFilter = MouseFilterMode.Ignore;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#0E1A1FEE"),
            BorderColor = Accent,
            BorderThickness = new Thickness(2),
        };

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(14, 10, 14, 12),
            SeparationOverride = 8,
        };
        AddChild(box);

        box.AddChild(new Label
        {
            Text = title,
            FontOverride = cache.GetFont("/Fonts/NotoSansDisplay/NotoSansDisplay-Bold.ttf", 14),
            FontColorOverride = Accent,
        });
        box.AddChild(Text(goal, Muted));
        box.AddChild(Separator());

        box.AddChild(_steps = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
        });

        box.AddChild(_legendSeparator = Separator());
        box.AddChild(_legend = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        });
        _legendSeparator.Visible = false;
        _legend.Visible = false;

        var statusBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(10, 8),
            SeparationOverride = 4,
        };
        statusBox.AddChild(_status = new RichTextLabel());
        statusBox.AddChild(_progress = new RichTextLabel());
        statusBox.AddChild(_warning = new RichTextLabel());

        box.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Accent.WithAlpha(0.13f) },
            Children = { statusBox },
        });
    }

    /// <summary>Title and body come already localized, so they can carry arguments.</summary>
    public void AddStep(EntProtoId? icon, string title, string body)
    {
        var row = new StepRow(icon, title, body);
        _rows.Add(row);
        _steps.AddChild(row);
    }

    public void AddLegend(Texture texture, Color tint, Vector2 size, string text)
    {
        _legendSeparator.Visible = true;
        _legend.Visible = true;
        _legend.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Children =
            {
                new Control
                {
                    MinSize = new Vector2(20, 0),
                    Children =
                    {
                        new GlowTextureRect
                        {
                            Texture = texture,
                            Tint = tint,
                            SetSize = size,
                            Stretch = TextureRect.StretchMode.KeepAspectCentered,
                            HorizontalAlignment = HAlignment.Center,
                            VerticalAlignment = VAlignment.Center,
                        },
                    },
                },
                new BoxContainer
                {
                    HorizontalExpand = true,
                    VerticalAlignment = VAlignment.Center,
                    Children = { Text(text, Color.White) },
                },
            },
        });
    }

    /// <summary>Steps before the active one read as done, the ones after it wait. Past the end means all done.</summary>
    public void SetActiveStep(int active)
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].SetState(i < active ? RowState.Done : i == active ? RowState.Active : RowState.Waiting);
        }
    }

    public void SetStatus(string status, string? progress = null, string? warning = null)
    {
        SetText(_status, status, Color.White);

        _progress.Visible = progress != null;
        if (progress != null)
            SetText(_progress, progress, Muted);

        _warning.Visible = warning != null;
        if (warning != null)
            SetText(_warning, warning, WarningColor);
    }

    /// <summary>To the right of the window, or to the left when the right edge of the screen is too close.</summary>
    public void PlaceBeside(Control window)
    {
        if (Parent is not { } root)
            return;

        var pos = window.GlobalPosition - root.GlobalPosition;

        var x = pos.X + window.Size.X + Gap;
        if (x + Size.X > root.Size.X)
            x = pos.X - Size.X - Gap;

        var y = Math.Clamp(pos.Y, 0f, Math.Max(0f, root.Size.Y - Size.Y));
        LayoutContainer.SetPosition(this, new Vector2(Math.Max(0f, x), y));
    }

    private static RichTextLabel Text(string text, Color color)
    {
        var label = new RichTextLabel();
        SetText(label, text, color);
        return label;
    }

    private static void SetText(RichTextLabel label, string text, Color color)
    {
        label.SetMessage(FormattedMessage.FromMarkupPermissive($"[color={color.ToHex()}]{text}[/color]"));
    }

    private static PanelContainer Separator()
    {
        return new PanelContainer
        {
            MinSize = new Vector2(0, 1),
            PanelOverride = new StyleBoxFlat { BackgroundColor = Accent.WithAlpha(0.33f) },
        };
    }

    private enum RowState : byte
    {
        Waiting,
        Active,
        Done,
    }

    private sealed class StepRow : BoxContainer
    {
        private readonly PanelContainer _bar;
        private readonly RichTextLabel _title;
        private readonly RichTextLabel _body;
        private readonly string _titleText;

        public StepRow(EntProtoId? icon, string title, string body)
        {
            _titleText = title;
            Orientation = LayoutOrientation.Horizontal;
            SeparationOverride = 8;

            AddChild(_bar = new PanelContainer
            {
                MinSize = new Vector2(3, 0),
                PanelOverride = new StyleBoxFlat { BackgroundColor = Accent },
            });

            if (icon != null)
            {
                var view = new EntityPrototypeView
                {
                    SetSize = new Vector2(32, 32),
                    VerticalAlignment = VAlignment.Top,
                };
                view.SetPrototype(icon);
                AddChild(view);
            }
            else
            {
                AddChild(new Control { MinSize = new Vector2(32, 32) });
            }

            var texts = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
                SeparationOverride = 2,
            };
            texts.AddChild(_title = new RichTextLabel());
            texts.AddChild(_body = new RichTextLabel());
            SetText(_body, body, Color.White);
            AddChild(texts);
        }

        public void SetState(RowState state)
        {
            _bar.Modulate = state == RowState.Active ? Color.White : Color.Transparent;
            _body.Visible = state == RowState.Active;
            Modulate = state == RowState.Waiting ? Color.White.WithAlpha(0.55f) : Color.White;

            var title = state == RowState.Done
                ? Loc.GetString("tutorial-guide-done", ("step", _titleText))
                : _titleText;

            var color = state switch
            {
                RowState.Active => Accent,
                RowState.Done => DoneColor,
                _ => Muted,
            };
            SetText(_title, $"[bold]{title}[/bold]", color);
        }
    }
}
