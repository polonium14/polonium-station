// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <ozzeusz@gmail.com>
// SPDX-FileCopyrightText: 2026 github-actions[bot] <41898282+github-actions[bot]@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Polonium.CallablePhone;
using Content.Client._Polonium.CallablePhone.UI;

namespace Content.Client._Polonium.CallablePhone;

public sealed class CallablePhoneSystem : SharedCallablePhoneSystem
{
    private readonly Dictionary<NetEntity, CallablePhoneAdminChatWindow> _openAdminChatWindows = new();
    private readonly HashSet<NetEntity> _forceClosingAdminChats = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CallablePhoneAdminChatOpenEvent>(OnOpenAdminChat);
        SubscribeNetworkEvent<CallablePhoneAdminChatTextMessageEvent>(OnAdminChatMessage);
        SubscribeNetworkEvent<CallablePhoneAdminChatSetInputEnabledEvent>(OnAdminChatSetInputEnabled);
        SubscribeNetworkEvent<CallablePhoneAdminChatForceCloseEvent>(OnAdminChatForceClose);
        SubscribeNetworkEvent<CentCommCallPickupPromptEvent>(OnCentCommPickupPrompt);
    }

    private void OnOpenAdminChat(CallablePhoneAdminChatOpenEvent ev)
    {
        var window = EnsureAdminChatWindow(ev.Phone, ev.Title);
        window.SetImpersonationName(ev.InitialImpersonationName);
        window.OpenCentered();
        window.SetInputEnabled(ev.InputEnabled);
        window.FocusInput();
    }

    private CallablePhoneAdminChatWindow EnsureAdminChatWindow(NetEntity phone, string title)
    {
        if (_openAdminChatWindows.TryGetValue(phone, out var existing))
            return existing;

        var window = new CallablePhoneAdminChatWindow(phone, title);
        window.MessageSubmitted += OnAdminChatMessageSubmitted;
        window.ImpersonationNameSubmitted += OnAdminChatImpersonationNameSubmitted;
        window.WindowClosed += OnAdminChatWindowClosed;
        window.OnClose += () => _openAdminChatWindows.Remove(phone);

        _openAdminChatWindows[phone] = window;
        return window;
    }

    private void OnAdminChatMessage(CallablePhoneAdminChatTextMessageEvent ev)
    {
        if (!_openAdminChatWindows.TryGetValue(ev.Phone, out var window))
            return;

        window.ReceiveMessage(ev);
    }

    private void OnAdminChatSetInputEnabled(CallablePhoneAdminChatSetInputEnabledEvent ev)
    {
        if (!_openAdminChatWindows.TryGetValue(ev.Phone, out var window))
            return;

        window.SetInputEnabled(ev.Enabled);
    }

    private void OnCentCommPickupPrompt(CentCommCallPickupPromptEvent ev)
    {
        var message = Loc.GetString(
            "callable-phone-centcomm-pickup-message",
            ("caller", ev.CallerName));

        var window = new CallablePhoneAdminCallPickupWindow(message);
        window.Accepted += () => RaiseNetworkEvent(new CentCommCallPickupResponseEvent(ev.Phone, true));
        window.Declined += reason => RaiseNetworkEvent(new CentCommCallPickupResponseEvent(ev.Phone, false, reason));
        window.OpenCentered();
    }

    private void OnAdminChatMessageSubmitted(NetEntity phone, string message)
    {
        RaiseNetworkEvent(new CallablePhoneAdminChatSendMessageEvent(phone, message));
    }

    private void OnAdminChatImpersonationNameSubmitted(NetEntity phone, string name)
    {
        RaiseNetworkEvent(new CallablePhoneAdminChatSetImpersonationNameEvent(phone, name));
    }

    private void OnAdminChatForceClose(CallablePhoneAdminChatForceCloseEvent ev)
    {
        if (!_openAdminChatWindows.TryGetValue(ev.Phone, out var window))
            return;

        _forceClosingAdminChats.Add(ev.Phone);
        window.Close();
        _openAdminChatWindows.Remove(ev.Phone);
    }

    private void OnAdminChatWindowClosed(NetEntity phone)
    {
        if (_forceClosingAdminChats.Remove(phone))
            return;

        RaiseNetworkEvent(new CallablePhoneAdminChatCloseEvent(phone));
    }
}
