using Content.Client.Eui;
using Content.Shared._Polonium.Objectives;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Polonium.Objectives;

[UsedImplicitly]
public sealed class CustomObjectivesEui : BaseEui
{
    private readonly CustomObjectivesWindow _window;

    public CustomObjectivesEui()
    {
        _window = new CustomObjectivesWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnAdd += (title, description, issuer) =>
            SendMessage(new CustomObjectivesEuiMsg.Add(title, description, issuer));
        _window.OnSetStatus += (objective, status) =>
            SendMessage(new CustomObjectivesEuiMsg.SetStatus(objective, status));
        _window.OnRemove += objective => SendMessage(new CustomObjectivesEuiMsg.Remove(objective));
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
        if (state is CustomObjectivesEuiState objectivesState)
            _window.SetState(objectivesState);
    }
}
