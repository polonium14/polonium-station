using Content.Client.Stylesheets.Palette;
using Content.Client.UserInterface.Controls;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Xenonids.Construction;

[UsedImplicitly]
public sealed partial class XenoChooseStructureBui : BoundUserInterface
{
    private static readonly Color SelectedOptionColor = Palettes.Green.Element.WithAlpha(128);
    private static readonly Color SelectedOptionHoverColor = Palettes.Green.HoveredElement.WithAlpha(128);

    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly SharedPopupSystem _popup;
    private readonly SharedXenoConstructionSystem _xenoConstruction;

    private SimpleRadialMenu? _menu;

    public XenoChooseStructureBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _popup = EntMan.System<SharedPopupSystem>();
        _xenoConstruction = EntMan.System<SharedXenoConstructionSystem>();
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        _menu.SetButtons(BuildOptions());
        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> BuildOptions()
    {
        var options = new List<RadialMenuOptionBase>();
        if (!EntMan.TryGetComponent(Owner, out XenoConstructionComponent? xeno))
            return options;

        var selected = xeno.SelectedStructure;

        foreach (var structureId in xeno.CanBuild)
        {
            if (!_prototype.TryIndex(structureId, out var structure))
                continue;

            var tooltip = structure.Name;
            if (_xenoConstruction.GetStructurePlasmaCost(structureId) is { } cost)
                tooltip = $"{tooltip} ({cost} plasma)";

            var isSelected = selected == structureId;
            options.Add(new RadialMenuActionOption<EntProtoId>(SelectStructure, structureId)
            {
                ToolTip = tooltip,
                IconSpecifier = RadialMenuIconSpecifier.With(structureId),
                BackgroundColor = isSelected ? SelectedOptionColor : null,
                HoverBackgroundColor = isSelected ? SelectedOptionHoverColor : null,
            });
        }

        return options;
    }

    private void SelectStructure(EntProtoId structureId)
    {
        // ui closes right away - predicted msgs can get dropped
        SendMessage(new XenoChooseStructureMessage(structureId));

        if (_player.LocalEntity is { } player &&
            _prototype.TryIndex(structureId, out var structure))
        {
            _popup.PopupClient(
                Loc.GetString("cm-xeno-construction-selected", ("structure", structure.Name)),
                Owner,
                player);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _menu = null;
    }
}
