using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Administration;

[Serializable, NetSerializable]
public sealed class TimeTransferPanelEuiState(bool hasFlag) : EuiStateBase
{
    public bool HasFlag { get; } = hasFlag;
}

[Serializable, NetSerializable]
public sealed class TimeTransferEuiMessage(string playerId, List<TimeTransferData> timeData, bool overwrite) : EuiMessageBase
{
    public string PlayerId { get; } = playerId;
    public List<TimeTransferData> TimeData { get; } = timeData;

    /// <summary>
    /// If true, the entered time replaces the stored one. Otherwise it is added to it.
    /// </summary>
    public bool Overwrite { get; } = overwrite;
}

[Serializable, NetSerializable]
public sealed class TimeTransferWarningEuiMessage(string message, Color warningColor) : EuiMessageBase
{
    public string Message { get; } = message;
    public Color WarningColor { get; } = warningColor;
}

/// <summary>
/// Time to apply to one play time tracker, written like <c>1d2h30m</c> or as plain minutes.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct TimeTransferData(string PlaytimeTracker, string TimeString);
