using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Shared._Polonium.MaskHailer;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client._Polonium.MaskHailer;

[UsedImplicitly]
public sealed partial class MaskHailerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;

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
            if (phrase.Variants.Count == 0)
                continue;

            options.Add(new RadialMenuActionOption<(int Index, int Variants)>(SendPhrase, (i, phrase.Variants.Count))
            {
                IconSpecifier = RadialMenuIconSpecifier.With(phrase.Icon),
                ToolTip = string.Join('\n', phrase.Variants.Select(variant => Loc.GetString(variant.Text))),
                Order = i,
            });
        }

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.SetButtons(options);
        _menu.OpenCentered();
    }

    private void SendPhrase((int Index, int Variants) phrase)
    {
        SendPredictedMessage(new MaskHailerPhraseMessage(phrase.Index, _random.Next(phrase.Variants)));
    }
}
