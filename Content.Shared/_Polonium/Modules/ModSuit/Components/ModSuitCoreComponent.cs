using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Modules.ModSuit.Components;

/// <summary>
/// A special R.I.G. core. The suit built around it gets the bonuses listed here.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModSuitCoreComponent : Component
{
    /// <summary>
    /// Added to the suit's module complexity limit.
    /// </summary>
    [DataField]
    public int ComplexityBonus;

    /// <summary>
    /// Added to the suit's walk and sprint speed modifiers, powered or not.
    /// </summary>
    [DataField]
    public float SpeedBonus;

    /// <summary>
    /// Charge per second that the core adds to the suit's power cell.
    /// </summary>
    [DataField]
    public float RechargeRate;

    /// <summary>
    /// Material stack the suit can be refuelled with.
    /// </summary>
    [DataField]
    public ProtoId<StackPrototype>? FuelStack;

    /// <summary>
    /// Part of the power cell's maximum charge that one piece of <see cref="FuelStack"/> restores.
    /// </summary>
    [DataField]
    public float FuelCharge;

    /// <summary>
    /// Colour of the suit's lights while it has power. Null keeps the suit's own colour.
    /// </summary>
    [DataField]
    public Color? LightColor;
}
