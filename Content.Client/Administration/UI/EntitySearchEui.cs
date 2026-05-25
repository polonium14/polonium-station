// SPDX-FileCopyrightText: 2025 Polonium Station Contributors
//
// SPDX-License-Identifier: MIT

using Content.Client.Administration.UI.Tabs.AdminTab;
using Content.Client.Eui;
using Content.Shared.Administration;
using Content.Shared.Bql;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Console;

namespace Content.Client.Administration.UI;

[UsedImplicitly]
public sealed class EntitySearchEui : BaseEui
{
    private readonly EntitySearchWindow _window;

    public EntitySearchEui()
    {
        _window = new EntitySearchWindow(
            IoCManager.Resolve<IClientConsoleHost>(),
            IoCManager.Resolve<ILocalizationManager>());

        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.SearchRequested += PerformSearch;
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
        if (state is not ToolshedVisualizeEuiState castState)
            return;

        _window.UpdateResults(castState.Entities);
    }

    private void PerformSearch()
    {
        SendMessage(new EntitySearchEuiMsg.Search { Query = _window.SearchText });
    }
}
