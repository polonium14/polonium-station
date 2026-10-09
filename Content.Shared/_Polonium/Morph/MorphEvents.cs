using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Morph;

/// <summary>
/// Action event for disguising as the targeted entity.
/// </summary>
public sealed partial class MorphDisguiseActionEvent : EntityTargetActionEvent;

/// <summary>
/// Action event for returning to the true form.
/// </summary>
public sealed partial class MorphRevealActionEvent : InstantActionEvent;

/// <summary>
/// Action event for opening the stomach radial menu.
/// </summary>
public sealed partial class MorphStomachActionEvent : InstantActionEvent;

/// <summary>
/// Action event for spitting the prepared stomach content at a location.
/// </summary>
public sealed partial class MorphSpitActionEvent : WorldTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class MorphDigestDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum MorphStomachUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum MorphStomachAction : byte
{
    /// <summary>
    /// Drop the entity at the morph's feet.
    /// </summary>
    Drop,

    /// <summary>
    /// Prepare the entity to be thrown with the spit action.
    /// </summary>
    PrepareThrow,

    /// <summary>
    /// Destroy the entity, healing the morph if it was alive.
    /// </summary>
    Digest,
}

/// <summary>
/// Sent when a player picks an option for one of the stomach contents in the radial menu.
/// </summary>
[Serializable, NetSerializable]
public sealed class MorphStomachSelectMessage(NetEntity target, MorphStomachAction action) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
    public readonly MorphStomachAction Action = action;
}
