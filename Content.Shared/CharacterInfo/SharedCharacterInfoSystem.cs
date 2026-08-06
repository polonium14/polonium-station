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
    public readonly string JobTitle;
    // POLONIUM CHANGE START: carry the locale-independent job prototype id so the
    // client can build chat highlight job keywords (JobTitle is localized per server locale).
    public readonly string? JobProto;
    // POLONIUM CHANGE END
    public readonly Dictionary<string, List<ObjectiveInfo>> Objectives;
    public readonly string? Briefing;

    // POLONIUM CHANGE START: added jobProto parameter
    public CharacterInfoEvent(NetEntity netEntity, string jobTitle, string? jobProto, Dictionary<string, List<ObjectiveInfo>> objectives, string? briefing)
    {
        NetEntity = netEntity;
        JobTitle = jobTitle;
        JobProto = jobProto;
        Objectives = objectives;
        Briefing = briefing;
    }
    // POLONIUM CHANGE END
}
