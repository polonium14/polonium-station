using System.Linq;
using System.Numerics;
using Content.Client.Message;
using Content.Shared._Polonium.Replicator;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Replicator;

public sealed partial class ReplicatorEvolutionWindow : DefaultWindow
{
    private static readonly string[] Numerals = { "I", "II", "III", "IV", "V" };

    private static readonly Color GrownColor = Color.FromHex("#d70aa0");
    private static readonly Color NextColor = Color.FromHex("#ffff91");
    private static readonly Color LockedColor = Color.FromHex("#756d87");
    private static readonly Color MutedColor = Color.FromHex("#a6a1b5");
    private static readonly Color RowColor = Color.FromHex("#24121f");
    private static readonly Color PipColor = Color.FromHex("#432b3c");
    private static readonly Color DimColor = Color.White.WithAlpha(0.3f);

    private readonly IEntityManager _entity;
    private readonly IPlayerManager _player;
    private readonly IPrototypeManager _prototype;
    private readonly ReplicatorModuleSystem _modules;
    private readonly SpriteSystem _sprite;

    private readonly SpriteView _preview;
    private readonly Label _class;
    private readonly Label _stock;
    private readonly Label _level;
    private readonly Label _grown;
    private readonly BoxContainer _forms;
    private readonly BoxContainer _rows;

    private int _budget;
    private int _nestLevel;
    private int _signature;

    public event Action<ProtoId<ReplicatorModulePrototype>>? OnGrow;
    public event Action<ProtoId<ReplicatorClassPrototype>>? OnSpecialize;

    public ReplicatorEvolutionWindow()
    {
        _entity = IoCManager.Resolve<IEntityManager>();
        _player = IoCManager.Resolve<IPlayerManager>();
        _prototype = IoCManager.Resolve<IPrototypeManager>();
        _modules = _entity.System<ReplicatorModuleSystem>();
        _sprite = _entity.System<SpriteSystem>();

        Title = Loc.GetString("replicator-evolution-title");
        SetSize = new Vector2(900, 640);
        MinSize = new Vector2(640, 360);

        _preview = new SpriteView
        {
            OverrideDirection = Direction.South,
            Scale = new Vector2(3, 3),
            SetSize = new Vector2(112, 112),
        };

        _class = new Label { StyleClasses = { "LabelKeyText" } };
        _stock = new Label();
        _level = new Label();
        _grown = new Label();

        var summary = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            Children = { _class, _stock, _level, _grown },
        };

