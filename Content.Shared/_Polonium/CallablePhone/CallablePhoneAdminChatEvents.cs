// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <ozzeusz@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.CallablePhone;

/// <summary>
/// Raised on a client to open a persistent admin chat window for speaking through a callable phone line.
/// </summary>
[Serializable, NetSerializable]
public sealed class CallablePhoneAdminChatOpenEvent(NetEntity phone, string title, bool inputEnabled = true) : EntityEventArgs
{
    public NetEntity Phone = phone;
    public string Title = title;
    public bool InputEnabled = inputEnabled;
}

/// <summary>
/// Raised by a client when an admin sends a message in a callable phone admin chat window.
/// </summary>
[Serializable, NetSerializable]
public sealed class CallablePhoneAdminChatSendMessageEvent(NetEntity phone, string message) : EntityEventArgs
{
    public NetEntity Phone = phone;
    public string Message = message;
}

/// <summary>
/// Raised by a client when a callable phone admin chat window is closed.
/// </summary>
[Serializable, NetSerializable]
public sealed class CallablePhoneAdminChatCloseEvent(NetEntity phone) : EntityEventArgs
{
    public NetEntity Phone = phone;
}


[Serializable, NetSerializable]
public sealed class CallablePhoneAdminChatForceCloseEvent(NetEntity phone) : EntityEventArgs
{
    public NetEntity Phone = phone;
}

/// <summary>
/// Raised on clients watching a callable phone line to display chat history.
/// </summary>
[Serializable, NetSerializable]
public sealed class CallablePhoneAdminChatTextMessageEvent(
    NetEntity phone,
    string sender,
    string message,
    bool incoming,
    bool isLog = false) : EntityEventArgs
{
    public NetEntity Phone = phone;
    public string Sender = sender;
    public string Message = message;
    public bool Incoming = incoming;
    public bool IsLog = isLog;
}

/// <summary>
/// Raised on clients to enable or disable the chat input for a callable phone admin chat window.
/// </summary>
[Serializable, NetSerializable]
public sealed class CallablePhoneAdminChatSetInputEnabledEvent(NetEntity phone, bool enabled) : EntityEventArgs
{
    public NetEntity Phone = phone;
    public bool Enabled = enabled;
}

/// <summary>
/// Raised by a client to set the in-call display name for admin phone impersonation.
/// </summary>
[Serializable, NetSerializable]
public sealed class CallablePhoneAdminChatSetImpersonationNameEvent(NetEntity phone, string name) : EntityEventArgs
{
    public NetEntity Phone = phone;
    public string Name = name;
}

