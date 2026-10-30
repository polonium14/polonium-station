using Content.Client.Eui;
using Content.Shared._Polonium.Objectives;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Polonium.Objectives;

[UsedImplicitly]
public sealed class AdminObjectivesEui : BaseEui
{
    private readonly AdminObjectivesWindow _window;

    public AdminObjectivesEui()
    {
        _window = new AdminObjectivesWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnAddStandard += prototype => SendMessage(new AdminObjectivesEuiMsg.AddStandard(prototype));
        _window.OnAddCustom += (title, description, issuer) =>
            SendMessage(new AdminObjectivesEuiMsg.AddCustom(title, description, issuer));
        _window.OnSetStatus += (objective, status) =>
            SendMessage(new AdminObjectivesEuiMsg.SetStatus(objective, status));
        _window.OnRemove += objective => SendMessage(new AdminObjectivesEuiMsg.Remove(objective));
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
        if (state is AdminObjectivesEuiState objectivesState)
            _window.SetState(objectivesState);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is AdminObjectivesEuiMsg.AddStandardFailed failed)
            _window.ShowAddStandardFailed(failed.Prototype);
    }
}
