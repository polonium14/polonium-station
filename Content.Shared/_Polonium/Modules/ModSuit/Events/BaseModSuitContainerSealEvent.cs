namespace Content.Shared._Polonium.Modules.ModSuit.Events;

public abstract partial class BaseModSuitContainerSealEvent(EntityUid part) : EntityEventArgs
{
    public readonly EntityUid Part = part;
}
