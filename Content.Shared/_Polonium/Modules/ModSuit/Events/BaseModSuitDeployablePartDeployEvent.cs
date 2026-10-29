namespace Content.Shared._Polonium.Modules.ModSuit.Events;

public abstract partial class BaseModSuitDeployablePartDeployEvent(EntityUid suit, EntityUid? wearer, string slot, int partNumber) : EntityEventArgs
{
    public readonly EntityUid Suit = suit;
    public readonly EntityUid? Wearer = wearer;
    public readonly string Slot = slot;
    public readonly int PartNumber = partNumber;
}
