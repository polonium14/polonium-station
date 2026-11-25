using Content.Shared.Item;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Medical.Surgery;

/// <summary>
/// Surgery step that hides the item in the surgeon's hand in the patient's chest cavity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryCavityInsertStepComponent : Component
{
    /// <summary>
    /// The largest item that fits in the cavity.
    /// </summary>
    [DataField]
    public ProtoId<ItemSizePrototype> MaxSize = "Small";
}

/// <summary>
/// Surgery step that takes whatever is hidden in the patient's chest cavity out into the surgeon's hand.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryCavityRemoveStepComponent : Component;

/// <summary>
/// Marks a body part that has a surgical cavity container, so its contents drop out when the part is gone.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgicalCavityComponent : Component;
