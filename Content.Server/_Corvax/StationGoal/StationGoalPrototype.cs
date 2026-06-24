using Robust.Shared.Prototypes;

namespace Content.Server._Corvax.StationGoal;

[Prototype]
public sealed partial class StationGoalPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Text { get; set; } = string.Empty;

    [DataField]
    public int? MinPlayers;

    [DataField]
    public int? MaxPlayers;

    /// <summary>
    /// Goal may require certain items to complete. These items will appear near the receiving fax machine at the start of the round.
    /// </summary>
    [DataField]
    public List<EntProtoId> Spawns = new();
}
