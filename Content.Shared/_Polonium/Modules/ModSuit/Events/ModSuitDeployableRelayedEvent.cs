namespace Content.Shared._Polonium.Modules.ModSuit.Events;

[ByRefEvent]
public struct ModSuitDeployableRelayedEvent<T>(T args, EntityUid modSuit, int partNumber)
{
    public T Args = args;
    public readonly EntityUid ModSuit = modSuit;
    public readonly int PartNumber = partNumber;
}
