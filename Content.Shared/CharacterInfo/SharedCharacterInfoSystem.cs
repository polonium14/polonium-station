using Content.Shared.Objectives;
using Robust.Shared.Serialization;

namespace Content.Shared.CharacterInfo;

[Serializable, NetSerializable]
public sealed class RequestCharacterInfoEvent : EntityEventArgs
{
    public readonly NetEntity NetEntity;

    public RequestCharacterInfoEvent(NetEntity netEntity)
    {
        NetEntity = netEntity;
    }
}

[Serializable, NetSerializable]
public sealed class CharacterInfoEvent : EntityEventArgs
{
    public readonly NetEntity NetEntity;
    // POLONIUM CHANGE: send only the locale-independent job prototype id (null if the
    // entity has no job). The client resolves both the localized display name and the chat
    // highlight key from it, so there is a single source of truth over the wire.
    public readonly string? JobProto;
    public readonly Dictionary<string, List<ObjectiveInfo>> Objectives;
    public readonly string? Briefing;

    // POLONIUM CHANGE: jobProto replaces the previously server-localized jobTitle
    public CharacterInfoEvent(NetEntity netEntity, string? jobProto, Dictionary<string, List<ObjectiveInfo>> objectives, string? briefing)
    {
        NetEntity = netEntity;
        JobProto = jobProto;
        Objectives = objectives;
        Briefing = briefing;
    }
}
