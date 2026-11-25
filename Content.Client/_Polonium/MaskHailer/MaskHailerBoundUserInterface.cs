using Content.Client.UserInterface.Controls;
using Content.Shared._Polonium.MaskHailer;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.MaskHailer;

[UsedImplicitly]
public sealed partial class MaskHailerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<MaskHailerComponent>(Owner, out var hailer)
            || !_prototype.Resolve(hailer.Set, out var set))
            return;

        var options = new List<RadialMenuOptionBase>();
        for (var i = 0; i < set.Phrases.Count; i++)
        {
            var phrase = set.Phrases[i];
            options.Add(new RadialMenuActionOption<int>(SendPhrase, i)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(phrase.Icon),
                ToolTip = Loc.GetString(phrase.Text),
                Order = i,
            });
        }

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.SetButtons(options);
        _menu.OpenCentered();
    }

    private void SendPhrase(int index)
    {
        SendPredictedMessage(new MaskHailerPhraseMessage(index));
    }
}
