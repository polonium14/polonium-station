using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Horror;

public sealed partial class HorrorInfestActionEvent : EntityTargetActionEvent;
public sealed partial class HorrorLeaveHostActionEvent : InstantActionEvent;
public sealed partial class HorrorChemicalsMenuActionEvent : InstantActionEvent;
public sealed partial class HorrorScanHostActionEvent : InstantActionEvent;
public sealed partial class HorrorTakeControlActionEvent : InstantActionEvent;
public sealed partial class HorrorReleaseControlActionEvent : InstantActionEvent;
public sealed partial class HorrorResistControlActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class HorrorInfestDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class HorrorLeaveHostDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class HorrorBondDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Raised on a body before a horror crawls into it. Cancel it to keep the horror out.
/// </summary>
[ByRefEvent]
public record struct HorrorInfestAttemptEvent(EntityUid Horror, bool Cancelled = false, LocId? Reason = null);

/// <summary>
/// Raised on a host before a horror takes control of it. Cancel it to keep the mind safe.
/// </summary>
[ByRefEvent]
public record struct HorrorControlAttemptEvent(EntityUid Horror, bool Cancelled = false, LocId? Reason = null);

[Serializable, NetSerializable]
public enum HorrorChemicalsUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class HorrorInjectChemicalMessage(ProtoId<HorrorChemicalPrototype> chemical) : BoundUserInterfaceMessage
{
    public readonly ProtoId<HorrorChemicalPrototype> Chemical = chemical;
}

[Serializable, NetSerializable]
public sealed class HorrorChemicalsBoundUserInterfaceState(List<ProtoId<HorrorChemicalPrototype>> chemicals, int stored, bool hasHost)
    : BoundUserInterfaceState
{
    public readonly List<ProtoId<HorrorChemicalPrototype>> Chemicals = chemicals;
    public readonly int Stored = stored;
    public readonly bool HasHost = hasHost;
}

public sealed partial class HorrorMutateActionEvent : InstantActionEvent;
public sealed partial class HorrorSeekSoulActionEvent : InstantActionEvent;
public sealed partial class HorrorConsumeSoulActionEvent : InstantActionEvent;
public sealed partial class HorrorHideActionEvent : InstantActionEvent;
public sealed partial class HorrorChameleonActionEvent : InstantActionEvent;
public sealed partial class HorrorTripActionEvent : InstantActionEvent;
public sealed partial class HorrorLubeSpillActionEvent : InstantActionEvent;
public sealed partial class HorrorTentacleActionEvent : InstantActionEvent;
public sealed partial class HorrorTransferHostActionEvent : EntityTargetActionEvent;
public sealed partial class HorrorJumpstartActionEvent : InstantActionEvent;
public sealed partial class HorrorViewMemoryActionEvent : InstantActionEvent;
public sealed partial class HorrorMindLinkActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class HorrorConsumeSoulDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class HorrorTransferDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class HorrorSqueezeDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class HorrorPryDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum HorrorMutationsUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum HorrorSoulUiKey : byte
{
    Key,
}

/// <summary>
/// Buys an ability (action prototype) or an upgrade in the mutations menu.
/// </summary>
[Serializable, NetSerializable]
public sealed class HorrorBuyMutationMessage(string id, bool upgrade) : BoundUserInterfaceMessage
{
    public readonly string Id = id;
    public readonly bool Upgrade = upgrade;
}

[Serializable, NetSerializable]
public sealed class HorrorMutationsBoundUserInterfaceState(int points, int souls, List<string> owned) : BoundUserInterfaceState
{
    public readonly int Points = points;
    public readonly int Souls = souls;

    /// <summary>
    /// Owned ability and upgrade ids.
    /// </summary>
    public readonly List<string> Owned = owned;
}

/// <summary>
/// One of the souls the horror can pick to hunt.
/// </summary>
[Serializable, NetSerializable]
public sealed record HorrorSoulCandidate(NetEntity Mind, string Name, string? Job);

[Serializable, NetSerializable]
public sealed class HorrorSoulBoundUserInterfaceState(List<HorrorSoulCandidate> candidates) : BoundUserInterfaceState
{
    public readonly List<HorrorSoulCandidate> Candidates = candidates;
}

[Serializable, NetSerializable]
public sealed class HorrorPickSoulMessage(NetEntity mind) : BoundUserInterfaceMessage
{
    public readonly NetEntity Mind = mind;
}

/// <summary>
/// Layers of the soul locator alert sprite.
/// </summary>
[Serializable, NetSerializable]
public enum HorrorLocatorLayers : byte
{
    Arrow,
}
