namespace Content.Shared._Polonium.Modules.ModSuit.Events;

public abstract partial class BaseModSuitSealEvent(EntityUid? wearer) : EntityEventArgs
{
    public readonly EntityUid? Wearer = wearer;
}
