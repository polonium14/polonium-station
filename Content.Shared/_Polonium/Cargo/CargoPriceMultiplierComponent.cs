using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Cargo;

/// <summary>
/// Added to a station with a bank account. Holds the cargo price multipliers fixed for the current round.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(CargoPriceMultiplierSystem))]
public sealed partial class CargoPriceMultiplierComponent : Component
{
    /// <summary>
    /// Multiplier for the cost of cargo orders.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BuyMultiplier = 1f;

    /// <summary>
    /// Multiplier for the money received from selling goods on cargo pallets.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SellMultiplier = 1f;
}
