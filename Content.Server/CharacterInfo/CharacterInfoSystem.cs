using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Shared.CharacterInfo;
using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;

namespace Content.Server.CharacterInfo;

public sealed partial class CharacterInfoSystem : EntitySystem
{
    [Dependency] private JobSystem _jobs = default!;
    [Dependency] private MindSystem _minds = default!;
    [Dependency] private RoleSystem _roles = default!;
    [Dependency] private SharedObjectivesSystem _objectives = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestCharacterInfoEvent>(OnRequestCharacterInfoEvent);
    }

    private void OnRequestCharacterInfoEvent(RequestCharacterInfoEvent msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue
            || args.SenderSession.AttachedEntity != GetEntity(msg.NetEntity))
            return;

        var entity = args.SenderSession.AttachedEntity.Value;

        var objectives = new Dictionary<string, List<ObjectiveInfo>>();
        string? jobProto = null; // POLONIUM CHANGE: send only the locale-independent job id; client localizes it
        string? briefing = null;
        if (_minds.TryGetMind(entity, out var mindId, out var mind))
        {
            // Get objectives
            foreach (var objective in mind.Objectives)
            {
                var info = _objectives.GetInfo(objective, mindId, mind);
                if (info == null)
                    continue;

                if (!ProtoMan.TryIndex(Comp<ObjectiveComponent>(objective).Issuer, out var issuerProto))
                {
                    Log.Error($"Found incorrect objective issuer {issuerProto} when generating character info for objective {MetaData(objective).EntityPrototype}.");
                    continue;
                }

                // group objectives by their issuer
                var issuer = issuerProto.LocalizedName;
                if (!objectives.ContainsKey(issuer))
                    objectives[issuer] = new List<ObjectiveInfo>();
                objectives[issuer].Add(info.Value);
            }

            // POLONIUM CHANGE: send the raw job prototype id; the client resolves the
            // localized display name and the highlight key from it (loc titles vary per locale).
            if (_jobs.MindTryGetJobId(mindId, out var jobId) && jobId is { } id)
                jobProto = id.Id;

            // Get briefing
            briefing = _roles.MindGetBriefing(mindId);
        }

        // POLONIUM CHANGE: send jobProto instead of a server-localized job title
        RaiseNetworkEvent(new CharacterInfoEvent(GetNetEntity(entity), jobProto, objectives, briefing), args.SenderSession);
    }
}
