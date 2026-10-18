using Content.Client.UserInterface.Controls;
using Content.Shared._Polonium.Replicator;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Replicator;

[UsedImplicitly]
public sealed partial class ReplicatorBuildMenuBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private static readonly Color LockedBackground = Color.FromHex("#3a3a44").WithAlpha(0.6f);

    private readonly IPrototypeManager _prototype = IoCManager.Resolve<IPrototypeManager>();

    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.OpenOverMouseScreenPosition();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null || state is not ReplicatorBuildMenuState menu)
            return;

        var buttons = new List<RadialMenuOptionBase>();
        foreach (var entry in menu.Entries)
        {
            if (!_prototype.TryIndex(entry.Structure, out var structure) || !_prototype.TryIndex(structure.Entity, out var entity))
                continue;

            buttons.Add(new RadialMenuActionOption<ProtoId<ReplicatorStructurePrototype>>(Select, entry.Structure)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(structure.Entity),
                Order = structure.Order,
                BackgroundColor = entry.Unlocked ? null : LockedBackground,
                HoverBackgroundColor = entry.Unlocked ? null : LockedBackground,
                ToolTip = entry.Unlocked
                    ? Loc.GetString("replicator-build-menu-entry", ("structure", entity.Name), ("cost", entry.Cost))
                    : Loc.GetString("replicator-build-menu-locked", ("structure", entity.Name), ("level", structure.MinNestLevel)),
            });
        }

        _menu.SetButtons(buttons);
    }

    private void Select(ProtoId<ReplicatorStructurePrototype> structure)
    {
        SendMessage(new ReplicatorBuildSelectMessage(structure));
    }
}
