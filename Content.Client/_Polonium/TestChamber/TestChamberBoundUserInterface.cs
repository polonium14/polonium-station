using Content.Shared._Polonium.TestChamber;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.TestChamber;

[UsedImplicitly]
public sealed class TestChamberBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private TestChamberWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<TestChamberWindow>();
        _window.OnSpawnKit += kit => SendMessage(new TestChamberSpawnKitMessage(kit));
        _window.OnReset += () => SendMessage(new TestChamberResetMessage());
    }
}
