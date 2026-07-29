using Content.Client.UserInterface.Controls;
using Content.Shared._RMC14.Xenonids.Pheromones;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Xenonids.Pheromones;

[UsedImplicitly]
public sealed class XenoPheromonesBui : BoundUserInterface
{
    private static readonly ResPath PheromoneRsi = new("/Textures/_RMC14/Interface/xeno_pheromones.rsi");

    private SimpleRadialMenu? _menu;

    public XenoPheromonesBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
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
        var suffix = EntMan.TryGetComponent(Owner, out XenoPheromonesComponent? pheromones)
            ? pheromones.PheroSuffix
            : null;

        yield return MakeOption(XenoPheromones.Recovery, suffix);
        yield return MakeOption(XenoPheromones.Warding, suffix);
        yield return MakeOption(XenoPheromones.Frenzy, suffix);
    }

    private RadialMenuOptionBase MakeOption(XenoPheromones type, string? suffix)
    {
        var key = type.ToString().ToLowerInvariant();
        var state = string.IsNullOrEmpty(suffix) ? key : $"{key}_{suffix}";

        return new RadialMenuActionOption<XenoPheromones>(Select, type)
        {
            ToolTip = Loc.GetString($"cm-pheromones-{key}"),
            IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(PheromoneRsi, state)),
        };
    }

    private void Select(XenoPheromones pheromones)
    {
        SendPredictedMessage(new XenoPheromonesChosenBuiMsg(pheromones));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _menu = null;
    }
}
