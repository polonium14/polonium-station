using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Horror.Components;

/// <summary>
/// A horror woken from the smuggled pet carrier. It serves whoever woke it and won't devour their soul.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HorrorServantComponent : Component
{
    /// <summary>
    /// Mind of the one who woke the horror.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Master;

    /// <summary>
    /// Objectives handed to the player once they take the horror.
    /// </summary>
    [DataField]
    public List<Robust.Shared.Prototypes.EntProtoId> PendingObjectives = new();
}
