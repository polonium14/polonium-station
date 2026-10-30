using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Modules.ModSuit.Components;

/// <summary>
/// Makes swapping the suit's power cell take time and lets other players pull the cell out of a worn suit.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModSuitCellSlotComponent : Component
{
    /// <summary>
    /// How long it takes to insert or remove the cell of a suit you hold or wear.
    /// </summary>
    [DataField]
    public TimeSpan SwapDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Set while a finished do-after changes the cell, so the instant item slot interactions stay blocked.
    /// </summary>
    [ViewVariables]
    public bool AllowSlotChange;
}

[Serializable, NetSerializable]
public sealed partial class ModSuitCellInsertDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ModSuitCellEjectDoAfterEvent : SimpleDoAfterEvent;
