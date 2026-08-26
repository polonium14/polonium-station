// SPDX-FileCopyrightText: 2025 beck-thompson <107373427+beck-thompson@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._DV.CustomObjectiveSummary;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Network;

namespace Content.Client._DV.CustomObjectiveSummary;

public sealed partial class CustomObjectiveSummaryUIController : UIController
{
    [Dependency] private IClientNetManager _net = default!;

    private CustomObjectiveSummaryWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        // Client must register a NetMessage it sends so ClientSendMessage can resolve its id.
        _net.RegisterNetMessage<CustomObjectiveClientSetObjective>();
        SubscribeNetworkEvent<CustomObjectiveSummaryOpenMessage>(OnCustomObjectiveSummaryOpen);
    }

    private void OnCustomObjectiveSummaryOpen(CustomObjectiveSummaryOpenMessage msg, EntitySessionEventArgs args)
    {
        OpenWindow();
    }

    public void OpenWindow()
    {
        // If a window is already open, close it
        _window?.Close();

        _window = new CustomObjectiveSummaryWindow();
        _window.OpenCentered();
        _window.OnClose += () => _window = null;
        _window.OnSubmitted += OnFeedbackSubmitted;
    }

    private void OnFeedbackSubmitted(string args)
    {
        var msg = new CustomObjectiveClientSetObjective
        {
            Summary = args,
        };
        _net.ClientSendMessage(msg);
        _window?.Close();
    }
}