        _forms = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            VerticalAlignment = VAlignment.Center,
        };

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 12,
            Children = { _preview, summary, _forms },
        };

        _rows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = false,
            Children = { _rows },
        };

        Contents.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Children = { header, scroll },
        });

        Rebuild();
    }

    public void SetHive(int budget, int nestLevel)
    {
        _budget = budget;
        _nestLevel = nestLevel;
        Rebuild();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (GetSignature() != _signature)
            Rebuild();
    }

    private int GetSignature()
    {
        var hash = new HashCode();
        hash.Add(_player.LocalEntity);

        if (_entity.TryGetComponent<ReplicatorModulesComponent>(_player.LocalEntity, out var modules))
        {
            hash.Add(modules.Class);

            foreach (var (id, stage) in modules.Stages)
            {
                hash.Add(id);
                hash.Add(stage);
            }
        }

        return hash.ToHashCode();
    }

    private void Rebuild()
    {
        _signature = GetSignature();
        _rows.RemoveAllChildren();
        _forms.RemoveAllChildren();

        if (_player.LocalEntity is not { } user || !_entity.TryGetComponent<ReplicatorModulesComponent>(user, out var modules))
            return;

        _preview.SetEntity(user);
        _stock.Text = Loc.GetString("replicator-evolution-stock", ("budget", ReplicatorHiveSystem.ToSheets(_budget)));
        _level.Text = Loc.GetString("replicator-evolution-level", ("level", _nestLevel));

        if (!_modules.TryGetClass((user, modules), out var prototype))
        {
            _class.Text = Loc.GetString("replicator-evolution-no-class");
            _grown.Visible = false;

            var hint = new RichTextLabel { HorizontalExpand = true };
            hint.SetMarkupPermissive(Loc.GetString("replicator-evolution-choose"));
            _rows.AddChild(hint);

            foreach (var candidate in _prototype.EnumeratePrototypes<ReplicatorClassPrototype>().OrderBy(c => c.Order).ThenBy(c => c.ID))
            {
                _rows.AddChild(BuildClassRow(candidate));
            }

            return;
        }

        var total = _modules.GetTotalStages(modules);
        _class.Text = Loc.GetString("replicator-evolution-class", ("class", Loc.GetString(prototype.Name)));
        _grown.Visible = true;
        _grown.Text = Loc.GetString("replicator-evolution-grown", ("grown", total), ("limit", modules.MaxStages));

        for (var i = 0; i < prototype.Forms.Count; i++)
        {
            _forms.AddChild(BuildForm(prototype, i, prototype.Forms[i].Stages > total));
        }

        foreach (var id in prototype.Modules)
        {
            if (_prototype.TryIndex(id, out var module))
                _rows.AddChild(BuildRow(module, _modules.GetStage((user, modules), id), total, modules.MaxStages));
        }
    }

    private Control BuildForm(ReplicatorClassPrototype prototype, int index, bool dim)
    {
        var form = prototype.Forms[index];
        return new TextureRect
        {
            Texture = _sprite.Frame0(new SpriteSpecifier.Rsi(prototype.Sprite, form.State)),
            TextureScale = new Vector2(2, 2),
            Stretch = TextureRect.StretchMode.KeepCentered,
            MinSize = new Vector2(64, 64),
            VerticalAlignment = VAlignment.Center,
            Modulate = dim ? DimColor : Color.White,
            MouseFilter = MouseFilterMode.Pass,
            ToolTip = Loc.GetString("replicator-evolution-form", ("number", index + 1), ("stages", form.Stages)),
        };
    }

    private Control BuildClassRow(ReplicatorClassPrototype candidate)
    {
        var forms = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            VerticalAlignment = VAlignment.Center,
        };

        for (var i = 0; i < candidate.Forms.Count; i++)
        {
            forms.AddChild(BuildForm(candidate, i, false));
        }

        var branches = string.Join(", ",
            candidate.Modules.Select(branch => _prototype.TryIndex(branch, out var module) ? Loc.GetString(module.Name) : branch.Id));

        var description = new RichTextLabel { HorizontalExpand = true };
        description.SetMarkupPermissive(
            $"{Loc.GetString(candidate.Description)}\n[color={MutedColor.ToHex()}]{Loc.GetString("replicator-evolution-branches", ("branches", branches))}[/color]");

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 2,
            Children =
            {
                new Label { Text = Loc.GetString(candidate.Name), StyleClasses = { "LabelKeyText" } },
                description,
            },
        };

        var button = new Button
        {
            MinSize = new Vector2(132, 0),
            VerticalAlignment = VAlignment.Center,
            Text = candidate.Cost > 0
                ? Loc.GetString("replicator-evolution-specialize-cost", ("cost", ReplicatorHiveSystem.ToSheets(candidate.Cost)))
                : Loc.GetString("replicator-evolution-specialize"),
        };

        if (_budget < candidate.Cost)
        {
            button.Disabled = true;
            button.ToolTip = Loc.GetString("replicator-construction-fail-budget", ("cost", ReplicatorHiveSystem.ToSheets(candidate.Cost)));
        }

        var id = candidate.ID;
        button.OnPressed += _ => OnSpecialize?.Invoke(id);

        return BuildPanel(forms, body, button);
    }

    private Control BuildRow(ReplicatorModulePrototype module, int current, int total, int limit)
    {
        var pips = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 3,
            VerticalAlignment = VAlignment.Center,
        };

        var description = new List<string>();
        for (var i = 0; i < module.Stages.Count; i++)
        {
            pips.AddChild(new PanelContainer
            {
                MinSize = new Vector2(14, 8),
                PanelOverride = new StyleBoxFlat { BackgroundColor = i < current ? GrownColor : PipColor },
            });

            var color = i < current ? GrownColor : i == current ? NextColor : LockedColor;
            var numeral = i < Numerals.Length ? Numerals[i] : (i + 1).ToString();
            description.Add($"[color={color.ToHex()}]{numeral}.[/color] {Loc.GetString(module.Stages[i].Description)}");
        }

        var title = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Children =
            {
                new Label { Text = Loc.GetString(module.Name), StyleClasses = { "LabelKeyText" } },
                pips,
            },
        };

        var stages = new RichTextLabel { HorizontalExpand = true };
        stages.SetMarkupPermissive(string.Join("\n", description));

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 2,
            Children = { title, stages },
        };

        var button = new Button
        {
            MinSize = new Vector2(132, 0),
            VerticalAlignment = VAlignment.Center,
        };

        if (current >= module.Stages.Count)
        {
            button.Text = Loc.GetString("replicator-evolution-complete");
            button.Disabled = true;
        }
        else
        {
            var next = module.Stages[current];
            button.Text = Loc.GetString("replicator-evolution-grow", ("cost", ReplicatorHiveSystem.ToSheets(next.Cost)));

            string? blocker = null;
            if (total >= limit)
                blocker = Loc.GetString("replicator-module-fail-full", ("limit", limit));
            else if (_nestLevel < next.MinNestLevel)
                blocker = Loc.GetString("replicator-build-fail-level", ("level", next.MinNestLevel));
            else if (_budget < next.Cost)
                blocker = Loc.GetString("replicator-construction-fail-budget", ("cost", ReplicatorHiveSystem.ToSheets(next.Cost)));

            button.Disabled = blocker != null;
            button.ToolTip = blocker;

            var id = module.ID;
            button.OnPressed += _ => OnGrow?.Invoke(id);
        }

        return BuildPanel(body, button);
    }

    private static Control BuildPanel(params Control[] children)
    {
        var row = new ButtonRow
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            HorizontalExpand = true,
        };

        foreach (var child in children)
        {
            row.AddChild(child);
        }

        return new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = RowColor,
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 6,
            },
            Children = { row },
        };
    }

    // A box offers each child only the width the ones before it left, so wrapped text would leave the button none.
    private sealed class ButtonRow : BoxContainer
    {
        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            if (ChildCount == 0)
                return Vector2.Zero;

            var button = GetChild(ChildCount - 1);
            button.Measure(availableSize);

            var desired = new Vector2(button.DesiredSize.X + (SeparationOverride ?? 0) * (ChildCount - 1), button.DesiredSize.Y);
            var width = Math.Max(0, availableSize.X - desired.X);

            for (var i = 0; i < ChildCount - 1; i++)
            {
                var child = GetChild(i);
                child.Measure(new Vector2(width, availableSize.Y));
                desired.X += child.DesiredSize.X;
                desired.Y = Math.Max(desired.Y, child.DesiredSize.Y);
                width = Math.Max(0, width - child.DesiredSize.X);
            }

            return desired;
        }
    }
}
