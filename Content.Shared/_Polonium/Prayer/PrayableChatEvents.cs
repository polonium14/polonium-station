// SPDX-FileCopyrightText: 2026 haze
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Prayer;

/// <summary>
/// Raised on a client to open a persistent chat window for speaking through a prayable device.
/// </summary>
[Serializable, NetSerializable]
public sealed class OpenPrayableChatEvent(NetEntity entity, string prefix, string deviceName, bool inputEnabled = true) : EntityEventArgs
{
    public NetEntity Entity = entity;
    public string Prefix = prefix;
    public string DeviceName = deviceName;
    public bool InputEnabled = inputEnabled;
}

/// <summary>
/// Raised by a client when the admin sends a message in a prayable chat window.
/// </summary>
[Serializable, NetSerializable]
public sealed class PrayableChatSendMessageEvent(NetEntity entity, string message) : EntityEventArgs
{
    public NetEntity Entity = entity;
    public string Message = message;
}

/// <summary>
/// Raised by a client when a prayable chat window is closed.
/// </summary>
[Serializable, NetSerializable]
public sealed class PrayableChatCloseEvent(NetEntity entity) : EntityEventArgs
{
    public NetEntity Entity = entity;
}

/// <summary>
/// Raised on clients watching a prayable device to display chat history.
/// </summary>
[Serializable, NetSerializable]
public sealed class PrayableChatTextMessageEvent(
    NetEntity entity,
    string sender,
    string message,
    bool incoming,
    bool isLog = false) : EntityEventArgs
{
    public NetEntity Entity = entity;
    public string Sender = sender;
    public string Message = message;
    public bool Incoming = incoming;
    public bool IsLog = isLog;
}

/// <summary>
/// Raised on clients to enable or disable the chat input for a prayable device window.
/// </summary>
[Serializable, NetSerializable]
public sealed class PrayableChatSetInputEnabledEvent(NetEntity entity, bool enabled) : EntityEventArgs
{
    public NetEntity Entity = entity;
    public bool Enabled = enabled;
}
