using Content.Shared._Polonium.Horror;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Horror;

[UsedImplicitly]
public sealed class HorrorChemicalsBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private HorrorChemicalsWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<HorrorChemicalsWindow>();
        _window.OnInject += chemical => SendMessage(new HorrorInjectChemicalMessage(chemical));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is HorrorChemicalsBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
