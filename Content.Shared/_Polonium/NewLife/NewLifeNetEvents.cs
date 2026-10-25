using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.NewLife;

[Serializable, NetSerializable]
public sealed class NewLifeRequestEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class NewLifePreviousCharactersEvent : EntityEventArgs
{
    public List<string> Names { get; }

    public NewLifePreviousCharactersEvent(List<string> names)
    {
        Names = names;
    }
}
