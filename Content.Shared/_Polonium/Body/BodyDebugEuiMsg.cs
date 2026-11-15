using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared.Eui;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Body;

[Serializable, NetSerializable]
public enum BodyDebugOrganKind : byte
{
    Alive,
    Dead,
    Missing,
}

/// <summary>
/// One row of the body debug window. Everything is sent by value, because the admin can be out of PVS range.
/// </summary>
/// <param name="Organ">Null for a missing organ.</param>
/// <param name="HealthyFrom">Lowest integrity at which the organ is still healthy.</param>
/// <param name="Floor">Integrity never drops below this value.</param>
/// <param name="Protected">True for organs that can only have their integrity changed.</param>
[Serializable, NetSerializable]
public readonly record struct BodyDebugOrgan(
    NetEntity? Organ,
    string Category,
    string Name,
    BodyDebugOrganKind Kind,
    FixedPoint2 Integrity,
    FixedPoint2 Cap,
    FixedPoint2 HealthyFrom,
    FixedPoint2 Floor,
    OrganSeverity Severity,
    List<(string Name, FixedPoint2 Value)> Modifiers,
    bool Protected);

/// <param name="Error">Localization key of the reason the last action failed.</param>
[Serializable, NetSerializable]
public sealed class BodyDebugEuiState(string targetName, List<BodyDebugOrgan> organs, string? error) : EuiStateBase
{
    public readonly string TargetName = targetName;
    public readonly List<BodyDebugOrgan> Organs = organs;
    public readonly string? Error = error;
}

public static class BodyDebugEuiMsg
{
    /// <summary>
    /// Sets the integrity of the listed organs, in whole percents of their cap.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class Apply(List<(NetEntity Organ, int Percent)> changes) : EuiMessageBase
    {
        public readonly List<(NetEntity Organ, int Percent)> Changes = changes;
    }

    [Serializable, NetSerializable]
    public sealed class Kill(NetEntity organ) : EuiMessageBase
    {
        public readonly NetEntity Organ = organ;
    }

    [Serializable, NetSerializable]
    public sealed class Revive(NetEntity organ) : EuiMessageBase
    {
        public readonly NetEntity Organ = organ;
    }

    [Serializable, NetSerializable]
    public sealed class Remove(NetEntity organ) : EuiMessageBase
    {
        public readonly NetEntity Organ = organ;
    }

    [Serializable, NetSerializable]
    public sealed class Insert(string category) : EuiMessageBase
    {
        public readonly string Category = category;
    }

    [Serializable, NetSerializable]
    public sealed class Refresh : EuiMessageBase;
}
