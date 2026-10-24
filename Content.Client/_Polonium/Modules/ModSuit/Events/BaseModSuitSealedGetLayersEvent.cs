namespace Content.Client._Polonium.Modules.ModSuit.Events;

public abstract partial class BaseModSuitSealedGetLayersEvent : EntityEventArgs
{
    public List<PrototypeLayerData> Layers = [];
}
