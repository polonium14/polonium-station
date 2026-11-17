using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Medical.Surgery;

/// <summary>
/// Surgery step that repairs the most damaged organ of the operated part. Each organ can be repaired once.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryRepairOrgansStepComponent : Component
{
    /// <summary>
    /// The share of its full integrity an organ is brought back to. Organs above it need no repair.
    /// </summary>
    [DataField]
    public float RepairTo = 0.6f;
}

/// <summary>
/// Marks an organ that was already repaired by surgery; after that only medicine or a transplant helps.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OrganRepairedComponent : Component;
