using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Objectives;

[Serializable, NetSerializable]
public enum CustomObjectiveStatus : byte
{
    Failed,
    Partial,
    Completed,
}

public static class CustomObjectiveLimits
{
    public const int MaxTitleLength = 80;
    public const int MaxDescriptionLength = 400;
}

[Serializable, NetSerializable]
public readonly record struct CustomObjectiveEntry(
    NetEntity Objective,
    string Title,
    string Description,
    string Issuer,
    CustomObjectiveStatus Status);

[Serializable, NetSerializable]
public readonly record struct CustomObjectiveIssuer(string Id, string Name);

[Serializable, NetSerializable]
public sealed class CustomObjectivesEuiState(
    string playerName,
    List<CustomObjectiveEntry> objectives,
    List<CustomObjectiveIssuer> issuers,
    bool hasMind) : EuiStateBase
{
    public readonly string PlayerName = playerName;
    public readonly List<CustomObjectiveEntry> Objectives = objectives;
    public readonly List<CustomObjectiveIssuer> Issuers = issuers;
    public readonly bool HasMind = hasMind;
}

public static class CustomObjectivesEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class Add(string title, string description, string issuer) : EuiMessageBase
    {
        public readonly string Title = title;
        public readonly string Description = description;
        public readonly string Issuer = issuer;
    }

    [Serializable, NetSerializable]
    public sealed class SetStatus(NetEntity objective, CustomObjectiveStatus status) : EuiMessageBase
    {
        public readonly NetEntity Objective = objective;
        public readonly CustomObjectiveStatus Status = status;
    }

    [Serializable, NetSerializable]
    public sealed class Remove(NetEntity objective) : EuiMessageBase
    {
        public readonly NetEntity Objective = objective;
    }
}
