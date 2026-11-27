using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Abductors;

[Serializable, NetSerializable]
public sealed class AbductorCuratorSpeechEvent : EntityEventArgs
{
    public readonly List<LocId> Pages;

    public AbductorCuratorSpeechEvent(List<LocId> pages)
    {
        Pages = pages;
    }
}
