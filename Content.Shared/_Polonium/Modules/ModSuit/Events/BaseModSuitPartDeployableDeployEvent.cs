namespace Content.Shared._Polonium.Modules.ModSuit.Events;

public abstract partial class BaseModSuitPartDeployableDeployEvent(EntityUid part, EntityUid? wearer, string slot, int partNumber) : EntityEventArgs
{
    public readonly EntityUid Part = part;
    public readonly EntityUid? Wearer = wearer;
    public readonly string Slot = slot;
    public readonly int PartNumber = partNumber;
}
