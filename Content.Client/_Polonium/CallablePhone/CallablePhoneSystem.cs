using Content.Shared._Polonium.CallablePhone;
using Content.Client._Polonium.CallablePhone.UI;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.CallablePhone;

public sealed class CallablePhoneSystem : SharedCallablePhoneSystem
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

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
        if (_openAdminChatWindows.TryGetValue(ev.Phone, out var existing))
        {
            _uiManager.WindowRoot.AddChild(existing);
            existing.OpenCentered();
            existing.SetInputEnabled(ev.InputEnabled);
            existing.FocusInput();
            return;
        }

        var window = new CallablePhoneAdminChatWindow(ev.Phone, ev.Title);
        window.MessageSubmitted += OnAdminChatMessageSubmitted;
        window.ImpersonationNameSubmitted += OnAdminChatImpersonationNameSubmitted;
        window.WindowClosed += OnAdminChatWindowClosed;
        window.OnClose += () => _openAdminChatWindows.Remove(ev.Phone);
        window.SetInputEnabled(ev.InputEnabled);

        _openAdminChatWindows[ev.Phone] = window;
        window.OpenCentered();
        window.FocusInput();
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
