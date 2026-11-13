using Content.Shared._Polonium.Breathalyzer;
using Content.Shared.IdentityManagement;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Breathalyzer;

[UsedImplicitly]
public sealed class BreathalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private BreathalyzerWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BreathalyzerWindow>();
        _window.OnPrint += () => SendMessage(new BreathalyzerPrintMessage());
        Update();
    }

    public override void Update()
    {
        if (_window == null || !EntMan.TryGetComponent(Owner, out BreathalyzerComponent? comp))
            return;

        _window.Title = Identity.Name(Owner, EntMan);
        _window.SetBreathalyzer((Owner, comp));
    }
}
