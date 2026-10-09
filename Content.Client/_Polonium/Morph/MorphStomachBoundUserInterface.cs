using Content.Client.UserInterface.Controls;
using Content.Shared._Polonium.Morph;
using Content.Shared.Devour.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Morph;

/// <summary>
/// Radial menu listing the morph's stomach, each entry opening the actions for that entity.
/// </summary>
[UsedImplicitly]
public sealed partial class MorphStomachBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        Update();
        _menu.OpenOverMouseScreenPosition();
    }

    public override void Update()
    {
        if (_menu == null)
            return;

        var containers = EntMan.System<SharedContainerSystem>();
        if (!containers.TryGetContainer(Owner, DevourerComponent.StomachContainerId, out var stomach))
            return;

        EntMan.TryGetComponent<MorphComponent>(Owner, out var morph);

        var buttons = new List<RadialMenuOptionBase>();
        foreach (var contained in stomach.ContainedEntities)
        {
            var target = EntMan.GetNetEntity(contained);
            var options = new List<RadialMenuOptionBase>
            {
                CreateOption(target, MorphStomachAction.Drop, "/Textures/Interface/VerbIcons/drop.svg.192dpi.png", "morph-stomach-drop"),
                CreateOption(target, MorphStomachAction.PrepareThrow, "/Textures/Interface/VerbIcons/eject.svg.192dpi.png", "morph-stomach-throw"),
                CreateOption(target, MorphStomachAction.Digest, "/Textures/Interface/VerbIcons/delete.svg.192dpi.png", "morph-stomach-digest"),
            };

            var tooltip = morph?.PreparedThrow == contained
                ? Loc.GetString("morph-stomach-entity-prepared", ("entity", contained))
                : Loc.GetString("morph-stomach-entity", ("entity", contained));

            buttons.Add(new RadialMenuNestedLayerOption(options)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(contained),
                ToolTip = tooltip,
            });
        }

        _menu.SetButtons(buttons);
    }

    private RadialMenuActionOption<NetEntity> CreateOption(NetEntity target, MorphStomachAction action, string icon, string tooltip)
    {
        return new RadialMenuActionOption<NetEntity>(t => SendMessage(new MorphStomachSelectMessage(t, action)), target)
        {
            IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Texture(new(icon))),
            ToolTip = Loc.GetString(tooltip),
        };
    }
}
