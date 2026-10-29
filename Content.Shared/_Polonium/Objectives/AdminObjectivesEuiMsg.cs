using Content.Shared.Eui;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Objectives;

[Serializable, NetSerializable]
public enum CustomObjectiveStatus : byte
{
    Failed,
    Partial,
    Completed,
}

public static class CustomObjectives
{
    /// <summary>
    /// Custom objectives are only created with an admin-written title, never from the standard objective list.
    /// </summary>
    public static readonly EntProtoId Prototype = "PoloniumCustomObjective";

    public const int MaxTitleLength = 80;
    public const int MaxDescriptionLength = 400;
}

/// <param name="Issuer">Localized issuer name, may contain markup.</param>
/// <param name="Progress">Null if the objective failed to report its progress.</param>
/// <param name="CustomStatus">Null for objectives that are not custom.</param>
[Serializable, NetSerializable]
public readonly record struct AdminObjectiveEntry(
    NetEntity Objective,
    string Title,
    string Description,
    string Issuer,
    float? Progress,
    CustomObjectiveStatus? CustomStatus);

[Serializable, NetSerializable]
public sealed class AdminObjectivesEuiState(
    string playerName,
    List<AdminObjectiveEntry> objectives,
    bool hasMind) : EuiStateBase
{
    public readonly string PlayerName = playerName;
    public readonly List<AdminObjectiveEntry> Objectives = objectives;
    public readonly bool HasMind = hasMind;
}

public static class AdminObjectivesEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class AddStandard(string prototype) : EuiMessageBase
    {
        public readonly string Prototype = prototype;
    }

    /// <summary>
    /// Sent to the admin when a standard objective could not be assigned.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class AddStandardFailed(string prototype) : EuiMessageBase
    {
        public readonly string Prototype = prototype;
    }

    [Serializable, NetSerializable]
    public sealed class AddCustom(string title, string description, string issuer) : EuiMessageBase
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
