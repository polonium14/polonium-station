using Content.Shared.CharacterInfo;
using Content.Shared.Objectives;
using Content.Shared.Roles;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.CharacterInfo;

public sealed partial class CharacterInfoSystem : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _proto = default!; // POLONIUM CHANGE: localize job name from proto id

    public event Action<CharacterData>? OnCharacterUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CharacterInfoEvent>(OnCharacterInfoEvent);
    }

    public void RequestCharacterInfo()
    {
        var entity = _players.LocalEntity;
        if (entity == null)
        {
            return;
        }

        RaiseNetworkEvent(new RequestCharacterInfoEvent(GetNetEntity(entity.Value)));
    }

    private void OnCharacterInfoEvent(CharacterInfoEvent msg, EntitySessionEventArgs args)
    {
        var entity = GetEntity(msg.NetEntity);
        // POLONIUM CHANGE: resolve the localized job name client-side from the proto id, so
        // every client shows the title in its own locale (falls back to a generic label when
        // the entity has no job). JobProto is kept for the locale-independent highlight key.
        var job = msg.JobProto is { } proto && _proto.TryIndex<JobPrototype>(proto, out var jobPrototype)
            ? jobPrototype.LocalizedName
            : Loc.GetString("character-info-no-profession");
        var data = new CharacterData(entity, job, msg.JobProto, msg.Objectives, msg.Briefing, Name(entity));

        OnCharacterUpdate?.Invoke(data);
    }

    public List<Control> GetCharacterInfoControls(EntityUid uid)
    {
        var ev = new GetCharacterInfoControlsEvent(uid);
        RaiseLocalEvent(uid, ref ev, true);
        return ev.Controls;
    }

    public readonly record struct CharacterData(
        EntityUid Entity,
        string Job,
        string? JobProto, // POLONIUM CHANGE: locale-independent job id for chat highlights
        Dictionary<string, List<ObjectiveInfo>> Objectives,
        string? Briefing,
        string EntityName
    );

    /// <summary>
    /// Event raised to get additional controls to display in the character info menu.
    /// </summary>
    [ByRefEvent]
    public readonly record struct GetCharacterInfoControlsEvent(EntityUid Entity)
    {
        public readonly List<Control> Controls = new();

        public readonly EntityUid Entity = Entity;
    }
}
