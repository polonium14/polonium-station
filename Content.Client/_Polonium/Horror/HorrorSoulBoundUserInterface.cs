using Content.Client.UserInterface.Controls;
using Content.Shared._Polonium.Horror;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Horror;

/// <summary>
/// Radial pick of the soul to hunt, out of the few the horror senses.
/// </summary>
[UsedImplicitly]
public sealed class HorrorSoulBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private static readonly SpriteSpecifier SoulIcon =
        new SpriteSpecifier.Rsi(new ResPath("/Textures/_Polonium/Interface/Actions/horror.rsi"), "consume_soul");

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

        if (_menu == null || state is not HorrorSoulBoundUserInterfaceState cast)
            return;

        var buttons = new List<RadialMenuOptionBase>();
        foreach (var candidate in cast.Candidates)
        {
            var tooltip = candidate.Job == null
                ? candidate.Name
                : Loc.GetString("horror-seek-soul-option", ("name", candidate.Name), ("job", candidate.Job));

            buttons.Add(new RadialMenuActionOption<NetEntity>(mind => SendMessage(new HorrorPickSoulMessage(mind)), candidate.Mind)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(SoulIcon),
                ToolTip = tooltip,
            });
        }

        _menu.SetButtons(buttons);
    }
}
