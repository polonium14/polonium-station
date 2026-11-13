namespace Content.Client._Polonium.VendingMachines;

/// <summary>
/// Remembers the last vending machine search per machine prototype for the current session.
/// </summary>
public sealed class VendingMachineSearchMemorySystem : EntitySystem
{
    private readonly Dictionary<string, string> _queries = new();

    /// <summary>
    /// Returns the last search typed into a machine of this type, or an empty string.
    /// </summary>
    public string GetQuery(EntityUid machine)
    {
        return Prototype(machine) is { } proto && _queries.TryGetValue(proto.ID, out var query)
            ? query
            : string.Empty;
    }

    /// <summary>
    /// Stores the search for every machine of this type; an empty query forgets it.
    /// </summary>
    public void SetQuery(EntityUid machine, string query)
    {
        if (Prototype(machine) is not { } proto)
            return;

        if (string.IsNullOrWhiteSpace(query))
            _queries.Remove(proto.ID);
        else
            _queries[proto.ID] = query;
    }
}
