using Content.Shared._Polonium.Horror;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Horror;

[UsedImplicitly]
public sealed class HorrorMutationsBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private HorrorMutationsWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<HorrorMutationsWindow>();
        _window.OnBuy += (id, upgrade) => SendMessage(new HorrorBuyMutationMessage(id, upgrade));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is HorrorMutationsBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
