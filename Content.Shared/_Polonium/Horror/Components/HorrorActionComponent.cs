using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Horror.Components;

/// <summary>
/// Where an eldritch horror can use an ability.
/// </summary>
[Flags]
public enum HorrorActionState : byte
{
    None = 0,
    /// <summary>
    /// The horror crawls on its own.
    /// </summary>
    Free = 1 << 0,
    /// <summary>
    /// The horror sits in a host's head.
    /// </summary>
    Infesting = 1 << 1,
    /// <summary>
    /// The horror controls the host's body. These actions belong to the host body.
    /// </summary>
    Controlling = 1 << 2,
}

/// <summary>
/// Marks an action prototype as an eldritch horror ability.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HorrorActionComponent : Component
{
    /// <summary>
    /// Where the action is available.
    /// </summary>
    [DataField(required: true)]
    public HorrorActionState States;

    /// <summary>
    /// Souls it costs in the mutations menu. Zero means it can't be bought.
    /// </summary>
    [DataField]
    public int SoulPrice;
}
