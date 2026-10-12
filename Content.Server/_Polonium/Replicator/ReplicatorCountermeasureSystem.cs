using Content.Server.Chat.Systems;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Tools.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Replicator;

public sealed partial class ReplicatorCountermeasureSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;

    private readonly HashSet<ProtoId<LatheRecipePackPrototype>> _unlocked = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorColossusCoreComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<ReplicatorColossusCoreComponent, ReplicatorColossusCoreStudyDoAfterEvent>(OnStudyDoAfter);
        SubscribeLocalEvent<ReplicatorColossusCoreComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ReplicatorColossusWreckComponent, ToolUseAttemptEvent>(OnWreckToolUse);

        SubscribeLocalEvent<LatheComponent, LatheGetRecipesEvent>(OnGetRecipes);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _unlocked.Clear();
    }

    private void OnGetVerbs(Entity<ReplicatorColossusCoreComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Studied || HasComp<ReplicatorComponent>(args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("replicator-colossus-core-verb-study"),
            Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.StudyTime,
                new ReplicatorColossusCoreStudyDoAfterEvent(), ent, target: ent)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            }),
        });
    }

    private void OnStudyDoAfter(Entity<ReplicatorColossusCoreComponent> ent, ref ReplicatorColossusCoreStudyDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || ent.Comp.Studied)
            return;

        args.Handled = true;
        ent.Comp.Studied = true;
        _unlocked.Add(ent.Comp.Unlocks);
        _audio.PlayPvs(ent.Comp.StudySound, ent);

        _chat.DispatchGlobalAnnouncement(Loc.GetString(ent.Comp.Announcement),
            Loc.GetString("comms-console-announcement-title-centcom"),
            colorOverride: Color.LightGreen);
    }

    private void OnExamined(Entity<ReplicatorColossusCoreComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Studied
            ? "replicator-colossus-core-examine-studied"
            : "replicator-colossus-core-examine"));
    }

    private void OnWreckToolUse(Entity<ReplicatorColossusWreckComponent> ent, ref ToolUseAttemptEvent args)
    {
        if (!HasComp<ReplicatorComponent>(args.User))
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("replicator-colossus-wreck-fail-replicator"), ent, args.User, PopupType.SmallCaution);
    }

    private void OnGetRecipes(Entity<LatheComponent> ent, ref LatheGetRecipesEvent args)
    {
        if (_unlocked.Count == 0 || _tag.HasTag(ent, ReplicatorHiveSystem.StructureTag) || HasComp<ReplicatorHiveComponent>(ent))
            return;

        foreach (var pack in _unlocked)
        {
            args.Recipes.UnionWith(_prototype.Index(pack).Recipes);
        }
    }
}
