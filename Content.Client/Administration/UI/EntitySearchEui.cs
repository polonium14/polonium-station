using Content.Client.Administration.UI.Tabs.AdminTab;
using Content.Client.Eui;
using Content.Shared.Administration;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Shared.IoC;
using static Content.Shared.Administration.EntitySearchEuiMsg;

namespace Content.Client.Administration.UI;

[UsedImplicitly]
public sealed class EntitySearchEui : BaseEui
{
    [Dependency] private readonly IDependencyCollection _deps = default!;

    private readonly EntitySearchWindow _window;

    public EntitySearchEui()
    {
        _window = new EntitySearchWindow(_deps);

        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.SearchRequested += PerformSearch;
        _window.NextResultsRequested += RequestNextResults;
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

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not NewResults newResults)
            return;

        if (newResults.Replace)
            _window.SetResults(newResults.Entities, newResults.HasNext);
        else
            _window.AddResults(newResults.Entities, newResults.HasNext);
    }

    private void PerformSearch()
    {
        SendMessage(new Search { Query = _window.SearchText });
    }

    private void RequestNextResults()
    {
        _window.BeginNextResultsRequest();
        SendMessage(new NextResultsRequest());
    }
}
