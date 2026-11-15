using Content.Client.Eui;
using Content.Shared._Polonium.Body;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Polonium.Body;

[UsedImplicitly]
public sealed class BodyDebugEui : BaseEui
{
    private readonly BodyDebugWindow _window;

    public BodyDebugEui()
    {
        _window = new BodyDebugWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnApply += changes => SendMessage(new BodyDebugEuiMsg.Apply(changes));
        _window.OnAction += (action, target) => SendMessage(new BodyDebugEuiMsg.Act(action, target));
        _window.OnInsert += category => SendMessage(new BodyDebugEuiMsg.Insert(category));
        _window.OnAddWound += (limb, type, amount) => SendMessage(new BodyDebugEuiMsg.AddWound(limb, type, amount));
        _window.OnRefresh += () => SendMessage(new BodyDebugEuiMsg.Refresh());
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is BodyDebugEuiState bodyState)
            _window.SetState(bodyState);
    }
}
