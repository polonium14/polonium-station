// SPDX-FileCopyrightText: 2026 haze
//
// SPDX-License-Identifier: MIT

using Content.Client._Polonium.Prayer.UI;
using Content.Shared._Polonium.CallablePhone;
using Content.Shared._Polonium.Prayer;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Prayer;

public sealed class PrayerSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private readonly Dictionary<NetEntity, CentCommChatWindow> _openWindows = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<OpenPrayableChatEvent>(OnOpenChat);
        SubscribeNetworkEvent<PrayableChatTextMessageEvent>(OnChatMessage);
        SubscribeNetworkEvent<PrayableChatSetInputEnabledEvent>(OnSetInputEnabled);
        SubscribeNetworkEvent<CentCommCallPickupPromptEvent>(OnCentCommPickupPrompt);
    }

    private void OnOpenChat(OpenPrayableChatEvent ev)
    {
        if (_openWindows.TryGetValue(ev.Entity, out var existing))
        {
            _uiManager.WindowRoot.AddChild(existing);
            existing.OpenCentered();
            existing.SetInputEnabled(ev.InputEnabled);
            existing.FocusInput();
            return;
        }

        var title = string.IsNullOrEmpty(ev.DeviceName)
            ? ev.Prefix
            : Loc.GetString(
                "prayer-device-chat-window-title",
                ("prefix", ev.Prefix),
                ("device", ev.DeviceName));

        var window = new CentCommChatWindow(ev.Entity, title, ev.AllowImpersonation);
        window.MessageSubmitted += OnMessageSubmitted;
        window.ImpersonationNameSubmitted += OnImpersonationNameSubmitted;
        window.WindowClosed += OnWindowClosed;
        window.OnClose += () => _openWindows.Remove(ev.Entity);
        window.SetInputEnabled(ev.InputEnabled);

        _openWindows[ev.Entity] = window;
        window.OpenCentered();
        window.FocusInput();
    }

    private void OnChatMessage(PrayableChatTextMessageEvent ev)
    {
        if (!_openWindows.TryGetValue(ev.Entity, out var window))
            return;

        window.ReceiveMessage(ev);
    }

    private void OnSetInputEnabled(PrayableChatSetInputEnabledEvent ev)
    {
        if (!_openWindows.TryGetValue(ev.Entity, out var window))
            return;

        window.SetInputEnabled(ev.Enabled);
    }

    private void OnCentCommPickupPrompt(CentCommCallPickupPromptEvent ev)
    {
        var message = Loc.GetString(
            "callable-phone-centcomm-pickup-message",
            ("caller", ev.CallerName));

        var window = new CentCommCallPickupWindow(message);
        window.Accepted += () => RaiseNetworkEvent(new CentCommCallPickupResponseEvent(ev.Phone, true));
        window.Declined += () => RaiseNetworkEvent(new CentCommCallPickupResponseEvent(ev.Phone, false));
        window.OpenCentered();
    }

    private void OnMessageSubmitted(NetEntity entity, string message)
    {
        RaiseNetworkEvent(new PrayableChatSendMessageEvent(entity, message));
    }

    private void OnImpersonationNameSubmitted(NetEntity entity, string name)
    {
        RaiseNetworkEvent(new PrayableChatSetImpersonationNameEvent(entity, name));
    }

    private void OnWindowClosed(NetEntity entity)
    {
        RaiseNetworkEvent(new PrayableChatCloseEvent(entity));
    }
}
