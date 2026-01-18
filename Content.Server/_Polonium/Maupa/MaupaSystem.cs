using Content.Server.Chat;
using Content.Server.Chat.Systems;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.ActionBlocker;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Maupa;

public sealed class MaupaSystem : EntitySystem
{
    [Dependency] private readonly AutoEmoteSystem _autoEmote = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MaupaComponent, ComponentStartup>(OnInit);
    }

    private void OnInit(EntityUid uid, MaupaComponent component, ComponentStartup args)
    {

        EnsureComp<AutoEmoteComponent>(uid);
        _autoEmote.AddEmote(uid, "MaupaScream");

        //if (TryComp<HTNComponent>(uid, out var htn))
        //{
        //    _npc.WakeNPC(uid, htn);
        //}
    }
}
