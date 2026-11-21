using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Lathe;

/// <summary>
/// The lathe's whole queue was cancelled from its console. Stays until the next print starts.
/// </summary>
[RegisterComponent, NetworkedComponent, UnsavedComponent]
public sealed partial class LatheStoppedComponent : Component;

/// <summary>
/// Sent to the server to cancel the item being printed and everything in the queue.
/// </summary>
[Serializable, NetSerializable]
public sealed class LatheStopAllMessage : BoundUserInterfaceMessage;
