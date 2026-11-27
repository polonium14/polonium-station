using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Body;

/// <summary>
/// Marks an organ that died and can only be cut out and replaced.
/// It keeps its slot in the body, but has none of the components that give an organ its function.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DeadOrganComponent : Component;
